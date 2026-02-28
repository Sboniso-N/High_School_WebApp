using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    // --- Driver list / filters ---
    public class DriverListFilterVM
    {
        public string Status { get; set; } = "";  // "", Available, OnDelivery, Offline
        public string Q { get; set; } = "";       // search by name/phone/email

        public List<DriverListItemVM> Drivers { get; set; } = new List<DriverListItemVM>();

        public int Total { get; set; }
        public int AvailableCount { get; set; }
        public int OnDeliveryCount { get; set; }
        public int OfflineCount { get; set; }
    }

    public class DriverListItemVM
    {
        public int DriverID { get; set; }
        public string Name { get; set; }
        public string VehicleType { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Status { get; set; }

        public int MaxDailyDeliveries { get; set; }
        public int DeliveriesToday { get; set; }
        public int CapacityLeft { get; set; }

        public string PhotoBase64 { get; set; }
        public decimal? LastKnownLatitude { get; set; }
        public decimal? LastKnownLongitude { get; set; }
    }

    // --- Create/Edit Driver ---
    public class DriverFormVM
    {
        public int? DriverID { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; }

        [StringLength(50)]
        public string VehicleType { get; set; }   // Bike, Car, Van ...

        [Required, StringLength(50)]
        public string Phone { get; set; }

        [StringLength(255), EmailAddress]
        public string Email { get; set; }

        [Required, StringLength(100)]
        public string LicenseNo { get; set; }

        [Required]
        public DateTime LicenseExpiry { get; set; }

        [Range(1, 50)]
        public int MaxDailyDeliveries { get; set; } = 5;

        public bool DefaultAvailable { get; set; } = true;

        public HttpPostedFileBase Photo { get; set; }
    }



    // --- Orders awaiting assignment ---
    public class OrdersAwaitingDriverVM
    {
        public List<OrdersAwaitingDriverItemVM> Orders { get; set; } = new List<OrdersAwaitingDriverItemVM>();
        public int Total { get; set; }
    }

    public class OrdersAwaitingDriverItemVM
    {
        public int OrderID { get; set; }
        public int BookID { get; set; }
        public string BookTitle { get; set; }
        public string Condition { get; set; }
        public string BuyerName { get; set; }
        public string BuyerEmail { get; set; }
        public string DeliveryAddress { get; set; }
        public decimal DeliveryFee { get; set; }
        public DateTime CreatedAt { get; set; }

        public string MainImageBase64 { get; set; }
    }

    // --- Assign modal VM ---
    public class AssignDriverVM
    {
        public int OrderID { get; set; }
        public int BookID { get; set; }
        public string BookTitle { get; set; }
        public string Condition { get; set; }
        public string BuyerName { get; set; }
        public string BuyerEmail { get; set; }
        public string DeliveryAddress { get; set; }
        public decimal DeliveryFee { get; set; }
        public DateTime OrderDate { get; set; }
        public string MainImageBase64 { get; set; }

        public List<AssignDriverDriverItemVM> Drivers { get; set; } = new List<AssignDriverDriverItemVM>();
    }

    public class AssignDriverDriverItemVM
    {
        public int DriverID { get; set; }
        public string Name { get; set; }
        public string PhotoBase64 { get; set; }
        public string Status { get; set; }

        public int MaxDailyDeliveries { get; set; }
        public int DeliveriesToday { get; set; }
        public int CapacityLeft { get; set; }

        public decimal? LastKnownLatitude { get; set; }
        public decimal? LastKnownLongitude { get; set; }

        public bool IsDisabled { get; set; }
        public string DisabledReason { get; set; }
    }

    public class AssignDriverPostVM
    {
        [Required] public int OrderID { get; set; }
        [Required] public int DriverID { get; set; }

        [Display(Name = "Expected delivery date")]
        public DateTime? ExpectedDeliveryDate { get; set; }

        [StringLength(300)]
        public string Notes { get; set; }
    }

  
    

}
