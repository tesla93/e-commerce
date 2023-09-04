using ECommerce.Data;
using ECommerce.Data.Models;
using ECommerce.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Server.Controllers.Payments
{
    [Route("api/charge")]
    [ApiController]
    public class ChargeController : ControllerBase
    {

        private readonly IPaymentsGateway _paymentsGateway;
        public ChargeController(IPaymentsGateway paymentsGateway)
        {
            _paymentsGateway = paymentsGateway;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ChargeDTO charge)
        {
            await _paymentsGateway.ChargeWithCustomerEmail(charge.CustomerEmail, charge.PaymentMethodId, charge.Currency, (long)charge.Amount * 100);
            return Ok(charge.CustomerEmail);
        }
    }
}
