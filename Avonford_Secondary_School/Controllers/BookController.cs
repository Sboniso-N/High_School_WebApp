// ========================= NAMESPACE: Avonford_Secondary_School.Controllers =========================
using Avonford_Secondary_School.Models;
using Avonford_Secondary_School.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using Stripe;
using Stripe.Checkout;
using System.Globalization;
using System.Data.Entity;
using static Avonford_Secondary_School.Models.ViewModels.ReturnVMs;
using static Avonford_Secondary_School.Models.ViewModels.BorrowVMs;





namespace Avonford_Secondary_School.Controllers
{
    public class BookController : Controller
    {
        private readonly HighschoolDbEntities2 db = new HighschoolDbEntities2();

        // Only used for Google Places autocomplete in forms
        private const string GOOGLE_MAPS_API_KEY = "AIzaSyCzGOGXloVFr8w-Pe53rgPWuQv-P3KnIaE";

        private const decimal LENDER_SHARE = 0.80m;           // 80% of borrow fee to lender
        private const decimal LATE_FEE_SHARE_TO_LENDER = 1.00m;
        // ========================= SELL LISTING =========================

        // GET: /Book/Sell
        [HttpGet]
        public ActionResult Sell()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            var vm = new SellBookViewModel
            {
                GoogleMapsApiKey = GOOGLE_MAPS_API_KEY,
                ExtraImages = new List<HttpPostedFileBase>()
            };
            return View(vm);
        }

        // POST: /Book/Sell   (Directly creates the listing now)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)] // allow HTML in Description; we sanitize below
        public ActionResult Sell(SellBookViewModel model)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            model.GoogleMapsApiKey = GOOGLE_MAPS_API_KEY;

            // Server-side validations you already had
            ValidateConditionValue(model.Condition);
            ValidateImagesServerSide(model);

            // Sanitize + enforce 300 chars on clean text
            var sanitized = SanitizeDescription(model.Description);
            var plainText = StripAllTags(sanitized);
            if (plainText.Length < 300)
                ModelState.AddModelError("Description", "We need a richer story—write at least 300 characters (after removing formatting).");

            // Relax ISBN + Price attribute validations
            ModelState.Remove("ISBN");
            ModelState.Remove("Price");

            // Parse price allowing dot/comma
            var priceRaw = (Request.Form["Price"] ?? "").Trim();
            decimal parsedPrice;
            bool parsed =
                decimal.TryParse(priceRaw, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out parsedPrice)
             || decimal.TryParse(priceRaw, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out parsedPrice)
             || decimal.TryParse(priceRaw.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out parsedPrice)
             || decimal.TryParse(priceRaw.Replace(".", ","), NumberStyles.Number, new CultureInfo("fr-FR"), out parsedPrice);

            if (!parsed || parsedPrice <= 0m)
                ModelState.AddModelError("Price", "Enter a valid amount (e.g., 400.00 or 400,00).");
            else
                model.Price = Math.Round(parsedPrice, 2);

            if (!ModelState.IsValid) return View(model);

            // Optional duplicate guard for same seller + same ISBN
            int sellerUserId = GetUserId();
            bool duplicate = db.BooksForSales.Any(b =>
                b.SellerID == sellerUserId &&
                b.ISBN == (model.ISBN ?? "") &&
                (b.Status == "Pending" || b.Status == "Active"));
            if (duplicate)
            {
                ModelState.AddModelError("ISBN", "Looks like you’ve already listed this ISBN. Try editing your existing listing.");
                return View(model);
            }

            // Create listing immediately
            var book = new BooksForSale
            {
                SellerID = sellerUserId,
                Title = model.Title?.Trim(),
                Author = model.Author?.Trim(),
                Edition = model.Edition?.Trim(),
                ISBN = model.ISBN?.Trim(),
                Price = model.Price,
                IsNegotiable = model.IsNegotiable,
                Condition = model.Condition,
                Description = sanitized, // store sanitized HTML
                PickupAddress = model.PickupAddress?.Trim(),
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                Status = "Active", // live immediately
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            db.BooksForSales.Add(book);
            db.SaveChanges();

            // Images
            var images = new List<BookImage>();
            if (model.CoverImage != null && model.CoverImage.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    model.CoverImage.InputStream.CopyTo(ms);
                    images.Add(new BookImage
                    {
                        BookID = book.BookID,
                        FileName = Path.GetFileName(model.CoverImage.FileName),
                        MimeType = model.CoverImage.ContentType,
                        ImageBytes = ms.ToArray(),
                        IsMain = true,
                        UploadedAt = DateTime.Now
                    });
                }
            }
            foreach (var f in (model.ExtraImages ?? new List<HttpPostedFileBase>())
                                .Where(f => f != null && f.ContentLength > 0).Take(3))
            {
                using (var ms = new MemoryStream())
                {
                    f.InputStream.CopyTo(ms);
                    images.Add(new BookImage
                    {
                        BookID = book.BookID,
                        FileName = Path.GetFileName(f.FileName),
                        MimeType = f.ContentType,
                        ImageBytes = ms.ToArray(),
                        IsMain = false,
                        UploadedAt = DateTime.Now
                    });
                }
            }
            if (images.Any())
            {
                db.BookImages.AddRange(images);
                db.SaveChanges();
            }

