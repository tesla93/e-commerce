using ECommerce.Data.Models;
using ECommerce.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace ECommerce.Server.Controllers.Payments
{
    [Route("api/subscription")]
    [ApiController]
    public class SubscriptionsController : ControllerBase
    {

        private readonly IPaymentsGateway _paymentsGateway;
        private readonly IConfiguration _configuration;

        public SubscriptionsController(IPaymentsGateway paymentsGateway,
            IConfiguration configuration)
        {
            _paymentsGateway = paymentsGateway;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> CreateIntent([FromBody] CustomerModel customer)
        {
            customer = await this._paymentsGateway.GetCustomerByEmail(customer.Email);

            var options = new SetupIntentCreateOptions
            {
                Customer = customer.Id,
            };
            var service = new SetupIntentService();
            var intent = service.Create(options);
            var clientSecret = intent.ClientSecret;

            return Ok(customer);
        }

//        {
//  "paymentMethodId": "card_1NmkuUGN6ZAFL9hrVZvhGG8l",
//  "customerId": "cus_OZsyNJAXelYtnZ",
//  "priceId": "price_1NlZjwGN6ZAFL9hr50yRXDoU"
//}


    [HttpPost("create-subscription")]
        public async Task<ActionResult<Subscription>> Create([FromBody] CreateSubscriptionRequest req)
        {
            // Attach payment method
            var options = new PaymentMethodAttachOptions
            {
                Customer = req.CustomerId,
            };
            var service = new PaymentMethodService();
            var paymentMethod = service.Attach(req.PaymentMethodId, options);

            // Update customer's default invoice payment method
            var customerOptions = new CustomerUpdateOptions
            {
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = paymentMethod.Id,
                },
            };
            var customerService = new CustomerService();
            customerService.Update(req.CustomerId, customerOptions);

            // Create subscription
            var subscriptionOptions = new SubscriptionCreateOptions
            {
                Customer = req.CustomerId,
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Price = req.PriceId,
                    },
                },
            };
            subscriptionOptions.AddExpand("latest_invoice.payment_intent");
            var subscriptionService = new SubscriptionService();
            try
            {
                Subscription subscription = subscriptionService.Create(subscriptionOptions);
                return subscription;
            }
            catch (StripeException e)
            {
                Console.WriteLine($"Failed to create subscription.{e}");
                return BadRequest();
            }
        }
    }
}
