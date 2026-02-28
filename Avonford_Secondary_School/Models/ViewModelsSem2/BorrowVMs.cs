using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public static class BorrowVMs
    {
        // Small helper for showing blocked intervals on the calendar
        public class IntervalVM
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
        }

        // GET form: pick start + days, see fee/day and planned total
        public class BorrowStartVM
        {
            // Context
            public int BookID { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public string Condition { get; set; }
            public decimal BookPrice { get; set; }
            public bool IsBorrowable { get; set; }   // derived from listing policy/status

            // Fees
            public decimal FeePerDay { get; set; }   // 0.03 * BookPrice (snapshot for UI)
            public int DefaultDays { get; set; } = 3;

            // Inputs
            [DataType(DataType.Date)]
            public DateTime StartDate { get; set; }  // date-only
            [Range(1, 60, ErrorMessage = "Please choose 1–60 days.")]
            public int RequestedDays { get; set; }   // N days

            // Computed for preview (UI)
            public DateTime EndDate => StartDate.Date.AddDays(Math.Max(1, RequestedDays) - 1);
            public decimal PlannedBorrowFee => Math.Round(FeePerDay * Math.Max(1, RequestedDays), 2);

            // UX helpers
            public DateTime MinStartDate { get; set; }    // usually Today
            public DateTime? NextAvailableDate { get; set; }
            public List<IntervalVM> Blocked { get; set; } = new List<IntervalVM>();
            public string PolicyNote { get; set; } = "Late returns incur 5%/day of book price.";
        }

        // POST preview → read-only review + confirm
        public class BorrowPreviewVM
        {
            public int BookID { get; set; }
            public string Title { get; set; }
            public decimal BookPrice { get; set; }
            public decimal FeePerDay { get; set; }

            [DataType(DataType.Date)]
            public DateTime StartDate { get; set; }
            [DataType(DataType.Date)]
            public DateTime EndDate { get; set; }
            public int PlannedDays { get; set; }
            public decimal PlannedBorrowFee { get; set; }

            public string PolicyNote { get; set; }
        }

        // POST confirm → create reservation
        public class BorrowConfirmVM
        {
            [Required] public int BookID { get; set; }
            [Required] public DateTime StartDate { get; set; }
            [Required, Range(1, 60)] public int RequestedDays { get; set; }

            // For idempotency checks (optional, not strictly required)
            public decimal FeePerDay { get; set; }
        }

        // Borrower list item
        public class MyBorrowingRowVM
        {
            public int ReservationID { get; set; }
            public int BookID { get; set; }
            public string Title { get; set; }
            public DateTime? StartDate { get; set; }     // <- was DateTime
            public DateTime? EndDate { get; set; }       // <- was DateTime
            public string Status { get; set; }
            public decimal? FeePerDay { get; set; }      // <- was decimal
            public decimal? PlannedBorrowFee { get; set; } // <- was decimal
            public DateTime CreatedAt { get; set; }
        }

        public class MyBorrowingsVM
        {
            public List<MyBorrowingRowVM> Items { get; set; } = new List<MyBorrowingRowVM>();
        }

        // Single reservation detail (for future view)
       

        public class BorrowDetailVM
        {
            public int ReservationID { get; set; }
            public int BookID { get; set; }
            public string Title { get; set; }
            public decimal? BookPrice { get; set; }        // <- was decimal
            public decimal? FeePerDay { get; set; }        // <- was decimal
            public DateTime? StartDate { get; set; }       // <- was DateTime
            public DateTime? EndDate { get; set; }         // <- was DateTime
            public string Status { get; set; }
            public decimal? PlannedBorrowFee { get; set; } // <- was decimal
            public DateTime CreatedAt { get; set; }
        }



        public class BorrowReturnStartVM
        {
            public int ReservationID { get; set; }
            public int BookID { get; set; }
            public string Title { get; set; }

            public DateTime StartAt { get; set; }
            public DateTime? DueAt { get; set; }

            public int EstUsageDays { get; set; }
            public int EstLateDays { get; set; }
            public decimal FeePerDay { get; set; }
            public decimal LateFeePerDay { get; set; }
            public decimal EstBorrowFee { get; set; }
            public decimal EstLateFee { get; set; }
            public decimal EstTotal { get; set; }

            public string Comment { get; set; }
            public List<HttpPostedFileBase> Photos { get; set; } = new List<HttpPostedFileBase>();
        }

        // Borrower sees the final (approved) invoice and chooses how to pay
        public class BorrowInvoiceVM
        {
            public int ReservationID { get; set; }
            public int BookID { get; set; }
            public string Title { get; set; }
            public string Status { get; set; }

            public int UsageDays { get; set; }
            public int LateDays { get; set; }
            public decimal BorrowFeeFinal { get; set; }
            public decimal LateFeeFinal { get; set; }
            public decimal DamageFee { get; set; }
            public decimal TotalDue { get; set; }

            public string PaymentMethod { get; set; } = "Wallet";
            public bool BuyerHasEnoughWallet { get; set; }
        }

        // Simple receipt after payment
        public class BorrowReceiptVM
        {
            public int ReservationID { get; set; }
            public int BookID { get; set; }
            public string Title { get; set; }
            public decimal TotalPaid { get; set; }
            public DateTime PaidAt { get; set; }
        }

        // ==== ADMIN VMs (UC7) ====

        // List row for queue
        public class AdminBorrowQueueRowVM
        {
            public int ReservationID { get; set; }
            public int BookID { get; set; }
            public string Title { get; set; }
            public int BorrowerUserID { get; set; }
            public string BorrowerEmail { get; set; }
            public DateTime StartAt { get; set; }
            public DateTime? DueAt { get; set; }
            public DateTime? ReturnAt { get; set; }

            // quick glance numbers
            public int EstUsageDays { get; set; }
            public int EstLateDays { get; set; }
            public decimal FeePerDay { get; set; }
            public decimal LateFeePerDay { get; set; }
            public string Status { get; set; }
        }

        public class AdminBorrowQueueVM
        {
            public List<AdminBorrowQueueRowVM> Items { get; set; } = new List<AdminBorrowQueueRowVM>();
            public int TotalPending { get; set; }
        }

        // Review/decision page VM
        public class AdminBorrowReviewVM
        {
            public int ReservationID { get; set; }
            public int BookID { get; set; }
            public string Title { get; set; }

            public int BorrowerUserID { get; set; }
            public string BorrowerEmail { get; set; }

            public DateTime StartAt { get; set; }
            public DateTime? DueAt { get; set; }
            public DateTime? ReturnAt { get; set; }

            public int UsageDays { get; set; }
            public int LateDays { get; set; }
            public decimal FeePerDay { get; set; }
            public decimal LateFeePerDay { get; set; }

            public decimal BorrowFeeFinal { get; set; }
            public decimal LateFeeFinal { get; set; }
            public decimal DamageFee { get; set; }
            public decimal TotalDue { get; set; }

            public string AdminNote { get; set; }
        }

    }
}
