﻿using ECommerce.Data;
using ECommerce.Data.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace ECommerce.Services.Payments
{
    public class StripePaymentsGatewayService : IPaymentsGateway
    {
        private readonly ILogger<StripePaymentsGatewayService> _logger;

        public StripePaymentsGatewayService(ILogger<StripePaymentsGatewayService> logger, string apiKey)
        {
            _logger = logger;
            StripeConfiguration.ApiKey = apiKey;
        }

        #region Customers_Services

        public async Task<List<CustomerModel>> GetCustomers(int take)
        {
            var service = new CustomerService();
            var stripeCustomers = await service.ListAsync(new CustomerListOptions()
            {
                Limit = take > 100 ? 100 : take,
            });

            return stripeCustomers.Select(x => new CustomerModel(x.Id)
            {
                Email = x.Email,
                Name = x.Name,
                SystemId = x.Metadata["ID"]
            }).ToList();
        }

        public async Task<CustomerModel> GetCustomerByEmail(string email, params PaymentModelInclude[] includes)
        {
            var service = new CustomerService();
            var stripeCustomers = await service.ListAsync(new CustomerListOptions()
            {
                Email = email
            });

            if (!stripeCustomers.Any())
                return null;

            var stripeCustomer = stripeCustomers.Single();

            var customerModel = new CustomerModel(stripeCustomer.Id)
            {
                Email = email,
                Name = stripeCustomer.Name
            };
            if (includes.Any() && includes.Contains(PaymentModelInclude.PaymentMethods))
            {
                var paymentMethods = await this.GetPaymentMethods(stripeCustomer.Id, PaymentMethodType.Card);
                customerModel.PaymentMethods = paymentMethods;
            }

            return customerModel;
        }

        public async Task<bool> CreateCustomer(CreateCustomerDTO model)
        {
            this._logger.LogInformation("Creating Customer in Stripe");
            try
            {
                var options = new CustomerCreateOptions
                {
                    Email = model.Email,
                    Name = model.Name,
                    Metadata = new Dictionary<string, string>()
                    {
                        { "ID", model.SystemId ?? string.Empty}
                    }
                };
                var service = new CustomerService();
                Customer c = await service.CreateAsync(options);
                this._logger.LogInformation("Customer Created succesfully");
                return true;
            }
            catch (Exception ex)
            {
                this._logger.LogInformation($"An error occured during customer creation, {ex}");
                return false;
            }
        }

        public async Task<CustomerModel> DeleteCustomerByEmail(string email)
        {
            var service = new CustomerService();
            var stripeCustomers = await service.ListAsync(new CustomerListOptions()
            {
                Email = email
            });

            var stripeCustomer = await GetCustomerByEmail(email);
            if (stripeCustomer == null) return null;

            var deletedStripeCustomer = await service.DeleteAsync(stripeCustomer.Id);
            return new CustomerModel(deletedStripeCustomer.Id)
            {
                Name = deletedStripeCustomer.Name,
                Email = deletedStripeCustomer.Email,
                SystemId = deletedStripeCustomer.Metadata?.GetValueOrDefault("ID")
            };
        }

        #endregion



        public async Task<List<PlanModel>> PopulatePlans(List<PlanModel> plans)
        {
            var productService = new ProductService();
            var existingProducts = await productService.ListAsync(new ProductListOptions()
            {
                Active = true
            });

            var priceService = new PriceService();
            var existingPrices = await priceService.ListAsync(new PriceListOptions()
            {
                Active = true
            });

            List<PlanModel> result = new List<PlanModel>();

            foreach (var plan in plans)
            {
                var existingProduct = existingProducts.FirstOrDefault(x => x.Name.Equals(plan.Name, StringComparison.OrdinalIgnoreCase));
                IEnumerable<Price> prices;
                if (existingProduct != null)
                {
                    this._logger.LogInformation($"Product with NAME:{plan.Name} already exists.");
                    var existingPricesForProduct = existingPrices.Where(x => x.ProductId == existingProduct.Id);
                    prices = await CreatePrices(plan.Prices, existingProduct, existingPricesForProduct);
                }
                else
                {
                    var options = new ProductCreateOptions
                    {
                        Name = plan.Name,
                    };

                    this._logger.LogInformation($"Creating Product with NAME:{plan.Name}");
                    existingProduct = await productService.CreateAsync(options);
                    this._logger.LogInformation("Product created succesfully");
                    prices = await CreatePrices(plan.Prices, existingProduct);
                }

                result.Add(new PlanModel()
                {
                    Id = existingProduct.Id,
                    Name = existingProduct.Name,
                    Prices = prices.Select(p => new PlanPriceModel()
                    {
                        Id = p.Id,
                        Currency = p.Currency == "usd" ? Currency.USD : Currency.Eur,
                        Interval = p.Recurring?.Interval == "month" ? PriceInterval.Monthly : PriceInterval.Yearly,
                        UnitAmount = p.UnitAmount.GetValueOrDefault()
                    }).ToList()
                });
            }
            return result;
        }

        private async Task<IEnumerable<Price>> CreatePrices(List<PlanPriceModel> prices, Product existingProduct,
            IEnumerable<Price> existingPricesForProduct = null)
        {
            List<Price> stripePrices = new List<Price>();
            var priceService = new PriceService();
            foreach (var price in prices)
            {
                var existingPrice = existingPricesForProduct?.FirstOrDefault(x => x.UnitAmount == price.UnitAmount);
                if (existingPrice != null)
                {
                    this._logger.LogInformation($"Price with AMOUNT:{existingPrice.UnitAmount} for Product with NAME:{existingProduct.Name} already exists.");
                    continue;
                }

                var options = new PriceCreateOptions
                {
                    Product = existingProduct.Id,
                    UnitAmount = price.UnitAmount,
                    Currency = price.Currency.ToString().ToLower(),
                    Recurring = new PriceRecurringOptions
                    {
                        Interval = price.Interval == PriceInterval.Monthly ? "month" : "year"
                    },
                };
                var createdPrice = await priceService.CreateAsync(options);
                stripePrices.Add(createdPrice);
            }
            return stripePrices;
        }


        public async Task<PaymentMethodModel> AttachPaymentMethod(string paymentMethodId, string customerId, bool makeDefault = true)
        {
            try
            {
                var options = new PaymentMethodAttachOptions
                {
                    Customer = customerId,
                };
                var service = new PaymentMethodService();
                var stripePaymentMethod = await service.AttachAsync(paymentMethodId, options);

                if (makeDefault)
                {
                    // Update customer's default invoice payment method
                    var customerOptions = new CustomerUpdateOptions
                    {
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = stripePaymentMethod.Id,
                        },
                    };
                    var customerService = new CustomerService();
                    await customerService.UpdateAsync(customerId, customerOptions);
                }

                PaymentMethodModel result = new PaymentMethodModel(stripePaymentMethod.Id);

                if (!Enum.TryParse(stripePaymentMethod.Type, true, out PaymentMethodType paymentMethodType))
                {
                    this._logger.LogError($"Cannot recognize PAYMENT_METHOD_TYPE:{stripePaymentMethod.Type}");
                }
                result.Type = paymentMethodType;

                if (result.Type == PaymentMethodType.Card)
                {
                    result.Card = new PaymentMethodCardModel()
                    {
                        Brand = stripePaymentMethod.Card.Brand,
                        Country = stripePaymentMethod.Card.Country,
                        ExpMonth = stripePaymentMethod.Card.ExpMonth,
                        ExpYear = stripePaymentMethod.Card.ExpYear,
                        Issuer = stripePaymentMethod.Card.Issuer,
                        Last4 = stripePaymentMethod.Card.Last4,
                        Description = stripePaymentMethod.Card.Description,
                        Fingerprint = stripePaymentMethod.Card.Fingerprint,
                        Funding = stripePaymentMethod.Card.Funding,
                        Iin = stripePaymentMethod.Card.Iin
                    };
                }

                return result;
            }
            catch (StripeException se)
            {
                this._logger.LogError($"An error occured during attach of PAYMENT_METHOD:{paymentMethodId} for CUSTOMER:{customerId}, {se}");
            }
            catch (Exception ex)
            {
                this._logger.LogError($"An error occured during attach of PAYMENT_METHOD:{paymentMethodId} for CUSTOMER:{customerId}, {ex}");
            }
            return null;
        }

        public async Task DeletePaymentMethod(string paymentMethodId)
        {
            var service = new PaymentMethodService();
            var paymentMethod = await service.DetachAsync(paymentMethodId);

        }

        public async Task<bool> CreateSubscription(string customerEmail, string priceId)
        {
            var stripeCustomer = await this.GetCustomerByEmail(customerEmail);
            if (stripeCustomer == null)
                return false;

            var subscriptionOptions = new SubscriptionCreateOptions
            {
                Customer = stripeCustomer.Id,
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Price = priceId,
                    },
                },
            };
            subscriptionOptions.AddExpand("latest_invoice.payment_intent");
            var subscriptionService = new SubscriptionService();
            try
            {
                Subscription subscription = await subscriptionService.CreateAsync(subscriptionOptions);
                return true;
            }
            catch (StripeException e)
            {
                this._logger.LogError($"An error occured during creation of subscription for CUSTOMER:{stripeCustomer.Id} and PRICE:{priceId}, {e}");
                return false;
            }
        }

        public async Task<FuturePaymentIntentModel> PrepareForFuturePaymentWithCustomerEmail(string customerEmail)
        {
            var stripeCustomer = await this.GetCustomerByEmail(customerEmail);
            if (stripeCustomer == null)
                return null;

            FuturePaymentIntentModel intent = await PrepareForFuturePayment(stripeCustomer.Id);
            return intent;
        }

        public async Task<FuturePaymentIntentModel> PrepareForFuturePayment(string customerId)
        {
            var options = new SetupIntentCreateOptions
            {
                Customer = customerId,
                Expand = new List<string>()
                {
                    "customer"
                }
            };

            var service = new SetupIntentService();
            var intent = await service.CreateAsync(options);
            return new FuturePaymentIntentModel()
            {
                Id = intent.Id,
                IntentSecret = intent.ClientSecret,
                Customer = new CustomerModel(intent.Customer.Id)
                {
                    Email = intent.Customer.Email,
                    Name = intent.Customer.Name,
                    SystemId = intent.Customer.Metadata?.GetValueOrDefault("ID"),
                }
            };
        }


        public async Task<List<PaymentMethodModel>> GetPaymentMethods(string customerId, PaymentMethodType paymentMethodType)
        {
            var options = new PaymentMethodListOptions
            {
                Customer = customerId,
                Type = paymentMethodType.ToString().ToLower()
            };

            var service = new PaymentMethodService();
            var paymentMethods = await service.ListAsync(options);


            List<PaymentMethodModel> result = new List<PaymentMethodModel>();
            foreach (var stripePaymentMethod in paymentMethods)
            {
                if (!Enum.TryParse(stripePaymentMethod.Type, true, out PaymentMethodType currPaymentMethodType))
                {
                    this._logger.LogError($"Cannot recognize PAYMENT_METHOD_TYPE:{stripePaymentMethod.Type}");
                    continue;
                }

                PaymentMethodModel currentPaymentMethod = new PaymentMethodModel(stripePaymentMethod.Id)
                {
                    Type = currPaymentMethodType
                };

                if (currPaymentMethodType == PaymentMethodType.Card)
                {
                    currentPaymentMethod.Card = new PaymentMethodCardModel()
                    {
                        Brand = stripePaymentMethod.Card.Brand,
                        Country = stripePaymentMethod.Card.Country,
                        ExpMonth = stripePaymentMethod.Card.ExpMonth,
                        ExpYear = stripePaymentMethod.Card.ExpYear,
                        Issuer = stripePaymentMethod.Card.Issuer,
                        Last4 = stripePaymentMethod.Card.Last4,
                        Description = stripePaymentMethod.Card.Description,
                        Fingerprint = stripePaymentMethod.Card.Fingerprint,
                        Funding = stripePaymentMethod.Card.Funding,
                        Iin = stripePaymentMethod.Card.Iin
                    };
                }

                result.Add(currentPaymentMethod);
            }
            return result;
        }

        public async Task<List<PaymentMethodModel>> GetPaymentMethodsByCustomerEmail(string customerEmail, PaymentMethodType paymentMethodType)
        {
            CustomerModel customer = await this.GetCustomerByEmail(customerEmail);

            return await this.GetPaymentMethods(customer.Id, paymentMethodType);
        }


        public async Task ChargeWithCustomerEmail(string customerEmail, string paymentMethodId, Currency currency, long unitAmount,
            bool sendEmailAfterSuccess = true, string emailDescription = "")
        {
            var customer = await GetCustomerByEmail(customerEmail);
            await Charge(customer.Id, paymentMethodId, currency, unitAmount, customerEmail, sendEmailAfterSuccess, emailDescription);
        }

        // customize receipt -> https://dashboard.stripe.com/settings/branding
        // -> https://dashboard.stripe.com/settings/billing/invoice
        // in case of email send uppon failure -> https://dashboard.stripe.com/settings/billing/automatic
        public async Task Charge(string customerId, string paymentMethodId,
            Currency currency, long unitAmount, string customerEmail, bool sendEmailAfterSuccess = true, string emailDescription = "")
        {
            try
            {
                var service = new PaymentIntentService();
                var options = new PaymentIntentCreateOptions
                {
                    Amount = unitAmount,
                    Currency = currency.ToString().ToLower(),
                    Customer = customerId,
                    PaymentMethod = paymentMethodId,
                    Confirm = true,
                    OffSession = true,
                    ReceiptEmail = sendEmailAfterSuccess ? customerEmail : null,
                    Description = emailDescription,
                };
                await service.CreateAsync(options);
            }
            catch (StripeException e)
            {
                switch (e.StripeError.Type)
                {
                    case "card_error":
                        // Error code will be authentication_required if authentication is needed
                        Console.WriteLine("Error code: " + e.StripeError.Code);
                        var paymentIntentId = e.StripeError.PaymentIntent.Id;
                        var service = new PaymentIntentService();
                        var paymentIntent = service.Get(paymentIntentId);

                        Console.WriteLine(paymentIntent.Id);
                        break;
                    default:
                        break;
                }
            }
        }


        public async Task<IEnumerable<ChargeModel>> GetPaymentStatus(string paymentId)
        {
            var service = new PaymentIntentService();
            var intent = await service.GetAsync(paymentId);
            var charges = intent.Charges.Data;

            return charges.Select(x => new ChargeModel(x.Id)
            {
                Status = x.Status
            });
        }
    }
}
