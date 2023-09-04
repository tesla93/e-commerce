using ECommerce.Data;
using ECommerce.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Server.Controllers.Payments
{
    [Route("api/payments")]
    [ApiController]
    public class PaymentMethodsController : ControllerBase
    {
        private readonly IPaymentsGateway _paymentsGateway;
        private readonly IConfiguration _configuration;

        public PaymentMethodsController(IPaymentsGateway paymentsGateway,
            IConfiguration configuration)
        {
            _paymentsGateway = paymentsGateway;
            _configuration = configuration;
        }

        [HttpGet("{customerEmail}")]
        public async Task<IActionResult> GetCustomer(string customerEmail)
        {
            var customer = await _paymentsGateway.GetCustomerByEmail(customerEmail, PaymentModelInclude.PaymentMethods);
            if (customer == null)
                return BadRequest();
            var stripePublicKey = _configuration.GetSection("Stripe").GetValue<string>("publicKey");
            var clientSecret = (await _paymentsGateway.PrepareForFuturePayment(customer.Id)).IntentSecret;
            return Ok(customer);
        }
    }
}