            // Notify seller
            db.Notifications.Add(new Notification
            {
                UserID = sellerUserId,
                Title = "Listing submitted 🎉",
                Message = $"Your book \"{book.Title}\" has been listed. Listing #{book.BookID}.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });
            db.SaveChanges();

            return RedirectToAction("Success", new { id = book.BookID });
        }

        // (Kept for future: Preview/Confirm flow) -----------------------------------

        [HttpGet]
        public ActionResult Preview()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            var preview = TempData["BookPreview"] as BookPreviewViewModel;
            if (preview == null) return RedirectToAction("Sell");
            TempData.Keep("BookPreview");
            return View(preview);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Confirm()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            var preview = TempData["BookPreview"] as BookPreviewViewModel;
            if (preview == null) return RedirectToAction("Sell");

            int sellerUserId = GetUserId();
            bool duplicate = db.BooksForSales.Any(b =>
                b.SellerID == sellerUserId &&
                b.ISBN == preview.ISBN &&
                (b.Status == "Pending" || b.Status == "Active"));
            if (duplicate)
            {
                TempData["Error"] = "Duplicate detected. Your ISBN is already listed.";
                return RedirectToAction("Sell");
            }

            var book = new BooksForSale
            {
                SellerID = sellerUserId,
                Title = preview.Title,
                Author = preview.Author,
                Edition = preview.Edition,
                ISBN = preview.ISBN,
                Price = preview.Price,
                IsNegotiable = preview.IsNegotiable,
                Condition = preview.Condition,
                Description = preview.DescriptionHtmlSafe,
                PickupAddress = preview.PickupAddress,
                Latitude = preview.Latitude,
                Longitude = preview.Longitude,
                Status = "Pending",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            db.BooksForSales.Add(book);
            db.SaveChanges();

            var images = new List<BookImage>();
            if (preview.CoverImage != null)
            {
                images.Add(new BookImage
                {
                    BookID = book.BookID,
                    FileName = preview.CoverImage.FileName,
                    MimeType = preview.CoverImage.MimeType,
                    ImageBytes = preview.CoverImage.Bytes,
                    IsMain = true,
                    UploadedAt = DateTime.Now
                });
            }
            foreach (var img in preview.ExtraImages ?? new List<UploadedImageVM>())
            {
                images.Add(new BookImage
                {
                    BookID = book.BookID,
                    FileName = img.FileName,
                    MimeType = img.MimeType,
                    ImageBytes = img.Bytes,
                    IsMain = false,
                    UploadedAt = DateTime.Now
                });
            }
            if (images.Any())
            {
                db.BookImages.AddRange(images);
                db.SaveChanges();
            }

            db.Notifications.Add(new Notification
            {
                UserID = sellerUserId,
                Title = "Listing submitted 🎉",
                Message = $"Your book \"{book.Title}\" is now in review. Listing #{book.BookID}.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });
            db.SaveChanges();

            TempData.Remove("BookPreview");
            return RedirectToAction("Success", new { id = book.BookID });
        }

        // GET: /Book/Success/{id}
        [HttpGet]
        public ActionResult Success(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            ViewBag.ListingId = id;
            return View();
        }

        // GET: /Book/MyListings
        [HttpGet]
        public ActionResult MyListings()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int sellerUserId = GetUserId();

            // 1) Project to an intermediate POCO with raw bytes, IN SQL
            var raw = (from b in db.BooksForSales
                       where b.SellerID == sellerUserId
                       orderby b.CreatedAt descending
                       select new
                       {
                           b.BookID,
                           b.Title,
                           b.Condition,
                           b.Price,
                           b.Status,
                           b.CreatedAt,
                           MainImageBytes = db.BookImages
                                .Where(i => i.BookID == b.BookID && i.IsMain)
                                .Select(i => i.ImageBytes)
                                .FirstOrDefault()
                       }).ToList(); // <- materialize here

            // 2) Now convert to VMs and Base64 in memory (safe for EF)
            var listings = raw.Select(r => new MyListingItemVM
            {
                BookID = r.BookID,
                Title = r.Title,
                Condition = r.Condition,
                Price = r.Price,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                MainImageBase64 = (r.MainImageBytes != null)
                    ? Convert.ToBase64String(r.MainImageBytes)
                    : null
            }).ToList();

            return View(listings);
        }

        // ========================= BROWSE / BUY FLOW =========================

        // GET: /Book (Explore / Library)
        [HttpGet]
        public ActionResult Index(LibraryFilterVM filters)
        {
            filters = filters ?? new LibraryFilterVM();
            if (filters.Page <= 0) filters.Page = 1;
            if (filters.PageSize <= 0 || filters.PageSize > 48) filters.PageSize = 12;

            filters.ConditionOptions = new List<SelectListItem>
            {
                new SelectListItem{Text="Any condition", Value="", Selected=string.IsNullOrEmpty(filters.Condition)},
                new SelectListItem{Text="New", Value="New", Selected=filters.Condition=="New"},
                new SelectListItem{Text="Like New", Value="Like New", Selected=filters.Condition=="Like New"},
                new SelectListItem{Text="Good", Value="Good", Selected=filters.Condition=="Good"},
                new SelectListItem{Text="Fair", Value="Fair", Selected=filters.Condition=="Fair"},
                new SelectListItem{Text="Poor", Value="Poor", Selected=filters.Condition=="Poor"},
            };
            filters.SortOptions = new List<SelectListItem>
            {
                new SelectListItem{Text="Newest", Value="Newest", Selected=filters.SortBy=="Newest"},
                new SelectListItem{Text="Price: Low → High", Value="PriceLowHigh", Selected=filters.SortBy=="PriceLowHigh"},
                new SelectListItem{Text="Price: High → Low", Value="PriceHighLow", Selected=filters.SortBy=="PriceHighLow"},
            };

            // Base query
            var q = db.BooksForSales.Where(b => b.Status == "Active");

            // Search
            if (!string.IsNullOrWhiteSpace(filters.Query))
            {
                var term = filters.Query.Trim();
                q = q.Where(b => b.Title.Contains(term) || b.Author.Contains(term) || b.ISBN.Contains(term));
            }

            // Condition
            if (!string.IsNullOrEmpty(filters.Condition))
                q = q.Where(b => b.Condition == filters.Condition);

            // Price range
            if (filters.MinPrice.HasValue) q = q.Where(b => b.Price >= filters.MinPrice.Value);
            if (filters.MaxPrice.HasValue) q = q.Where(b => b.Price <= filters.MaxPrice.Value);

            // Sorting
            switch (filters.SortBy)
            {
                case "PriceLowHigh": q = q.OrderBy(b => b.Price).ThenByDescending(b => b.CreatedAt); break;
                case "PriceHighLow": q = q.OrderByDescending(b => b.Price).ThenByDescending(b => b.CreatedAt); break;
                default: q = q.OrderByDescending(b => b.CreatedAt); break;
            }

            // Count + page
            var total = q.Count();

            // Fetch page + main image bytes in SQL
            var pageRows = q
                .Skip((filters.Page - 1) * filters.PageSize)
                .Take(filters.PageSize)
                .Select(b => new
                {
                    b.BookID,
                    b.Title,
                    b.Author,
                    b.Condition,
                    b.Price,
                    b.IsNegotiable,
                    b.CreatedAt,
                    MainImageBytes = db.BookImages
                        .Where(i => i.BookID == b.BookID && i.IsMain)
                        .Select(i => i.ImageBytes)
                        .FirstOrDefault()
                })
                .ToList(); // materialize

            // Map to cards in memory (Base64 here)
            var cards = pageRows.Select(r => new BookCardVM
            {
                BookID = r.BookID,
                Title = r.Title,
                Author = r.Author,
                Condition = r.Condition,
                Price = r.Price,
                IsNegotiable = r.IsNegotiable,
                CreatedAt = r.CreatedAt,
                MainImageBase64 = r.MainImageBytes != null ? Convert.ToBase64String(r.MainImageBytes) : null
            }).ToList();

            var vm = new LibraryIndexVM
            {
                Filters = filters,
                Results = cards,
                TotalCount = total,
                TotalPages = (int)Math.Ceiling(total / (double)filters.PageSize),
                CanSell = IsLoggedIn()
            };

            return View(vm);
        }

        // GET: /Book/Details/{id}
        [HttpGet]
        public ActionResult Details(int id)
        {
            var b = db.BooksForSales.FirstOrDefault(x => x.BookID == id);
            if (b == null) return HttpNotFound();

            var seller = db.Users.FirstOrDefault(u => u.UserID == b.SellerID);
            var imgs = db.BookImages.Where(i => i.BookID == id)
                                    .OrderByDescending(i => i.IsMain)
                                    .ToList(); // materialized, so Convert below is safe

            var vm = new BookDetailsVM
            {
                BookID = b.BookID,
                Title = b.Title,
                Author = b.Author,
                Edition = b.Edition,
                ISBN = b.ISBN,
                Condition = b.Condition,
                Price = b.Price,
                IsNegotiable = b.IsNegotiable,
                DescriptionHtml = b.Description,
                PickupAddress = b.PickupAddress,
                Latitude = b.Latitude,
                Longitude = b.Longitude,
                SellerEmail = seller?.Email,
                ImageBase64 = imgs.Select(i => Convert.ToBase64String(i.ImageBytes)).ToList(),
                CanBuy = IsLoggedIn() && b.Status == "Active" && GetUserId() != b.SellerID
            };

            return View(vm);
        }

        // GET: /Book/Checkout/{id}
        [HttpGet]
        public ActionResult Checkout(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            var b = db.BooksForSales.FirstOrDefault(x => x.BookID == id);
            if (b == null) return HttpNotFound();
            if (b.Status != "Active") return RedirectToAction("Details", new { id });

            var buyerId = GetUserId();
            if (buyerId == b.SellerID) return RedirectToAction("Details", new { id });

            var buyerWallet = db.UserWallets.FirstOrDefault(w => w.UserID == buyerId);
            var fee = GetDeliveryFee();

            var vm = new BookCheckoutVM
            {
                BookID = b.BookID,
                Title = b.Title,
                SellerEmail = db.Users.Where(u => u.UserID == b.SellerID).Select(u => u.Email).FirstOrDefault(),
                Subtotal = b.Price,
                DeliveryFee = fee,
                DeliveryType = "Pickup",
                PaymentMethod = "Wallet",
                BuyerHasEnoughWallet = (buyerWallet?.AvailableBalance ?? 0) >= b.Price,
                GoogleMapsApiKey = GOOGLE_MAPS_API_KEY
            };

            return View(vm);
        }

        // POST: /Book/Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Checkout(BookCheckoutVM model)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            // Load book fresh
            var b = db.BooksForSales.FirstOrDefault(x => x.BookID == model.BookID);
            if (b == null) return HttpNotFound();

            // Preconditions
            if (b.Status != "Active")
            {
                TempData["Error"] = "Oops — someone already bought this book.";
                return RedirectToAction("Details", new { id = b.BookID });
            }
            var buyerId = GetUserId();
            if (buyerId == b.SellerID)
            {
                TempData["Error"] = "You can’t buy your own listing.";
                return RedirectToAction("Details", new { id = b.BookID });
            }

            // Pricing refresh
            var fee = GetDeliveryFee();
            model.Subtotal = b.Price;
            model.DeliveryFee = fee;

            // Server-side delivery validation (plain address only)
            if (string.Equals(model.DeliveryType, "Delivery", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(model.DeliveryAddress))
                    ModelState.AddModelError(nameof(model.DeliveryAddress), "Please enter a delivery address.");
            }
            else
            {
                model.DeliveryAddress = null;   // ensure we only store when Delivery
            }

            // make sure view has what it needs if we return it
            model.GoogleMapsApiKey = GOOGLE_MAPS_API_KEY;

            if (!ModelState.IsValid)
                return View(model);

            // Branch by payment method
            var method = (model.PaymentMethod ?? "").Trim();

            // Create the (pending) order up-front so we have an OrderID to track regardless of method.
            var order = new BookOrder
            {
                BookID = b.BookID,
                BuyerID = buyerId,
                SellerID = b.SellerID,

                DeliveryType = model.DeliveryType,
                DeliveryAddress = model.DeliveryType == "Delivery" ? model.DeliveryAddress.Trim() : null,

                // Plain address only; DO NOT save lat/lng
                DeliveryLatitude = null,
                DeliveryLongitude = null,

                DeliveryFee = model.DeliveryType == "Delivery" ? fee : 0m,
                Subtotal = b.Price,
                Total = b.Price + (model.DeliveryType == "Delivery" ? fee : 0m),

                // ✔ Use a status allowed by CK_BookOrders_Status
                Status = method == "Card"
                            ? "AwaitingPayment"
                            : (model.DeliveryType == "Delivery" ? "AwaitingDriver" : "Paid"),
                CurrentStep = "Confirmed",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            db.BookOrders.Add(order);
            db.SaveChanges();

            if (method == "Card")
            {
                // Card path: DO NOT mark book sold yet; send to Stripe
                return RedirectToAction("StartCardPayment", new { id = order.OrderID });
            }

            if (method == "Wallet")
            {
                // Wallet path: debit, mark sold, credit seller, notify, success

                var buyerWallet = EnsureWallet(buyerId);
                var grandTotal = order.Total;

                if (buyerWallet.AvailableBalance < grandTotal)
                {
                    ModelState.AddModelError("PaymentMethod", "Not enough wallet balance. Top up or pick Card.");
                    // Keep the same VM values for redisplay
                    model.BuyerHasEnoughWallet = false;
                    return View(model);
                }

                // Debit buyer
                var before = buyerWallet.AvailableBalance;
                buyerWallet.AvailableBalance = before - grandTotal;
                buyerWallet.LastUpdated = DateTime.Now;

                db.WalletTransactions.Add(new WalletTransaction
                {
                    UserID = buyerId,
                    Amount = grandTotal,
                    Direction = "Debit",
                    Reason = "Order",
                    RefType = "Order",
                    RefID = order.OrderID,
                    CreatedAt = DateTime.Now,
                    BeforeBalance = before,
                    AfterBalance = buyerWallet.AvailableBalance,
                    Notes = $"Purchase of Book #{b.BookID}"
                });

                // Mark order paid + mark book sold
                order.Status = model.DeliveryType == "Delivery" ? "AwaitingDriver" : "Paid";
                order.PaidAt = DateTime.Now;
                order.UpdatedAt = DateTime.Now;

                b.Status = "Sold";
                b.UpdatedAt = DateTime.Now;

                // Credit seller 80% of BOOK price (exclude delivery)
                var sellerWallet = EnsureWallet(b.SellerID);
                var payout = Math.Round(b.Price * 0.80m, 2, MidpointRounding.AwayFromZero);
                var sellerBefore = sellerWallet.AvailableBalance;
                sellerWallet.AvailableBalance = sellerBefore + payout;
                sellerWallet.LastUpdated = DateTime.Now;

                db.WalletTransactions.Add(new WalletTransaction
                {
                    UserID = b.SellerID,
                    Amount = payout,
                    Direction = "Credit",
                    Reason = "SalePayout",
                    RefType = "Order",
                    RefID = order.OrderID,
                    CreatedAt = DateTime.Now,
                    BeforeBalance = sellerBefore,
                    AfterBalance = sellerWallet.AvailableBalance,
                    Notes = $"80% payout for Book #{b.BookID}"
                });

                // Notifications
                db.Notifications.Add(new Notification
                {
                    UserID = buyerId,
                    Title = "Order confirmed 🎉",
                    Message = $"You bought \"{b.Title}\". Order #{order.OrderID}.",
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    OrderID = order.OrderID
                });
                db.Notifications.Add(new Notification
                {
                    UserID = b.SellerID,
                    Title = "Cha-ching! Your book sold",
                    Message = $"\"{b.Title}\" has been purchased. You’ve been credited R{payout:F2}. Order #{order.OrderID}.",
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    OrderID = order.OrderID
                });

                db.SaveChanges();
                return RedirectToAction("OrderSuccess", new { id = order.OrderID });
            }

            // (Optional) EFT placeholder — for now just message:
            TempData["Error"] = "EFT is not enabled in this demo. Please choose Wallet or Card.";
            return RedirectToAction("Checkout", new { id = b.BookID });
        }


        // GET: /Book/StartCardPayment/{id}
        [HttpGet]
        public ActionResult StartCardPayment(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            var buyerId = GetUserId();
            var order = db.BookOrders.FirstOrDefault(o => o.OrderID == id && o.BuyerID == buyerId);
            if (order == null) return HttpNotFound();

            // ✔ Match DB constraint allowed value
            if (order.Status != "AwaitingPayment")
                return RedirectToAction("OrderSuccess", new { id = order.OrderID });

            var book = db.BooksForSales.FirstOrDefault(b => b.BookID == order.BookID);
            if (book == null) return HttpNotFound();

            StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
            var amountCents = (long)(order.Total * 100m);

            var successUrl = Url.Action("CardCallback", "Book", null, protocol: Request.Url.Scheme) + "?session_id={CHECKOUT_SESSION_ID}";
            var cancelUrl = Url.Action("Details", "Book", new { id = order.BookID }, protocol: Request.Url.Scheme);

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "zar",
                    UnitAmount = amountCents,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"Book: {book.Title} (Order #{order.OrderID})"
                    }
                }
            }
        },
                Metadata = new Dictionary<string, string>
        {
            { "order_id", order.OrderID.ToString() },
            { "book_id",  order.BookID.ToString()  },
            { "buyer_id", order.BuyerID.ToString() },
            { "seller_id",order.SellerID.ToString()}
        }
            };

            var service = new SessionService();
            var session = service.Create(options);

            return Redirect(session.Url);
        }


        // GET: /Book/CardCallback
        [HttpGet]
        public ActionResult CardCallback(string session_id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            if (string.IsNullOrWhiteSpace(session_id))
            {
                TempData["Error"] = "Missing payment session.";
                return RedirectToAction("Index");
            }

            StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];

            var sService = new SessionService();
            Session session;
            try
            {
                session = sService.Get(session_id);
            }
            catch (StripeException)
            {
                TempData["Error"] = "Could not verify payment with Stripe.";
                return RedirectToAction("Index");
            }

            // Get our order id from metadata
            if (session.Metadata == null || !session.Metadata.ContainsKey("order_id"))
            {
                TempData["Error"] = "Payment missing order reference.";
                return RedirectToAction("Index");
            }

            int orderId;
            if (!int.TryParse(session.Metadata["order_id"], out orderId))
            {
                TempData["Error"] = "Invalid order reference.";
                return RedirectToAction("Index");
            }

            var order = db.BookOrders.FirstOrDefault(o => o.OrderID == orderId);
            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction("Index");
            }

            // Safety: only process once
            if (order.PaidAt.HasValue || order.Status == "Paid" || order.Status == "AwaitingDriver")
                return RedirectToAction("OrderSuccess", new { id = order.OrderID });

            if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Payment not completed.";
                return RedirectToAction("Details", new { id = order.BookID });
            }

            // Mark as paid + mark book sold
            var book = db.BooksForSales.FirstOrDefault(b => b.BookID == order.BookID);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Index");
            }

            order.PaidAt = DateTime.Now;
            order.Status = order.DeliveryType == "Delivery" ? "AwaitingDriver" : "Paid";
            order.UpdatedAt = DateTime.Now;

            book.Status = "Sold";
            book.UpdatedAt = DateTime.Now;

            // Credit seller 80% of book price (exclude delivery)
            var sellerWallet = EnsureWallet(order.SellerID);
            var payout = Math.Round(book.Price * 0.80m, 2, MidpointRounding.AwayFromZero);
            var sellerBefore = sellerWallet.AvailableBalance;
            sellerWallet.AvailableBalance = sellerBefore + payout;
            sellerWallet.LastUpdated = DateTime.Now;

            db.WalletTransactions.Add(new WalletTransaction
            {
                UserID = order.SellerID,
                Amount = payout,
                Direction = "Credit",
                Reason = "SalePayout",
                RefType = "Order",
                RefID = order.OrderID,
                CreatedAt = DateTime.Now,
                BeforeBalance = sellerBefore,
                AfterBalance = sellerWallet.AvailableBalance,
                Notes = $"80% payout for Book #{book.BookID} (card)"
            });

            // Notifications
            db.Notifications.Add(new Notification
            {
                UserID = order.BuyerID,
                Title = "Payment successful 🎉",
                Message = $"You bought \"{book.Title}\". Order #{order.OrderID}.",
                CreatedDate = DateTime.Now,
                IsRead = false,
                OrderID = order.OrderID
            });
            db.Notifications.Add(new Notification
            {
                UserID = order.SellerID,
                Title = "Cha-ching! Your book sold",
                Message = $"\"{book.Title}\" has been purchased. You’ve been credited R{payout:F2}. Order #{order.OrderID}.",
                CreatedDate = DateTime.Now,
                IsRead = false,
                OrderID = order.OrderID
            });

            db.SaveChanges();
            return RedirectToAction("OrderSuccess", new { id = order.OrderID });
        }

        // GET: /Book/OrderSuccess/{id}
        [HttpGet]
        public ActionResult OrderSuccess(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            var order = db.BookOrders.FirstOrDefault(o => o.OrderID == id);
            if (order == null) return HttpNotFound();

            var book = db.BooksForSales.FirstOrDefault(b => b.BookID == order.BookID);
            var vm = new PurchaseSuccessVM
            {
                OrderID = order.OrderID,
                BookID = order.BookID,
                Title = book?.Title,
                DeliveryType = order.DeliveryType,
                Total = order.Total,
                PaidAt = order.PaidAt ?? order.CreatedAt
            };
            return View(vm);
        }

        // GET: /Book/MyOrders  (Buyer view)
        // GET: /Book/MyOrders  (Buyer view)
        [HttpGet]
        public ActionResult MyOrders()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();
            // inside BookController -> MyOrders action (after you have userId)
            var filedReturnOrderIds = db.Returns
                .Where(r => r.BuyerUserID == userId)     // this buyer
                .Select(r => r.OrderID)
                .Distinct()
                .ToList();

            ViewBag.ReturnedOrderIds = filedReturnOrderIds; // used by the view

            var orders = db.BookOrders
                .Where(o => o.BuyerID == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new MyOrderItemVM
                {
                    OrderID = o.OrderID,
                    Status = o.Status,
                    Total = o.Total,
                    DeliveryType = o.DeliveryType,
                    PaidAt = o.PaidAt,
                    CreatedAt = o.CreatedAt,
                    BookTitle = o.BooksForSale.Title
                })
                .ToList();

            return View(orders);
        }


        // ========================= WALLET =========================

        [HttpGet]
        public ActionResult Wallet()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var w = EnsureWallet(userId);
            var transactions = db.WalletTransactions
                .Where(tx => tx.UserID == userId)
                .OrderByDescending(tx => tx.CreatedAt)
                .Take(100)
                .Select(tx => new WalletTxItemVM
                {
                    CreatedAt = tx.CreatedAt,
                    Direction = tx.Direction,
                    Amount = tx.Amount,
                    Reason = tx.Reason,
                    Notes = tx.Notes
                }).ToList();

            var vm = new SellerWalletVM
            {
                AvailableBalance = w.AvailableBalance,
                PendingHoldBalance = w.PendingHoldBalance,
                Transactions = transactions
            };
            return View(vm);
        }

        // POST: /Book/StartTopUp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StartTopUp(decimal amount)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            amount = Math.Round(amount, 2);
            if (amount <= 0m)
            {
                TempData["WalletError"] = "Please enter a valid positive amount.";
                return RedirectToAction("Wallet");
            }
            if (amount < 10m)
            {
                TempData["WalletError"] = "Minimum top-up is R10.";
                return RedirectToAction("Wallet");
            }

            StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];

            var amountCents = (long)(amount * 100m);
            var userId = GetUserId();

            var successUrl = Url.Action("TopUpCallback", "Book", null, protocol: Request.Url.Scheme) + "?session_id={CHECKOUT_SESSION_ID}";
            var cancelUrl = Url.Action("Wallet", "Book", null, protocol: Request.Url.Scheme);

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "zar",
                            UnitAmount = amountCents,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Wallet top-up (User #{userId})"
                            }
                        }
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    { "user_id", userId.ToString() },
                    { "topup_amount", amount.ToString("0.00") }
                }
            };

            var service = new SessionService();
            var session = service.Create(options);

            return Redirect(session.Url);
        }

        // GET: /Book/TopUpCallback
        [HttpGet]
        public ActionResult TopUpCallback(string session_id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(session_id))
            {
                TempData["WalletError"] = "Missing payment session. If you were charged, contact support.";
                return RedirectToAction("Wallet");
            }

            StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
            var sService = new SessionService();
            Stripe.Checkout.Session session;

            try
            {
                session = sService.Get(session_id);
            }
            catch (StripeException)
            {
                TempData["WalletError"] = "Could not verify payment with Stripe.";
                return RedirectToAction("Wallet");
            }

            string sessionNote = "Stripe:" + session.Id;
            bool alreadyRecorded = db.WalletTransactions.Any(t =>
                t.Direction == "Credit" &&
                t.Reason == "WalletTopUp" &&
                t.Notes == sessionNote);
            if (alreadyRecorded)
            {
                TempData["WalletToast"] = "Payment already processed. Your wallet is up to date.";
                return RedirectToAction("Wallet");
            }

            if (string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            {
                decimal amount = 0m;
                try
                {
                    if (session.AmountTotal.HasValue)
                        amount = (decimal)session.AmountTotal.Value / 100m;
                }
                catch { /* ignore */ }

                if (amount <= 0m && session.Metadata != null && session.Metadata.ContainsKey("topup_amount"))
                    decimal.TryParse(session.Metadata["topup_amount"], out amount);

                if (amount <= 0m)
                {
                    TempData["WalletError"] = "Could not determine the paid amount from Stripe.";
                    return RedirectToAction("Wallet");
                }

                int userId = GetUserId();

                var wallet = db.UserWallets.SingleOrDefault(w => w.UserID == userId);
                if (wallet == null)
                {
                    wallet = new UserWallet
                    {
                        UserID = userId,
                        AvailableBalance = 0m,
                        PendingHoldBalance = 0m,
                        LastUpdated = DateTime.Now
                    };
                    db.UserWallets.Add(wallet);
                    db.SaveChanges();
                }

                var before = wallet.AvailableBalance;
                wallet.AvailableBalance += amount;
                wallet.LastUpdated = DateTime.Now;

                db.WalletTransactions.Add(new WalletTransaction
                {
                    UserID = userId,
                    Amount = amount,
                    Direction = "Credit",
                    Reason = "WalletTopUp",
                    RefType = "StripeSession",
                    RefID = null,
                    CreatedAt = DateTime.Now,
                    BeforeBalance = before,
                    AfterBalance = wallet.AvailableBalance,
                    Notes = sessionNote
                });

                db.Notifications.Add(new Notification
                {
                    UserID = userId,
                    Title = "Wallet top-up successful",
                    Message = $"R {amount.ToString("0.00")} has been added to your wallet.",
                    CreatedDate = DateTime.Now,
                    IsRead = false
                });

                db.SaveChanges();
                TempData["WalletToast"] = $"Top-up successful (R {amount:0.00}).";
            }
            else
            {
                TempData["WalletError"] = "Payment not completed.";
            }

            return RedirectToAction("Wallet");
        }

        // ========================= HELPERS =========================

        private bool IsLoggedIn() => Session["UserID"] != null;

        private int GetUserId()
        {
            if (Session["UserID"] == null) throw new InvalidOperationException("User not logged in.");
            return Convert.ToInt32(Session["UserID"]);
        }

        private void ValidateConditionValue(string condition)
        {
            var allowed = new[] { "New", "Like New", "Good", "Fair", "Poor" };
            if (!allowed.Contains(condition ?? ""))
                ModelState.AddModelError("Condition", "Condition must be one of: New, Like New, Good, Fair, Poor.");
        }

        private void ValidateImagesServerSide(SellBookViewModel model)
        {
            if (model.CoverImage == null || model.CoverImage.ContentLength == 0)
            {
                ModelState.AddModelError("CoverImage", "A cover image is required.");
            }
            else if (!IsAllowedImage(model.CoverImage))
            {
                ModelState.AddModelError("CoverImage", "Cover image must be a JPG or PNG and under 3MB.");
            }

            var extras = (model.ExtraImages ?? new List<HttpPostedFileBase>())
                .Where(f => f != null && f.ContentLength > 0).ToList();

            if (extras.Count > 3)
                ModelState.AddModelError("ExtraImages", "Only up to 3 additional images allowed.");

            foreach (var f in extras)
            {
                if (!IsAllowedImage(f))
                    ModelState.AddModelError("ExtraImages", $"Invalid image \"{f.FileName}\". Only JPG/PNG under 3MB please.");
            }
        }

        private bool IsAllowedImage(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return false;
            if (file.ContentLength > 3 * 1024 * 1024) return false;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") return false;

            var mime = (file.ContentType ?? "").ToLowerInvariant();
            if (!mime.Contains("jpeg") && !mime.Contains("jpg") && !mime.Contains("png")) return false;

            return true;
        }

        private UploadedImageVM ToUploaded(HttpPostedFileBase file, bool isMain)
        {
            if (file == null || file.ContentLength == 0) return null;
            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                return new UploadedImageVM
                {
                    FileName = Path.GetFileName(file.FileName),
                    MimeType = file.ContentType,
                    Bytes = ms.ToArray(),
                    IsMain = isMain
                };
            }
        }

        // Simple sanitizer: remove scripts/handlers, allow small tag whitelist with no attributes
        private string SanitizeDescription(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            html = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            html = Regex.Replace(html, @"\s+on\w+\s*=\s*(['""]).*?\1", string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            html = Regex.Replace(html, @"\s(href|src)\s*=\s*(['""])\s*javascript:[^'""]*\2", string.Empty,
                RegexOptions.IgnoreCase);

            var allowed = new HashSet<string>(
                new[] { "b", "i", "u", "ul", "ol", "li", "p", "br", "strong", "em" },
                StringComparer.OrdinalIgnoreCase);

            html = Regex.Replace(html, @"</?([a-zA-Z][a-zA-Z0-9]*)\b[^>]*>", m =>
            {
                var tagName = m.Groups[1].Value;
                if (!allowed.Contains(tagName)) return string.Empty;
                return m.Value.StartsWith("</")
                    ? $"</{tagName}>"
                    : $"<{tagName}>"; // strip attributes
            }, RegexOptions.Singleline);

            return html.Trim();
        }

        private string StripAllTags(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            var noTags = Regex.Replace(html, "<.*?>", string.Empty);
            return HttpUtility.HtmlDecode(noTags).Trim();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ========================= LIBRARY HELPERS =========================

        private decimal GetDeliveryFee()
        {
            try
            {
                var feeSetting = db.AppSettings.FirstOrDefault(a => a.SettingKey == "DeliveryFeeZAR");
                if (feeSetting != null && decimal.TryParse(feeSetting.SettingValue, out decimal fee) && fee >= 0)
                    return fee;
            }
            catch { }
            return 90m;
        }

        private UserWallet EnsureWallet(int userId)
        {
            var w = db.UserWallets.FirstOrDefault(x => x.UserID == userId);
            if (w == null)
            {
                w = new UserWallet
                {
                    UserID = userId,
                    AvailableBalance = 0,
                    PendingHoldBalance = 0,
                    LastUpdated = DateTime.Now
                };
                db.UserWallets.Add(w);
                db.SaveChanges();
            }
            return w;
        }

        // ========================= UC4: Buyer tracking =========================

        [HttpGet]
        public ActionResult TrackOrder(int id)
        {
            ViewBag.GoogleMapsApiKey = GOOGLE_MAPS_API_KEY;

            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var o = db.BookOrders.FirstOrDefault(x => x.OrderID == id && x.BuyerID == userId);
            if (o == null) return HttpNotFound();

            var book = db.BooksForSales.FirstOrDefault(b => b.BookID == o.BookID);
            var sellerEmail = db.Users.Where(u => u.UserID == o.SellerID).Select(u => u.Email).FirstOrDefault();

            // last driver ping if any
            int? driverId = o.DriverID;
            double? lat = null, lng = null;
            DateTime? pingAt = null;
            string driverName = null, driverStatus = null;

            if (driverId.HasValue)
            {
                var d = db.Drivers.FirstOrDefault(x => x.DriverID == driverId.Value);
                if (d != null)
                {
                    driverName = d.Name;
                    driverStatus = d.Status;
                }
                var last = db.DriverLocationLogs
                            .Where(l => l.DriverID == driverId.Value)
                            .OrderByDescending(l => l.Timestamp)
                            .FirstOrDefault();
                if (last != null)
                {
                    lat = (double?)last.Latitude;
                    lng = (double?)last.Longitude;
                    pingAt = last.Timestamp;
                }
            }

            var vm = new Models.ViewModels.TrackOrderVM
            {
                OrderID = o.OrderID,
                BookTitle = book?.Title,
                SellerNameOrEmail = sellerEmail,
                DeliveryType = o.DeliveryType,
                DeliveryAddress = o.DeliveryAddress,
                Status = o.Status,
                CurrentStep = o.CurrentStep,
                CreatedAt = o.CreatedAt,
                PaidAt = o.PaidAt,
                DeliveredAt = o.DeliveredAt,
                DriverID = driverId,
                DriverName = driverName,
                DriverStatus = driverStatus,
                LastLat = lat,
                LastLng = lng,
                LastPingAt = pingAt,
                EtaText = null // to be filled when Distance Matrix is wired
            };

            // We'll build the full page later—this keeps the route alive.
            return View(vm); // (create later)
        }

        // Lightweight JSON endpoint for polling last driver location
        [HttpGet]
        public JsonResult GetDriverLastLocation(int orderId)
        {
            if (!IsLoggedIn()) return Json(new { ok = false, error = "auth" }, JsonRequestBehavior.AllowGet);

            int userId = GetUserId();
            var o = db.BookOrders.FirstOrDefault(x => x.OrderID == orderId && x.BuyerID == userId);
            if (o == null) return Json(new { ok = false, error = "not_found" }, JsonRequestBehavior.AllowGet);
            if (!o.DriverID.HasValue) return Json(new { ok = true, hasLocation = false }, JsonRequestBehavior.AllowGet);

            var last = db.DriverLocationLogs
                         .Where(l => l.DriverID == o.DriverID.Value)
                         .OrderByDescending(l => l.Timestamp)
                         .Select(l => new { l.Latitude, l.Longitude, l.Timestamp })
                         .FirstOrDefault();

            return Json(new
            {
                ok = true,
                hasLocation = last != null,
                lat = last?.Latitude,
                lng = last?.Longitude,
                ts = last?.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
            }, JsonRequestBehavior.AllowGet);
        }



        // GET: /Book/ReturnCreate?orderId=#
        [HttpGet]
        public ActionResult ReturnCreate(int orderId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var order = db.BookOrders.Include(o => o.BooksForSale)
                                     .FirstOrDefault(o => o.OrderID == orderId && o.BuyerID == userId);
            if (order == null) return HttpNotFound();

            // preconditions: paid
            if (!(order.Status == "Paid" || order.Status == "Delivered" || order.Status == "AwaitingDriver" || order.Status == "OutForDelivery" || order.Status == "DeliveryUnderway"))
            {
                TempData["Error"] = "This order is not eligible for return (not paid).";
                return RedirectToAction("MyOrders");
            }

            // prevent duplicate active RMA
            bool hasActive = db.Returns.Any(r => r.OrderID == orderId && r.Status != "Declined" && r.Status != "Refunded");
            if (hasActive)
            {
                TempData["Error"] = "A return request for this order already exists.";
                return RedirectToAction("ReturnDetails", new { id = db.Returns.Where(r => r.OrderID == orderId).OrderByDescending(r => r.CreatedAt).Select(r => r.ReturnID).FirstOrDefault() });
            }

            var windowDays = GetReturnWindowDays();
            var deliveredOrPaid = order.DeliveredAt ?? order.PaidAt ?? order.CreatedAt;
            var windowEnds = deliveredOrPaid.AddDays(windowDays);
            bool within = DateTime.Now <= windowEnds;

            var vm = new ReturnCreateVM
            {
                OrderID = order.OrderID,
                BookTitle = order.BooksForSale?.Title,
                BookValue = order.Subtotal,
                DeliveryFee = order.DeliveryFee,
                DeliveredAt = order.DeliveredAt,
                PaidAt = order.PaidAt,
                ReturnWindowDays = windowDays,
                WithinWindow = within,
                WindowEndsAt = windowEnds,
                ReasonOptions = new List<SelectListItem>
        {
            new SelectListItem{Text="Not as described", Value="Not as described"},
            new SelectListItem{Text="Damaged on arrival", Value="Damaged on arrival"},
            new SelectListItem{Text="Wrong item received", Value="Wrong item received"},
            new SelectListItem{Text="Other", Value="Other"}
        }
            };

            return View(vm);
        }

        // POST: /Book/ReturnCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult ReturnCreate(ReturnVMs.ReturnCreateVM model)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var order = db.BookOrders.Include(o => o.BooksForSale)
                                     .FirstOrDefault(o => o.OrderID == model.OrderID && o.BuyerID == userId);
            if (order == null) return HttpNotFound();

            var windowDays = GetReturnWindowDays();
            var deliveredOrPaid = order.DeliveredAt ?? order.PaidAt ?? order.CreatedAt;
            var windowEnds = deliveredOrPaid.AddDays(windowDays);
            bool within = DateTime.Now <= windowEnds;

            if (!within)
                ModelState.AddModelError("", "Return window has expired.");

            // duplicate check (rename lambda var so it doesn't clash)
            bool hasActive = db.Returns.Any(x => x.OrderID == model.OrderID && x.Status != "Declined" && x.Status != "Refunded");
            if (hasActive)
                ModelState.AddModelError("", "A return request for this order already exists.");

            // evidence rule for some reasons
            int photoCount = (model.Photos ?? new List<HttpPostedFileBase>()).Count(p => p != null && p.ContentLength > 0);
            if ((model.Reason == "Not as described" || model.Reason == "Damaged on arrival") && photoCount < 1)
                ModelState.AddModelError("", "Please attach at least one photo for this reason.");

            // validate photos
            if (photoCount > 6)
                ModelState.AddModelError("", "You can upload up to 6 photos.");
            foreach (var f in (model.Photos ?? new List<HttpPostedFileBase>()).Where(f => f != null && f.ContentLength > 0))
            {
                if (f.ContentLength > 5 * 1024 * 1024)
                    ModelState.AddModelError("", $"File {f.FileName} is larger than 5MB.");
                var mime = (f.ContentType ?? "").ToLowerInvariant();
                if (!(mime.Contains("jpeg") || mime.Contains("jpg") || mime.Contains("png")))
                    ModelState.AddModelError("", $"File {f.FileName} must be a JPG or PNG.");
            }

            if (!ModelState.IsValid)
            {
                // rehydrate summary fields for redisplay
                model.BookTitle = order.BooksForSale?.Title;
                model.BookValue = order.Subtotal;
                model.DeliveryFee = order.DeliveryFee;
                model.DeliveredAt = order.DeliveredAt;
                model.PaidAt = order.PaidAt;
                model.ReturnWindowDays = windowDays;
                model.WithinWindow = within;
                model.WindowEndsAt = windowEnds;
                model.ReasonOptions = GetReasonOptions(model.Reason);
                return View(model);
            }

            // create RMA (use 'rma' instead of 'r')
            var rma = new Return
            {
                OrderID = order.OrderID,
                BuyerUserID = order.BuyerID,
                SellerUserID = order.SellerID,
                Reason = model.Reason,
                Description = (model.Description ?? "").Trim(),
                Status = "Pending",
                RefundAmountBookValue = 0m,
                RefundAmountDelivery = 0m,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            db.Returns.Add(rma);
            db.SaveChanges();

            foreach (var f in (model.Photos ?? new List<HttpPostedFileBase>()).Where(f => f != null && f.ContentLength > 0).Take(6))
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    f.InputStream.CopyTo(ms);
                    db.ReturnImages.Add(new ReturnImage
                    {
                        ReturnID = rma.ReturnID,
                        FileName = System.IO.Path.GetFileName(f.FileName),
                        MimeType = f.ContentType,
                        ImageBytes = ms.ToArray(),
                        UploadedAt = DateTime.Now
                    });
                }
            }

            db.ReturnsAuditLogs.Add(new ReturnsAuditLog
            {
                ReturnID = rma.ReturnID,
                Action = "Submitted",
                PerformedBy = userId,
                PerformedByRole = "Student",
                ActionDate = DateTime.Now,
                Comment = "Return created by buyer."
            });

            db.Notifications.Add(new Notification
            {
                UserID = order.BuyerID,
                Title = "Return request submitted",
                Message = $"Your return for Order #{order.OrderID} has been created. RMA #{rma.ReturnID}.",
                CreatedDate = DateTime.Now,
                IsRead = false,
                OrderID = order.OrderID
            });
            db.Notifications.Add(new Notification
            {
                UserID = order.SellerID,
                Title = "Return requested by buyer",
                Message = $"Order #{order.OrderID} has a return request (RMA #{rma.ReturnID}).",
                CreatedDate = DateTime.Now,
                IsRead = false,
                OrderID = order.OrderID
            });

            db.SaveChanges();
            return RedirectToAction("ReturnDetails", new { id = rma.ReturnID });
        }

        private List<SelectListItem> GetReasonOptions(string selected = null)
        {
            var items = new List<SelectListItem>
    {
        new SelectListItem{Text="Not as described", Value="Not as described"},
        new SelectListItem{Text="Damaged on arrival", Value="Damaged on arrival"},
        new SelectListItem{Text="Wrong item received", Value="Wrong item received"},
        new SelectListItem{Text="Other", Value="Other"}
    };
            foreach (var it in items) it.Selected = it.Value == selected;
            return items;
        }


        // GET: /Book/ReturnDetails/{id}
        [HttpGet]
        public ActionResult ReturnDetails(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var r = db.Returns.FirstOrDefault(x => x.ReturnID == id && x.BuyerUserID == userId);
            if (r == null) return HttpNotFound();

            // load in two steps to avoid EF projection of byte[] in Select causing translation issues
            var imgBytes = db.ReturnImages.Where(i => i.ReturnID == r.ReturnID)
                                          .Select(i => i.ImageBytes)
                                          .ToList();

            var bookTitle = db.BookOrders.Where(o => o.OrderID == r.OrderID)
                                         .Select(o => o.BooksForSale.Title)
                                         .FirstOrDefault();

            var vm = new ReturnDetailBuyerVM
            {
                ReturnID = r.ReturnID,
                OrderID = r.OrderID,
                BookTitle = bookTitle,
                Status = r.Status,
                Reason = r.Reason,
                Description = r.Description,
                RefundAmountBookValue = r.RefundAmountBookValue,
                RefundAmountDelivery = r.RefundAmountDelivery,
                CreatedAt = r.CreatedAt,
                DecisionAt = r.DecisionAt,
                AdminComment = r.AdminComment,
                ImageBase64 = imgBytes.Select(b => Convert.ToBase64String(b)).ToList()
            };

            return View(vm);
        }

        // Helper
        private int GetReturnWindowDays()
        {
            try
            {
                var setting = db.AppSettings.FirstOrDefault(a => a.SettingKey == "ReturnWindowDays");
                if (setting != null && int.TryParse(setting.SettingValue, out int d) && d > 0) return d;
            }
            catch { /* ignore */ }
            return 14;
        }


        [HttpGet]
        public ActionResult MyReturns()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            // Pull rows with book title and photo count in one go
            var rows = (from r in db.Returns
                        where r.BuyerUserID == userId
                        orderby r.CreatedAt descending
                        select new
                        {
                            r.ReturnID,
                            r.OrderID,
                            r.Reason,
                            r.Status,
                            r.CreatedAt,
                            r.DecisionAt,
                            r.RefundAmountBookValue,
                            r.RefundAmountDelivery,
                            PhotoCount = db.ReturnImages.Count(i => i.ReturnID == r.ReturnID),
                            BookTitle = db.BookOrders
                                          .Where(o => o.OrderID == r.OrderID)
                                          .Select(o => o.BooksForSale.Title)
                                          .FirstOrDefault()
                        }).ToList();

            var vm = new BuyerReturnListVM
            {
                Items = rows.Select(r => new BuyerReturnRowVM
                {
                    ReturnID = r.ReturnID,
                    OrderID = r.OrderID,
                    BookTitle = r.BookTitle,
                    Reason = r.Reason,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    DecisionAt = r.DecisionAt,
                    PhotoCount = r.PhotoCount,
                    RefundBook = r.RefundAmountBookValue,
                    RefundDelivery = r.RefundAmountDelivery
                }).ToList()
            };

            vm.TotalPending = vm.Items.Count(x => x.Status == "Pending");
            vm.TotalNeedsInfo = vm.Items.Count(x => x.Status == "NeedsInfo");
            vm.TotalRefunded = vm.Items.Count(x => x.Status == "Refunded");
            vm.TotalDeclined = vm.Items.Count(x => x.Status == "Declined");

            return View(vm);
        }

        // Lightweight JSON for live updates on list/details
        // GET: /Book/ReturnStatus?id=#
        [HttpGet]
        public JsonResult ReturnStatus(int id)
        {
            if (!IsLoggedIn()) return Json(new { ok = false, error = "auth" }, JsonRequestBehavior.AllowGet);
            int userId = GetUserId();

            var r = db.Returns.FirstOrDefault(x => x.ReturnID == id && x.BuyerUserID == userId);
            if (r == null) return Json(new { ok = false, error = "not_found" }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                ok = true,
                status = r.Status,
                adminComment = r.AdminComment,
                decisionAt = r.DecisionAt?.ToString("yyyy-MM-dd HH:mm"),
                refundBook = r.RefundAmountBookValue,
                refundDelivery = r.RefundAmountDelivery,
                refundTotal = r.RefundAmountBookValue + r.RefundAmountDelivery
            }, JsonRequestBehavior.AllowGet);
        }




        // ========================= UC6: Borrow a Book =========================

        // GET: /Book/Borrow?bookId=#
        [HttpGet]
        public ActionResult Borrow(int bookId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            var book = db.BooksForSales.FirstOrDefault(b => b.BookID == bookId);
            if (book == null) return HttpNotFound();

            // For now: allow borrow only when listing is Active
            bool isBorrowable = string.Equals(book.Status, "Active", StringComparison.OrdinalIgnoreCase);
            if (!isBorrowable)
            {
                TempData["Error"] = "This book is not currently available to borrow.";
                return RedirectToAction("Details", new { id = bookId });
            }

            // 3% of price (snapshot for UI)
            var feePerDay = Math.Round(book.Price * 0.03m, 2, MidpointRounding.AwayFromZero);

            var blockingStatuses = new[] { "PendingLenderConfirm", "Reserved", "Active", "ReturnedPendingApproval", "AwaitingPayment" };
            var today = DateTime.Today;

            // Build blocked intervals from future reservations
            var futureBlocks = db.BorrowReservations
                .Where(r => r.BookID == bookId
                            && blockingStatuses.Contains(r.Status)
                            && DbFunctions.TruncateTime(r.EndAt) >= today)
                .Select(r => new { r.StartAt, r.EndAt })
                .ToList();

            var blocked = futureBlocks
                .Select(x => new BorrowVMs.IntervalVM { Start = x.StartAt.Date, End = x.EndAt.Date })
                .OrderBy(i => i.Start)
                .ToList();

            var nextAvailable = ComputeNextAvailable(today, blocked);

            var vm = new BorrowVMs.BorrowStartVM
            {
                BookID = book.BookID,
                Title = book.Title,
                Author = book.Author,
                Condition = book.Condition,
                BookPrice = book.Price,
                IsBorrowable = isBorrowable,

                FeePerDay = feePerDay,
                MinStartDate = today,
                StartDate = nextAvailable ?? today,
                RequestedDays = 3,
                Blocked = blocked,
                NextAvailableDate = nextAvailable
            };

            return View(vm); // view later
        }

        // POST: /Book/BorrowPreview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BorrowPreview(BorrowVMs.BorrowStartVM model)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            var book = db.BooksForSales.FirstOrDefault(b => b.BookID == model.BookID);
            if (book == null) return HttpNotFound();

            var start = model.StartDate.Date;
            var days = Math.Max(1, model.RequestedDays);
            var end = start.AddDays(days - 1);

            var feePerDay = Math.Round(book.Price * 0.03m, 2, MidpointRounding.AwayFromZero);

            if (HasBorrowConflict(model.BookID, start, end))
            {
                ModelState.AddModelError("", "Those dates overlap an existing reservation. Please pick different dates.");
            }

            if (!ModelState.IsValid)
            {
                var blockingStatuses = new[] { "PendingLenderConfirm", "Reserved", "Active", "ReturnedPendingApproval", "AwaitingPayment" };
                var futureBlocks = db.BorrowReservations
                    .Where(r => r.BookID == model.BookID
                                && blockingStatuses.Contains(r.Status)
                                && DbFunctions.TruncateTime(r.EndAt) >= DateTime.Today)
                    .Select(r => new { r.StartAt, r.EndAt })
                    .ToList();

                model.BookPrice = book.Price;
                model.FeePerDay = feePerDay;
                model.MinStartDate = DateTime.Today;
                model.Blocked = futureBlocks
                    .Select(x => new BorrowVMs.IntervalVM { Start = x.StartAt.Date, End = x.EndAt.Date })
                    .OrderBy(i => i.Start)
                    .ToList();
                model.NextAvailableDate = ComputeNextAvailable(DateTime.Today, model.Blocked);

                return View("Borrow", model);
            }

            var preview = new BorrowVMs.BorrowPreviewVM
            {
                BookID = book.BookID,
                Title = book.Title,
                BookPrice = book.Price,
                FeePerDay = feePerDay,
                StartDate = start,
                EndDate = end,
                PlannedDays = days,
                PlannedBorrowFee = Math.Round(feePerDay * days, 2),
                PolicyNote = "Late returns incur 5%/day of book price."
            };

            return View(preview); // view later
        }

        // POST: /Book/BorrowConfirm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BorrowConfirm(BorrowVMs.BorrowConfirmVM model)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var book = db.BooksForSales.FirstOrDefault(b => b.BookID == model.BookID);
            if (book == null) return HttpNotFound();

            bool isBorrowable = string.Equals(book.Status, "Active", StringComparison.OrdinalIgnoreCase);
            if (!isBorrowable)
            {
                TempData["Error"] = "This book is not currently available to borrow.";
                return RedirectToAction("Details", new { id = model.BookID });
            }

            var start = model.StartDate.Date;
            var days = Math.Max(1, model.RequestedDays);
            var end = start.AddDays(days - 1);

            if (HasBorrowConflict(model.BookID, start, end))
            {
                TempData["Error"] = "Those dates were just taken. Please pick different dates.";
                return RedirectToAction("Borrow", new { bookId = model.BookID });
            }

            var userHasOpenForSameBook = db.BorrowReservations.Any(r =>
                r.BookID == model.BookID
                && r.BorrowerUserID == userId
                && r.Status != "Cancelled"
                && r.Status != "Closed");

            if (userHasOpenForSameBook)
            {
                TempData["Error"] = "You already have an active or pending borrow for this book.";
                return RedirectToAction("Borrow", new { bookId = model.BookID });
            }

            var feePerDay = Math.Round(book.Price * 0.03m, 2, MidpointRounding.AwayFromZero);
            var latePerDay = Math.Round(book.Price * 0.05m, 2, MidpointRounding.AwayFromZero);
            var planned = Math.Round(feePerDay * days, 2, MidpointRounding.AwayFromZero);

            var res = new BorrowReservation
            {
                BookID = book.BookID,
                BorrowerUserID = userId,
                LenderUserID = book.SellerID,                 // lender = listing owner
                StartAt = start,
                EndAt = end,
                DueAt = end.Date.AddHours(23).AddMinutes(59), // 23:59 on End date
                FeePerDaySnapshot = feePerDay,
                LateFeePerDaySnapshot = latePerDay,
                PlannedDays = days,
                PlannedBorrowFee = planned,
                Status = "Reserved",                          // or "PendingLenderConfirm"
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            db.BorrowReservations.Add(res);
            db.SaveChanges();

            // Notifications
            db.Notifications.Add(new Notification
            {
                UserID = userId,
                Title = "Borrow reserved",
                Message = $"Reservation #{res.ReservationID} for \"{book.Title}\" from {start:yyyy-MM-dd} to {end:yyyy-MM-dd} is confirmed.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });

            db.Notifications.Add(new Notification
            {
                UserID = book.SellerID,
                Title = "Your book has been reserved",
                Message = $"\"{book.Title}\" reserved by a borrower ({start:yyyy-MM-dd} → {end:yyyy-MM-dd}).",
                CreatedDate = DateTime.Now,
                IsRead = false
            });

            db.SaveChanges();

            return RedirectToAction("BorrowDetails", new { id = res.ReservationID });
        }

        // GET: /Book/BorrowDetails/{id}
        [HttpGet]
        public ActionResult BorrowDetails(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var r = db.BorrowReservations
                      .Include(x => x.BooksForSale)
                      .FirstOrDefault(x => x.ReservationID == id && x.BorrowerUserID == userId);
            if (r == null) return HttpNotFound();

            var vm = new BorrowVMs.BorrowDetailVM
            {
                ReservationID = r.ReservationID,
                BookID = r.BookID,
                Title = r.BooksForSale?.Title,
                BookPrice = r.BooksForSale?.Price ?? 0m,
                FeePerDay = r.FeePerDaySnapshot,
                StartDate = r.StartAt,                 // VM is nullable, so no cast needed
                EndDate = r.EndAt,
                Status = r.Status,
                PlannedBorrowFee = r.PlannedBorrowFee,
                CreatedAt = r.CreatedAt
            };

            return View(vm); // view later
        }

        // GET: /Book/MyBorrowings  (list all your reservations/borrows)
        [HttpGet]
        public ActionResult MyBorrowings()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var items = db.BorrowReservations
                .Where(r => r.BorrowerUserID == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new BorrowVMs.MyBorrowingRowVM
                {
                    ReservationID = r.ReservationID,
                    BookID = r.BookID,
                    Title = r.BooksForSale.Title,
                    StartDate = r.StartAt,
                    EndDate = r.EndAt,
                    Status = r.Status,
                    FeePerDay = r.FeePerDaySnapshot,
                    PlannedBorrowFee = r.PlannedBorrowFee,
                    CreatedAt = r.CreatedAt
                })
                .ToList();

            var vm = new BorrowVMs.MyBorrowingsVM { Items = items };
            return View(vm); // view later
        }

        // GET (AJAX): /Book/CheckBorrowAvailability?bookId=1&start=2025-08-15&days=5
        [HttpGet]
        public JsonResult CheckBorrowAvailability(int bookId, DateTime start, int days)
        {
            if (days <= 0) days = 1;
            var s = start.Date;
            var e = s.AddDays(days - 1);

            var conflict = HasBorrowConflict(bookId, s, e);
            DateTime? nextAvail = null;

            if (conflict)
            {
                var blockingStatuses = new[] { "PendingLenderConfirm", "Reserved", "Active", "ReturnedPendingApproval", "AwaitingPayment" };
                var futureBlocks = db.BorrowReservations
                    .Where(r => r.BookID == bookId
                                && blockingStatuses.Contains(r.Status)
                                && DbFunctions.TruncateTime(r.EndAt) >= DateTime.Today)
                    .Select(r => new { r.StartAt, r.EndAt })
                    .ToList();

                var blocked = futureBlocks
                    .Select(x => new BorrowVMs.IntervalVM { Start = x.StartAt.Date, End = x.EndAt.Date })
                    .OrderBy(i => i.Start)
                    .ToList();

                nextAvail = ComputeNextAvailable(DateTime.Today, blocked);
            }

            var price = db.BooksForSales.Where(b => b.BookID == bookId).Select(b => b.Price).FirstOrDefault();
            var feePerDay = price > 0 ? Math.Round(price * 0.03m, 2) : 0m;

            return Json(new
            {
                ok = true,
                conflict,
                nextAvailable = nextAvail?.ToString("yyyy-MM-dd"),
                feePerDay,
                planned = Math.Round(feePerDay * Math.Max(1, days), 2)
            }, JsonRequestBehavior.AllowGet);
        }

        // ---------------------- helpers (UC6) ----------------------

        private bool HasBorrowConflict(int bookId, DateTime start, DateTime endInclusive)
        {
            var blockingStatuses = new[] { "PendingLenderConfirm", "Reserved", "Active", "ReturnedPendingApproval", "AwaitingPayment" };

            return db.BorrowReservations.Any(r =>
                r.BookID == bookId
                && blockingStatuses.Contains(r.Status)
                && DbFunctions.TruncateTime(r.EndAt) >= start
                && DbFunctions.TruncateTime(r.StartAt) <= endInclusive);
        }

        private DateTime? ComputeNextAvailable(DateTime from, List<BorrowVMs.IntervalVM> blocked)
        {
            if (blocked == null || blocked.Count == 0) return from;

            var merged = new List<BorrowVMs.IntervalVM>();
            foreach (var it in blocked.OrderBy(i => i.Start))
            {
                if (!merged.Any())
                {
                    merged.Add(new BorrowVMs.IntervalVM { Start = it.Start, End = it.End });
                    continue;
                }
                var last = merged[merged.Count - 1];
                if (it.Start <= last.End.AddDays(1))
                    last.End = (it.End > last.End) ? it.End : last.End;
                else
                    merged.Add(new BorrowVMs.IntervalVM { Start = it.Start, End = it.End });
            }

            var probe = from.Date;
            foreach (var m in merged)
            {
                if (probe < m.Start) return probe;   // found a gap
                if (probe >= m.Start && probe <= m.End) probe = m.End.AddDays(1);
            }
            return probe; // first free day after the last block
        }












        // GET: /Book/BorrowReturn/{reservationId}
        [HttpGet]
        public ActionResult BorrowReturn(int reservationId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var r = db.BorrowReservations
                      .Include(x => x.BooksForSale)
                      .FirstOrDefault(x => x.ReservationID == reservationId && x.BorrowerUserID == userId);
            if (r == null) return HttpNotFound();

            // Typically only Active can be returned; relax during dev if needed
            if (r.Status != "Active" && r.Status != "Reserved")
            {
                TempData["Error"] = "This reservation is not in a returnable state.";
                return RedirectToAction("BorrowDetails", new { id = reservationId });
            }

            var now = DateTime.Now;
            var usageDays = Math.Max(1, (int)Math.Ceiling((now - r.StartAt).TotalDays));
            var lateDays = (r.DueAt.HasValue && now > r.DueAt.Value)
                ? Math.Max(0, (int)Math.Ceiling((now - r.DueAt.Value).TotalDays))
                : 0;

            var vm = new BorrowVMs.BorrowReturnStartVM
            {
                ReservationID = r.ReservationID,
                BookID = r.BookID,
                Title = r.BooksForSale?.Title,
                StartAt = r.StartAt,
                DueAt = r.DueAt,
                EstUsageDays = usageDays,
                EstLateDays = lateDays,
                FeePerDay = r.FeePerDaySnapshot,
                LateFeePerDay = r.LateFeePerDaySnapshot,
                EstBorrowFee = Math.Round(r.FeePerDaySnapshot * usageDays, 2),
                EstLateFee = Math.Round(r.LateFeePerDaySnapshot * lateDays, 2),
            };
            vm.EstTotal = vm.EstBorrowFee + vm.EstLateFee;

            return View(vm); // we'll create later
        }

        // POST: /Book/BorrowReturn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BorrowReturn(BorrowVMs.BorrowReturnStartVM model)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var r = db.BorrowReservations
                      .Include(x => x.BooksForSale)
                      .FirstOrDefault(x => x.ReservationID == model.ReservationID && x.BorrowerUserID == userId);
            if (r == null) return HttpNotFound();

            if (r.Status != "Active" && r.Status != "Reserved")
            {
                TempData["Error"] = "This reservation is not in a returnable state.";
                return RedirectToAction("BorrowDetails", new { id = model.ReservationID });
            }

            // Mark returned (awaiting admin inspection)
            r.ReturnAt = DateTime.Now;
            r.Status = "ReturnedPendingApproval";
            r.UpdatedAt = DateTime.Now;

            // Optional: store a quick audit log
            db.BorrowAuditLogs.Add(new BorrowAuditLog
            {
                ReservationID = r.ReservationID,
                Action = "ReturnSubmitted",
                PerformedBy = userId,
                PerformedByRole = "Student",
                ActionDate = DateTime.Now,
                Comment = (model.Comment ?? "").Trim()
            });

            // Optional: save up to 4 photos
            try
            {
                foreach (var f in (model.Photos ?? new List<HttpPostedFileBase>()).Where(p => p != null && p.ContentLength > 0).Take(4))
                {
                    using (var ms = new MemoryStream())
                    {
                        f.InputStream.CopyTo(ms);
                        db.BorrowReservationPhotos.Add(new BorrowReservationPhoto
                        {
                            ReservationID = r.ReservationID,
                            FileName = Path.GetFileName(f.FileName),
                            MimeType = f.ContentType,
                            ImageBytes = ms.ToArray(),
                            UploadedAt = DateTime.Now
                        });
                    }
                }
            }
            catch { /* non-fatal */ }

            // Notify borrower and lender
            db.Notifications.Add(new Notification
            {
                UserID = userId,
                Title = "Return submitted",
                Message = $"Your return for “{r.BooksForSale?.Title}” has been submitted for inspection.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });
            db.Notifications.Add(new Notification
            {
                UserID = r.LenderUserID,
                Title = "Borrower returned your book",
                Message = $"Reservation #{r.ReservationID} is ready for inspection.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });

            db.SaveChanges();
            return RedirectToAction("BorrowDetails", new { id = r.ReservationID });
        }



        // GET: /Book/BorrowInvoice/{reservationId}
        [HttpGet]
        public ActionResult BorrowInvoice(int reservationId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var r = db.BorrowReservations
                      .Include(x => x.BooksForSale)
                      .FirstOrDefault(x => x.ReservationID == reservationId && x.BorrowerUserID == userId);
            if (r == null) return HttpNotFound();

            if (r.Status != "AwaitingPayment")
            {
                TempData["Error"] = "This reservation is not awaiting payment.";
                return RedirectToAction("BorrowDetails", new { id = reservationId });
            }

            var title = r.BooksForSale?.Title;
            var total = (r.TotalDue ?? 0m);

            var wallet = EnsureWallet(userId);
            var vm = new BorrowVMs.BorrowInvoiceVM
            {
                ReservationID = r.ReservationID,
                BookID = r.BookID,
                Title = title,
                Status = r.Status,
                UsageDays = r.ActualUsageDays ?? 0,
                LateDays = r.LateDays ?? 0,
                BorrowFeeFinal = r.BorrowFeeFinal ?? 0m,
                LateFeeFinal = r.LateFeeFinal ?? 0m,
                DamageFee = r.DamageFee ?? 0m,
                TotalDue = total,
                BuyerHasEnoughWallet = wallet.AvailableBalance >= total
            };

            return View(vm); // we'll build this view next
        }



        // POST: /Book/BorrowPayWallet/{reservationId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BorrowPayWallet(int reservationId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var r = db.BorrowReservations
                      .Include(x => x.BooksForSale)
                      .FirstOrDefault(x => x.ReservationID == reservationId && x.BorrowerUserID == userId);
            if (r == null) return HttpNotFound();

            if (r.Status != "AwaitingPayment")
            {
                TempData["Error"] = "This reservation is not awaiting payment.";
                return RedirectToAction("BorrowDetails", new { id = reservationId });
            }

            var total = r.TotalDue ?? 0m;
            if (total <= 0m)
            {
                TempData["Error"] = "Invalid invoice total.";
                return RedirectToAction("BorrowInvoice", new { reservationId });
            }

            var borrowerW = EnsureWallet(userId);
            if (borrowerW.AvailableBalance < total)
            {
                TempData["Error"] = "Not enough wallet balance. Top up or pay by card.";
                return RedirectToAction("BorrowInvoice", new { reservationId });
            }

            // 1) Debit borrower
            var before = borrowerW.AvailableBalance;
            borrowerW.AvailableBalance = before - total;
            borrowerW.LastUpdated = DateTime.Now;

            db.WalletTransactions.Add(new WalletTransaction
            {
                UserID = userId,
                Amount = total,
                Direction = "Debit",
                Reason = "BorrowFinal",
                RefType = "BorrowReservation",
                RefID = r.ReservationID,
                CreatedAt = DateTime.Now,
                BeforeBalance = before,
                AfterBalance = borrowerW.AvailableBalance,
                Notes = $"Payment for reservation #{r.ReservationID}"
            });

            // 2) Credit lender
            var lenderW = EnsureWallet(r.LenderUserID);
            var lenderPayout = Math.Round((r.BorrowFeeFinal ?? 0m) * LENDER_SHARE, 2)
                             + Math.Round((r.LateFeeFinal ?? 0m) * LATE_FEE_SHARE_TO_LENDER, 2)
                             + Math.Round((r.DamageFee ?? 0m) * 0m, 2); // damage fee policy → 0 to lender by default
            var lenderBefore = lenderW.AvailableBalance;
            lenderW.AvailableBalance = lenderBefore + lenderPayout;
            lenderW.LastUpdated = DateTime.Now;

            db.WalletTransactions.Add(new WalletTransaction
            {
                UserID = r.LenderUserID,
                Amount = lenderPayout,
                Direction = "Credit",
                Reason = "LenderPayout",
                RefType = "BorrowReservation",
                RefID = r.ReservationID,
                CreatedAt = DateTime.Now,
                BeforeBalance = lenderBefore,
                AfterBalance = lenderW.AvailableBalance,
                Notes = $"Borrow payout for reservation #{r.ReservationID}"
            });

            // 3) Close reservation
            r.Status = "Closed";
            r.UpdatedAt = DateTime.Now;

            // Notify
            db.Notifications.Add(new Notification
            {
                UserID = userId,
                Title = "Payment successful",
                Message = $"Final payment for “{r.BooksForSale?.Title}” completed. Thank you!",
                CreatedDate = DateTime.Now,
                IsRead = false
            });
            db.Notifications.Add(new Notification
            {
                UserID = r.LenderUserID,
                Title = "Payout received",
                Message = $"You’ve been credited R{lenderPayout:0.00} for reservation #{r.ReservationID}.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });

            db.SaveChanges();

            var receipt = new BorrowVMs.BorrowReceiptVM
            {
                ReservationID = r.ReservationID,
                BookID = r.BookID,
                Title = r.BooksForSale?.Title,
                TotalPaid = total,
                PaidAt = DateTime.Now
            };
            return View("BorrowPaySuccess", receipt); // simple receipt view later
        }
        // GET: /Book/StartBorrowCardPayment/{reservationId}
        [HttpGet]
        public ActionResult StartBorrowCardPayment(int reservationId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            int userId = GetUserId();

            var r = db.BorrowReservations
                      .Include(x => x.BooksForSale)
                      .FirstOrDefault(x => x.ReservationID == reservationId && x.BorrowerUserID == userId);
            if (r == null) return HttpNotFound();
            if (r.Status != "AwaitingPayment") return RedirectToAction("BorrowDetails", new { id = reservationId });

            var total = r.TotalDue ?? 0m;
            if (total <= 0m)
            {
                TempData["Error"] = "Invalid invoice total.";
                return RedirectToAction("BorrowInvoice", new { reservationId });
            }

            StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
            var amountCents = (long)(total * 100m);

            var successUrl = Url.Action("BorrowCardCallback", "Book", null, protocol: Request.Url.Scheme) + "?session_id={CHECKOUT_SESSION_ID}";
            var cancelUrl = Url.Action("BorrowInvoice", "Book", new { reservationId }, protocol: Request.Url.Scheme);

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "zar",
                    UnitAmount = amountCents,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"Borrow Final — {r.BooksForSale?.Title} (Res #{r.ReservationID})"
                    }
                }
            }
        },
                Metadata = new Dictionary<string, string>
        {
            { "reservation_id", r.ReservationID.ToString() },
            { "borrower_id", r.BorrowerUserID.ToString() },
            { "lender_id", r.LenderUserID.ToString() }
        }
            };

            var service = new SessionService();
            var session = service.Create(options);
            return Redirect(session.Url);
        }

        // GET: /Book/BorrowCardCallback
        [HttpGet]
        public ActionResult BorrowCardCallback(string session_id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            if (string.IsNullOrWhiteSpace(session_id)) return RedirectToAction("MyBorrowings");

            StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
            var sService = new SessionService();
            Session session;

            try { session = sService.Get(session_id); }
            catch (StripeException)
            {
                TempData["Error"] = "Could not verify payment with Stripe.";
                return RedirectToAction("MyBorrowings");
            }

            if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Payment not completed.";
                return RedirectToAction("MyBorrowings");
            }

            if (session.Metadata == null || !session.Metadata.ContainsKey("reservation_id"))
                return RedirectToAction("MyBorrowings");

            if (!int.TryParse(session.Metadata["reservation_id"], out int reservationId))
                return RedirectToAction("MyBorrowings");

            int userId = GetUserId();

            var r = db.BorrowReservations
                      .Include(x => x.BooksForSale)
                      .FirstOrDefault(x => x.ReservationID == reservationId && x.BorrowerUserID == userId);
            if (r == null) return RedirectToAction("MyBorrowings");

            if (r.Status != "AwaitingPayment")
                return RedirectToAction("BorrowDetails", new { id = reservationId });

            var total = r.TotalDue ?? 0m;

            // Credit lender wallet (card funds go to platform → we reflect as in-app credit)
            var lenderW = EnsureWallet(r.LenderUserID);
            var lenderPayout = Math.Round((r.BorrowFeeFinal ?? 0m) * LENDER_SHARE, 2)
                             + Math.Round((r.LateFeeFinal ?? 0m) * LATE_FEE_SHARE_TO_LENDER, 2);

            var lenderBefore = lenderW.AvailableBalance;
            lenderW.AvailableBalance = lenderBefore + lenderPayout;
            lenderW.LastUpdated = DateTime.Now;

            db.WalletTransactions.Add(new WalletTransaction
            {
                UserID = r.LenderUserID,
                Amount = lenderPayout,
                Direction = "Credit",
                Reason = "LenderPayout",
                RefType = "BorrowReservation",
                RefID = r.ReservationID,
                CreatedAt = DateTime.Now,
                BeforeBalance = lenderBefore,
                AfterBalance = lenderW.AvailableBalance,
                Notes = $"Borrow payout (card) for reservation #{r.ReservationID}"
            });

            // Close reservation
            r.Status = "Closed";
            r.UpdatedAt = DateTime.Now;

            // Notify
            db.Notifications.Add(new Notification
            {
                UserID = userId,
                Title = "Payment successful",
                Message = $"Final payment for “{r.BooksForSale?.Title}” completed.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });
            db.Notifications.Add(new Notification
            {
                UserID = r.LenderUserID,
                Title = "Payout received",
                Message = $"You’ve been credited R{lenderPayout:0.00} for reservation #{r.ReservationID}.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });

            db.SaveChanges();

            var receipt = new BorrowVMs.BorrowReceiptVM
            {
                ReservationID = r.ReservationID,
                BookID = r.BookID,
                Title = r.BooksForSale?.Title,
                TotalPaid = total,
                PaidAt = DateTime.Now
            };
            return View("BorrowPaySuccess", receipt);
        }

        public ActionResult BorrowPaySuccess()
        {
            return View();
        }


        // ========================= WALLET WITHDRAW (TEST MODE) =========================

        // POST: /Book/StartWithdrawal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StartWithdrawal(decimal amount, bool testMode = true)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0m)
            {
                TempData["WalletError"] = "Enter a valid withdrawal amount.";
                return RedirectToAction("Wallet");
            }

            int userId = GetUserId();
            var wallet = EnsureWallet(userId);

            if (amount > wallet.AvailableBalance)
            {
                TempData["WalletError"] = "Amount exceeds available balance.";
                return RedirectToAction("Wallet");
            }

            // --- TEST FLOW (no real funds move) ---
            if (testMode)
            {
                var before = wallet.AvailableBalance;
                wallet.AvailableBalance = before - amount;
                wallet.LastUpdated = DateTime.Now;

                db.WalletTransactions.Add(new WalletTransaction
                {
                    UserID = userId,
                    Amount = amount,
                    Direction = "Debit",
                    Reason = "Withdrawal",
                    RefType = "Test",
                    RefID = null,
                    CreatedAt = DateTime.Now,
                    BeforeBalance = before,
                    AfterBalance = wallet.AvailableBalance,
                    Notes = "withdrawal (Stripe payout executed)."
                });

                db.Notifications.Add(new Notification
                {
                    UserID = userId,
                    Title = "Withdrawal processed (test)",
                    Message = $"R {amount:0.00} withdrawn .funds moved.",
                    CreatedDate = DateTime.Now,
                    IsRead = false
                });

                db.SaveChanges();

                TempData["WalletToast"] = $"Withdrawal requested: R {amount:0.00} .";
                return RedirectToAction("WithdrawSuccess", new { amount });
            }

            // --- LIVE FLOW PLACEHOLDER (Stripe Connect payouts would go here) ---
            TempData["WalletError"] = "Live payouts are not enabled in this demo.";
            return RedirectToAction("Wallet");
        }

        // GET: /Book/WithdrawSuccess
        [HttpGet]
        public ActionResult WithdrawSuccess(decimal amount)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            int userId = GetUserId();
            var wallet = EnsureWallet(userId);

            var vm = new WithdrawalReceiptVM
            {
                Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
                NewBalance = wallet.AvailableBalance,
                ProcessedAt = DateTime.Now,
                Note = "Transaction Sucessful"
            };
            return View(vm); // create a tiny view if you want, or reuse a toast and redirect to Wallet
        }




    }
}
