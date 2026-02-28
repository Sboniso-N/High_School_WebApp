using System;
using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    // Existing: MyOrderItemVM (you already have it). Add this optional helper flag if you want.
    public partial class MyOrderItemVM
    {
        public bool CanTrack =>
            string.Equals(Status, "OutForDelivery", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Status, "DeliveryUnderway", StringComparison.OrdinalIgnoreCase);
    }

    // Buyer “Track Order” details (future view)
    public class TrackOrderVM
    {
        public int OrderID { get; set; }
        public string BookTitle { get; set; }

        public string SellerNameOrEmail { get; set; }
        public string DeliveryType { get; set; }
        public string DeliveryAddress { get; set; }

        public string Status { get; set; }
        public string CurrentStep { get; set; } // Confirmed, Pickup, OnTheWay, Delivered
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? DeliveredAt { get; set; }

        // Driver block
        public int? DriverID { get; set; }
        public string DriverName { get; set; }
        public string DriverStatus { get; set; }
        public double? LastLat { get; set; }
        public double? LastLng { get; set; }
        public DateTime? LastPingAt { get; set; }

        public string EtaText { get; set; }  // placeholder until Distance Matrix is wired
    }

    // Driver dashboard items
    public class DriverOrderItemVM
    {
        public int OrderID { get; set; }
        public string BookTitle { get; set; }

        public string BuyerEmail { get; set; }
        public string PickupAddress { get; set; }
        public string DeliveryAddress { get; set; }
        public string DeliveryType { get; set; }     // Pickup / Delivery
        public string Status { get; set; }           // OutForDelivery / DeliveryUnderway / Delivered
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        public bool CanBegin => Status == "OutForDelivery";
        public bool CanContinue => Status == "DeliveryUnderway";
    }

    public class DriverDashboardVM
    {
        public int DriverID { get; set; }
        public string DriverName { get; set; }
        public string DriverStatus { get; set; } // Available / OnDelivery / Offline
        public bool IsOnline => !string.Equals(DriverStatus, "Offline", StringComparison.OrdinalIgnoreCase);

        public List<DriverOrderItemVM> ActiveOrders { get; set; } = new List<DriverOrderItemVM>();      // OutForDelivery
        public List<DriverOrderItemVM> InProgressOrders { get; set; } = new List<DriverOrderItemVM>();  // DeliveryUnderway
        public List<DriverOrderItemVM> CompletedOrders { get; set; } = new List<DriverOrderItemVM>();   // Delivered (last 30 days)
    }

    public class DriverRouteVM
    {
        public int DriverID { get; set; }
        public string DriverName { get; set; }

        public int OrderID { get; set; }
        public string BookTitle { get; set; }

        public string PickupAddress { get; set; }
        public string DeliveryAddress { get; set; }
        public string DeliveryType { get; set; }   // Pickup / Delivery
        public string BuyerEmail { get; set; }
    }

}
