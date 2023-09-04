using ECommerce.Data;
using ECommerce.Data.Models;
using ECommerce.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Server.Controllers.Payments
{
    [Route("api/customers")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        private readonly IPaymentsGateway _paymentsGateway;

        public CustomersController(IPaymentsGateway paymentsGateway)
        {
            _paymentsGateway = paymentsGateway;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCustomerDTO model)
        {
            var isSuccess = await _paymentsGateway.CreateCustomer(model);
            return Ok(isSuccess);
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var customers = await _paymentsGateway.GetCustomers(take: 10);
            return Ok(customers);
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(string email)
        {
            var customers = await _paymentsGateway.DeleteCustomerByEmail(email);
            return Ok(customers);
        }


        [HttpPost("populate")]
        public async Task PopulatePlan()
        {
            var res = await this._paymentsGateway.PopulatePlans(new List<PlanModel>() {
                new PlanModel()
                {
                    Name = "basic",
                    Prices = new List<PlanPriceModel>()
                    {
                        new PlanPriceModel()
                        {
                            Interval = PriceInterval.Monthly,
                            Currency = Currency.USD,
                            UnitAmount = 1000
                        },
                        new PlanPriceModel()
                        {
                            Interval = PriceInterval.Yearly,
                            Currency = Currency.USD,
                            UnitAmount = 8000
                        }
                    }
                },
                 new PlanModel()
                {
                    Name = "premium",
                    Prices = new List<PlanPriceModel>()
                    {
                        new PlanPriceModel()
                        {
                            Interval = PriceInterval.Monthly,
                            Currency = Currency.Eur,
                            UnitAmount = 1200
                        },
                        new PlanPriceModel()
                        {
                            Interval = PriceInterval.Yearly,
                            Currency = Currency.Eur,
                            UnitAmount = 8700
                        }
                    }
                }

            });
        }

    }
}
