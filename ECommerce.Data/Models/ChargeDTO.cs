using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Data.Models
{
    public class ChargeDTO
    {
        public string CustomerEmail { get; set; }
        public string PaymentMethodId { get; set; }
        public decimal Amount { get; set; }
        public Currency Currency { get; set; }
    }
}
