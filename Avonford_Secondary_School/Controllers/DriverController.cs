
using Avonford_Secondary_School.Models;
using Avonford_Secondary_School.Models.ViewModels;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Controllers
{
    public class DriverController : Controller
    {
        private readonly HighschoolDbEntities2 db = new HighschoolDbEntities2();

        
        private int GetDriverIdOrPickFirst(int? driverIdParam)
        {
            if (driverIdParam.HasValue && driverIdParam.Value > 0)
                Session["DriverID"] = driverIdParam.Value;

            if (Session["DriverID"] != null)
                return Convert.ToInt32(Session["DriverID"]);

            var first = db.Drivers.OrderBy(d => d.DriverID).Select(d => d.DriverID).FirstOrDefault();
            Session["DriverID"] = first;
            return first;
        }

        // GET: /Driver?driverId=#
        [HttpGet]
        public ActionResult Index(int? driverId)
        {
            var id = GetDriverIdOrPickFirst(driverId);
            var d = db.Drivers.FirstOrDefault(x => x.DriverID == id);
            if (d == null) return HttpNotFound();

           
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);

            var active = db.BookOrders
                .Where(o => o.DriverID == id && o.Status == "OutForDelivery")
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new DriverOrderItemVM
                {
                    OrderID = o.OrderID,
                    BookTitle = o.BooksForSale.Title,
                    BuyerEmail = o.User.Email,
                    PickupAddress = o.BooksForSale.PickupAddress,
                    DeliveryAddress = o.DeliveryAddress,
                    DeliveryType = o.DeliveryType,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                    PaidAt = o.PaidAt
                })
                .ToList();

            var inprog = db.BookOrders
                .Where(o => o.DriverID == id && o.Status == "DeliveryUnderway")
                .OrderByDescending(o => o.UpdatedAt)
                .Select(o => new DriverOrderItemVM
                {
                    OrderID = o.OrderID,
                    BookTitle = o.BooksForSale.Title,
                    BuyerEmail = o.User.Email,
                    PickupAddress = o.BooksForSale.PickupAddress,
                    DeliveryAddress = o.DeliveryAddress,
                    DeliveryType = o.DeliveryType,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                    PaidAt = o.PaidAt
                })
                .ToList();

            var completed = db.BookOrders
                .Where(o => o.DriverID == id
                            && o.Status == "Delivered"
                            && o.DeliveredAt >= thirtyDaysAgo)   // ✅ uses captured value, EF can translate
                .OrderByDescending(o => o.DeliveredAt)
                .Select(o => new DriverOrderItemVM
                {
                    OrderID = o.OrderID,
                    BookTitle = o.BooksForSale.Title,
                    BuyerEmail = o.User.Email,
                    PickupAddress = o.BooksForSale.PickupAddress,
                    DeliveryAddress = o.DeliveryAddress,
                    DeliveryType = o.DeliveryType,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                    PaidAt = o.PaidAt
                })
                .ToList();

            var vm = new DriverDashboardVM
            {
                DriverID = d.DriverID,
                DriverName = d.Name,
                DriverStatus = d.Status,
                ActiveOrders = active,
                InProgressOrders = inprog,
                CompletedOrders = completed
            };

            ViewBag.Welcome = $"Welcome, {d.Name}";
            return View(vm);
        }

        // POST: /Driver/ToggleAvailability
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleAvailability(int driverId, bool online)
        {
            var d = db.Drivers.FirstOrDefault(x => x.DriverID == driverId);
            if (d == null) return HttpNotFound();

            // If online -> Available (unless already delivering)
            if (online)
            {
                bool hasActive = db.BookOrders.Any(o =>
                    o.DriverID == driverId &&
                    (o.Status == "OutForDelivery" || o.Status == "DeliveryUnderway"));

                d.Status = hasActive ? "OnDelivery" : "Available";
            }
            else
            {
                d.Status = "Offline";
            }

            db.DriverAvailabilityLogs.Add(new DriverAvailabilityLog
            {
                DriverID = driverId,
                LogDate = DateTime.Now.Date,
                Status = d.Status,
                UpdatedAt = DateTime.Now
            });

            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // POST: /Driver/BeginTrip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BeginTrip(int orderId)
        {
            var o = db.BookOrders.FirstOrDefault(x => x.OrderID == orderId);
            if (o == null) return HttpNotFound();
            if (o.Status != "OutForDelivery") return RedirectToAction("Index");

            var d = db.Drivers.FirstOrDefault(x => x.DriverID == o.DriverID);
            if (d == null) return RedirectToAction("Index");

            o.Status = "DeliveryUnderway";
            o.CurrentStep = "OnTheWay";
            o.UpdatedAt = DateTime.Now;

            d.Status = "OnDelivery";
            db.SaveChanges();

            return RedirectToAction("Index");
        }

        // POST: /Driver/ContinueTrip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ContinueTrip(int orderId)
        {
            // For now this just ensures the order stays "DeliveryUnderway".
            var o = db.BookOrders.FirstOrDefault(x => x.OrderID == orderId);
            if (o == null) return HttpNotFound();

            if (o.Status == "OutForDelivery")
            {
                o.Status = "DeliveryUnderway";
                o.CurrentStep = "OnTheWay";
                o.UpdatedAt = DateTime.Now;
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }


        [HttpGet]
        public ActionResult Route(int orderId)
        {
            // minimal load for the page
            var o = db.BookOrders.FirstOrDefault(x => x.OrderID == orderId);
            if (o == null) return HttpNotFound();

            var d = db.Drivers.FirstOrDefault(x => x.DriverID == o.DriverID);
            if (d == null) return HttpNotFound();

            var vm = new DriverRouteVM
            {
                DriverID = d.DriverID,
                DriverName = d.Name,
                OrderID = o.OrderID,
                BookTitle = o.BooksForSale.Title,
                PickupAddress = o.BooksForSale.PickupAddress,
                DeliveryAddress = o.DeliveryAddress,
                DeliveryType = o.DeliveryType,
                BuyerEmail = o.User.Email
            };

            ViewBag.GoogleMapsApiKey = "AIzaSyCzGOGXloVFr8w-Pe53rgPWuQv-P3KnIaE";
            return View(vm);
        }

        [HttpPost]
        public JsonResult PostLocation(int driverId, decimal lat, decimal lng)
        {
            var d = db.Drivers.FirstOrDefault(x => x.DriverID == driverId);
            if (d == null) return Json(new { ok = false, error = "driver_not_found" });

            d.LastKnownLatitude = lat;
            d.LastKnownLongitude = lng;
            db.DriverLocationLogs.Add(new DriverLocationLog
            {
                DriverID = driverId,
                Latitude = lat,
                Longitude = lng,
                Timestamp = DateTime.Now
            });
            db.SaveChanges();
            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkDelivered(int orderId)
        {
            var o = db.BookOrders.FirstOrDefault(x => x.OrderID == orderId);
            if (o == null) return HttpNotFound();

            var d = db.Drivers.FirstOrDefault(x => x.DriverID == o.DriverID);

            o.Status = "Delivered";
            o.CurrentStep = "Delivered";
            o.DeliveredAt = DateTime.Now;
            o.UpdatedAt = DateTime.Now;
            if (d != null) d.Status = "Available";

            // notify buyer
            db.Notifications.Add(new Notification
            {
                UserID = o.BuyerID,
                Title = "Your order has been delivered",
                Message = $"Order #{o.OrderID} for \"{o.BooksForSale.Title}\" is complete.",
                CreatedDate = DateTime.Now,
                IsRead = false,
                OrderID = o.OrderID
            });

            db.SaveChanges();
            return RedirectToAction("Index");
        }

    }
}
