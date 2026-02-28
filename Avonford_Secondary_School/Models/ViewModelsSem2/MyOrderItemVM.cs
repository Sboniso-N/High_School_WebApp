using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public partial class MyOrderItemVM
    {
        public int OrderID { get; set; }
        public string Status { get; set; }
        public string DeliveryType { get; set; }
        public decimal Total { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string BookTitle { get; set; }
    }
}
