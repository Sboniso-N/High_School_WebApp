using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    // Group all return-related VMs in one file/namespace
    public static class ReturnVMs
    {
        // === Buyer: create ===
        public class ReturnCreateVM
        {
            // Route/context
            public int OrderID { get; set; }

            // Read-only summary for the form header
            public string BookTitle { get; set; }
            public decimal BookValue { get; set; }     // order.Subtotal
            public decimal DeliveryFee { get; set; }   // order.DeliveryFee
            public DateTime? DeliveredAt { get; set; }
            public DateTime? PaidAt { get; set; }

            // Window UX
            public int ReturnWindowDays { get; set; }
            public bool WithinWindow { get; set; }
            public DateTime WindowEndsAt { get; set; }

            // Inputs
            [Required]
            public string Reason { get; set; }   // Not as described, Damaged on arrival, Wrong item received, Other

            [Required, MinLength(40, ErrorMessage = "Please provide at least 40 characters.")]
            public string Description { get; set; }

            // 0–6 images
            public List<HttpPostedFileBase> Photos { get; set; } = new List<HttpPostedFileBase>();

            // UI helpers
            public List<SelectListItem> ReasonOptions { get; set; } = new List<SelectListItem>();
        }

        // === Buyer: detail/status page ===
        public class ReturnDetailBuyerVM
        {
            public int ReturnID { get; set; }
            public int OrderID { get; set; }
            public string BookTitle { get; set; }

            public string Status { get; set; } // Pending, NeedsInfo, Approved, Declined, Refunded
            public string Reason { get; set; }
            public string Description { get; set; }

            public decimal RefundAmountBookValue { get; set; }
            public decimal RefundAmountDelivery { get; set; }
            public decimal RefundTotal => RefundAmountBookValue + RefundAmountDelivery;

            public DateTime CreatedAt { get; set; }
            public DateTime? DecisionAt { get; set; }
            public string AdminComment { get; set; }

            public List<string> ImageBase64 { get; set; } = new List<string>();
        }

        // === Admin: queue ===
        public class AdminReturnsQueueVM
        {
            public string Filter { get; set; } = "Pending"; // Pending, NeedsInfo, Approved, Declined, Refunded
            public List<AdminReturnRowVM> Items { get; set; } = new List<AdminReturnRowVM>();
        }

        public class AdminReturnRowVM
        {
            public int ReturnID { get; set; }
            public int OrderID { get; set; }
            public string BuyerEmail { get; set; }
            public string SellerEmail { get; set; }
            public string Reason { get; set; }
            public string Status { get; set; }
            public DateTime CreatedAt { get; set; }
            public int AgeDays { get; set; }
            public int PhotoCount { get; set; }
        }

        // === Admin: review page ===
        public class AdminReturnReviewVM
        {
            public int ReturnID { get; set; }
            public int OrderID { get; set; }

            public string BuyerEmail { get; set; }
            public string SellerEmail { get; set; }

            public string BookTitle { get; set; }
            public decimal BookValue { get; set; }     // order.Subtotal
            public decimal DeliveryFee { get; set; }   // order.DeliveryFee

            public string Reason { get; set; }
            public string Description { get; set; }
            public string Status { get; set; }
            public DateTime CreatedAt { get; set; }

            public List<string> ImageBase64 { get; set; } = new List<string>();

            // Decision inputs
            public bool IncludeDeliveryFee { get; set; } = false;
            public string AdminComment { get; set; }
        }

        // === Admin: decision post ===
        public class AdminReturnDecisionVM
        {
            [Required]
            public int ReturnID { get; set; }

            public bool IncludeDeliveryFee { get; set; } = false;

            [Required, MinLength(3)]
            public string Comment { get; set; }
        }


        public class BuyerReturnRowVM
        {
            public int ReturnID { get; set; }
            public int OrderID { get; set; }
            public string BookTitle { get; set; }
            public string Reason { get; set; }
            public string Status { get; set; } // Pending, NeedsInfo, Refunded, Declined
            public DateTime CreatedAt { get; set; }
            public DateTime? DecisionAt { get; set; }
            public int PhotoCount { get; set; }
            public decimal RefundBook { get; set; }
            public decimal RefundDelivery { get; set; }
            public decimal RefundTotal => RefundBook + RefundDelivery;
        }

        public class BuyerReturnListVM
        {
            public List<BuyerReturnRowVM> Items { get; set; } = new List<BuyerReturnRowVM>();
            public int TotalPending { get; set; }
            public int TotalNeedsInfo { get; set; }
            public int TotalRefunded { get; set; }
            public int TotalDeclined { get; set; }
        }
    }
}
