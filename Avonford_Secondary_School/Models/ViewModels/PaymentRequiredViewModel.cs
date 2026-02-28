using System.ComponentModel.DataAnnotations;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class PaymentRequiredViewModel
    {
        public decimal FeeAmount { get; set; }         // e.g., 38000
        public bool TermsAccepted { get; set; }        // must be checked to proceed
        public string ErrorMessage { get; set; }       // if payment fails, we show error
    }
}
