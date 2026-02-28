// ========================= NAMESPACE: Avonford_Secondary_School.Models.ViewModels =========================
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    // ---------- Explore / Library ----------
    public class LibraryFilterVM
    {
        [Display(Name = "Search")]
        [StringLength(120, ErrorMessage = "Keep it snappy — {1} chars max.")]
        public string Query { get; set; }

        [Display(Name = "Min Price (R)")]
        [Range(0, 999999, ErrorMessage = "Minimum price must be a real number ≥ 0.")]
        public decimal? MinPrice { get; set; }

        [Display(Name = "Max Price (R)")]
        [Range(0, 999999, ErrorMessage = "Maximum price must be a real number ≥ 0.")]
        public decimal? MaxPrice { get; set; }

        [Display(Name = "Condition")]
        public string Condition { get; set; } // New, Like New, Good, Fair, Poor (optional)

        [Display(Name = "Sort by")]
        public string SortBy { get; set; } = "Newest"; // Newest | PriceLowHigh | PriceHighLow

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;

        // Helpers for dropdowns in the view
        public IEnumerable<SelectListItem> ConditionOptions { get; set; }
        public IEnumerable<SelectListItem> SortOptions { get; set; }
    }

    public class BookCardVM
    {
        public int BookID { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Condition { get; set; }
        public decimal Price { get; set; }
        public string MainImageBase64 { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsNegotiable { get; set; }
    }

    public class LibraryIndexVM
    {
        public LibraryFilterVM Filters { get; set; }
        public List<BookCardVM> Results { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool CanSell { get; set; }
    }

    // ---------- Details ----------
    public class BookDetailsVM
    {
        public int BookID { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Edition { get; set; }
        public string ISBN { get; set; }
        public string Condition { get; set; }
        public decimal Price { get; set; }
        public bool IsNegotiable { get; set; }
        public string DescriptionHtml { get; set; }
        public string PickupAddress { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string SellerEmail { get; set; }
        public List<string> ImageBase64 { get; set; } = new List<string>();
        public bool CanBuy { get; set; }
    }

    // ---------- Checkout ----------
    public class BookCheckoutVM : IValidatableObject
    {
        [Required]
        public int BookID { get; set; }

        // Display-only helpers
        public string Title { get; set; }
        public string SellerEmail { get; set; }

        // Pricing
        public decimal Subtotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal Total => Subtotal + (DeliveryType == "Delivery" ? DeliveryFee : 0);

        // Delivery
        [Required(ErrorMessage = "Choose how you want to receive the book (Delivery or Pickup).")]
        [RegularExpression("Pickup|Delivery", ErrorMessage = "Delivery type must be 'Pickup' or 'Delivery'.")]
        public string DeliveryType { get; set; } = "Pickup";

        [Display(Name = "Delivery Address")]
        public string DeliveryAddress { get; set; }

        public decimal? DeliveryLatitude { get; set; }
        public decimal? DeliveryLongitude { get; set; }

        // Payment
        [Required(ErrorMessage = "Pick a payment method.")]
        [RegularExpression("Wallet|Card|EFT", ErrorMessage = "Payment must be Wallet, Card, or EFT.")]
        public string PaymentMethod { get; set; } = "Wallet";

        // UI helpers
        public string GoogleMapsApiKey { get; set; }
        public bool BuyerHasEnoughWallet { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DeliveryType == "Delivery")
            {
                if (string.IsNullOrWhiteSpace(DeliveryAddress))
                    yield return new ValidationResult("Please drop a valid delivery address — our drivers aren’t psychic (yet).", new[] { nameof(DeliveryAddress) });
            }
        }
    }

    public class PurchaseSuccessVM
    {
        public int OrderID { get; set; }
        public int BookID { get; set; }
        public string Title { get; set; }
        public string DeliveryType { get; set; }
        public decimal Total { get; set; }
        public DateTime PaidAt { get; set; }
    }

    // ---------- Seller Wallet (Credits) ----------
    public class WalletTxItemVM
    {
        public DateTime CreatedAt { get; set; }
        public string Direction { get; set; } // Credit / Debit
        public decimal Amount { get; set; }
        public string Reason { get; set; }
        public string Notes { get; set; }
    }

    public class SellerWalletVM
    {
        public decimal AvailableBalance { get; set; }
        public decimal PendingHoldBalance { get; set; }
        public List<WalletTxItemVM> Transactions { get; set; } = new List<WalletTxItemVM>();
    }

    public class WithdrawalReceiptVM
    {
        public decimal Amount { get; set; }
        public decimal NewBalance { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string Note { get; set; }
    }
    // ---------- Orders (buyer) ----------
    //public class MyOrderItemVM
    //{
    //    public int OrderID { get; set; }
    //    public string BookTitle { get; set; }
    //    public string Status { get; set; }         
    //    public string DeliveryType { get; set; }   
    //    public DateTime? PaidAt { get; set; }
    //    public DateTime CreatedAt { get; set; }
    //    public decimal Total { get; set; }
    //}

}
