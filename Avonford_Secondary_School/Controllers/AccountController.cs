using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using Avonford_Secondary_School.Models;
using Avonford_Secondary_School.Models.ViewModels;
using System.Configuration;
using System.Globalization;
using System.Text;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using iText.IO.Font.Constants;




namespace Avonford_Secondary_School.Controllers
{
    public class AccountController : Controller
    {
        // Database context instance
        private HighschoolDbEntities2 db = new HighschoolDbEntities2();

        // Email configuration constants
        private const string FROM_EMAIL = "kamvelihlembuthuma23@gmail.com";
        private const string FROM_PASSWORD = "jysuoddhkaqfqoeq";

        private const string HARD_CODED_ADMIN_EMAIL = "admin@avonford.com";
        private const string HARD_CODED_ADMIN_PASSWORD = "Admin@2004";

        // This is the default teacher/student password as well
        private const string DEFAULT_PASSWORD = "Admin@2004";

        private const decimal SCHOOL_FEE_AMOUNT = 38000m;

        // File log path (for advanced logging)
        private readonly string LogFilePath = System.Web.HttpContext.Current.Server.MapPath("~/App_Data/ApplicationLog.txt");

        #region Application Submission Actions

        // GET: Account/Apply
        [AllowAnonymous]
        public ActionResult Apply()
        {
            // Initialize the view model and populate dropdown lists for grades and streams.
            ApplicationViewModel model = new ApplicationViewModel();
            model.AvailableGrades = GetGradesList();
            model.AvailableStreams = GetStreamsList();

            // Log the GET request for the application form.
            Log("Accessing Application Form (GET).");

            return View(model);
        }

        // POST: Account/Apply
        // POST: Account/Apply
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public ActionResult Apply(ApplicationViewModel model)
        {
            Log("POST Application received.");
            try
            {
                // Check if the posted model is valid.
                if (!ModelState.IsValid)
                {
                    Log("ModelState is invalid.");
                    model.AvailableGrades = GetGradesList();
                    model.AvailableStreams = GetStreamsList();
                    return View(model);
                }

                // Perform manual validations (in addition to data annotations).
                ValidateApplication(model);
                if (!ModelState.IsValid)
                {
                    Log("Manual validation errors detected.");
                    model.AvailableGrades = GetGradesList();
                    model.AvailableStreams = GetStreamsList();
                    return View(model);
                }

                // -- New Duplicate Check Logic --
                bool alreadyExists = db.Applications.Any(a => a.NationalityID == model.NationalityID);
                if (alreadyExists)
                {
                    Log("Duplicate NationalityID detected: " + model.NationalityID);
                    ModelState.AddModelError("", "An application with this Nationality ID already exists in our system.");
                    model.AvailableGrades = GetGradesList();
                    model.AvailableStreams = GetStreamsList();
                    return View(model);
                }

                // Convert uploaded files to byte arrays.
                byte[] studentIdBytes = ConvertFileToByteArray(model.StudentIDDoc);
                byte[] parentIdBytes = ConvertFileToByteArray(model.ParentIDDoc);
                byte[] reportCardBytes = ConvertFileToByteArray(model.PreviousReportCard);
                byte[] applicationFormBytes = ConvertFileToByteArray(model.ApplicationForm);

                Log("File conversion complete.");

                // Check capacity rules
                if (!CheckCapacity(model.GradeSelection, model.StreamSelection))
                {
                    Log("Capacity check failed: selected grade/stream is full.");
                    ModelState.AddModelError("", "The selected grade/stream is full. Please contact the school for further assistance.");
                    model.AvailableGrades = GetGradesList();
                    model.AvailableStreams = GetStreamsList();
                    return View(model);
                }
                Log("Capacity check passed.");

                // Generate a unique application reference
                string uniqueAppRef = GenerateUniqueReference();
                Log("Generated unique reference: " + uniqueAppRef);

                // Create a new Application record
                Application application = new Application
                {
                    UniqueAppRef = uniqueAppRef,
                    FirstName = model.StudentFirstName,
                    LastName = model.StudentLastName,
                    NationalityID = model.NationalityID,
                    DateOfBirth = model.DateOfBirth,
                    ParentName = model.ParentName,
                    ParentID = model.ParentID,
                    ParentEmail = model.ParentEmail,
                    ParentContact = model.ParentContact,
                    SelectedGrade = model.GradeSelection,
                    // Always assign the stream from the view model, regardless of grade.
                    SelectedStream = model.StreamSelection,
                    StudentIDDoc = studentIdBytes,
                    ParentIDDoc = parentIdBytes,
                    PreviousReportCard = reportCardBytes,
                    ApplicationForm = applicationFormBytes,
                    ApplicationStatus = "Pending",
                    CreatedDate = DateTime.Now
                };

                // Save to DB
                db.Applications.Add(application);
                db.SaveChanges();
                Log("Application saved to database. Ref: " + uniqueAppRef);

                // Send a confirmation email
                SendConfirmationEmail(application.ParentEmail, application.FirstName, uniqueAppRef);

                Log("Application processed successfully for " + application.ParentEmail);

                // Redirect to a confirmation page
                return RedirectToAction("ApplicationSubmitted");
            }
            catch (Exception ex)
            {
                Log("Error in Application Submission: " + ex.Message + " StackTrace: " + ex.StackTrace);
                ModelState.AddModelError("", "There was an error processing your application. Please try again later.");
                model.AvailableGrades = GetGradesList();
                model.AvailableStreams = GetStreamsList();
                return View(model);
            }
        }


        // GET: Account/ApplicationSubmitted
        [AllowAnonymous]
        public ActionResult ApplicationSubmitted()
        {
            ViewBag.ConfirmationMessage = "Your application has been submitted successfully and is now under review. " +
                                         "You will receive an email notification once it's approved.";

            return View();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Validates the application view model for required fields.
        /// </summary>
        /// <param name="model">The ApplicationViewModel.</param>
        private void ValidateApplication(ApplicationViewModel model)
        {
            Log("Validating application fields.");
            if (string.IsNullOrWhiteSpace(model.StudentFirstName))
                ModelState.AddModelError("StudentFirstName", "Student first name is required.");
            if (string.IsNullOrWhiteSpace(model.StudentLastName))
                ModelState.AddModelError("StudentLastName", "Student last name is required.");
            if (string.IsNullOrWhiteSpace(model.NationalityID))
                ModelState.AddModelError("NationalityID", "Nationality ID is required.");
            if (model.DateOfBirth == null)
                ModelState.AddModelError("DateOfBirth", "Date of Birth is required.");
            if (string.IsNullOrWhiteSpace(model.ParentName))
                ModelState.AddModelError("ParentName", "Parent/Guardian name is required.");
            if (string.IsNullOrWhiteSpace(model.ParentID))
                ModelState.AddModelError("ParentID", "Parent/Guardian ID is required.");
            if (string.IsNullOrWhiteSpace(model.ParentEmail))
                ModelState.AddModelError("ParentEmail", "Parent email is required.");
            if (string.IsNullOrWhiteSpace(model.ParentContact))
                ModelState.AddModelError("ParentContact", "Parent contact is required.");

            if (model.GradeSelection < 8 || model.GradeSelection > 12)
                ModelState.AddModelError("GradeSelection", "Please select a valid grade.");

            // For grades 8 to 12, a stream must be selected now.
            if ((model.GradeSelection >= 8 && model.GradeSelection <= 12) && string.IsNullOrWhiteSpace(model.StreamSelection))
                ModelState.AddModelError("StreamSelection", "Please select a stream for the selected grade.");

            if (model.StudentIDDoc == null)
                ModelState.AddModelError("StudentIDDoc", "Student ID document is required.");
            if (model.ParentIDDoc == null)
                ModelState.AddModelError("ParentIDDoc", "Parent ID document is required.");
            if (model.PreviousReportCard == null)
                ModelState.AddModelError("PreviousReportCard", "Previous report card is required.");
            if (model.ApplicationForm == null)
                ModelState.AddModelError("ApplicationForm", "Application form document is required.");

            Log("Validation complete. ModelState is " + (ModelState.IsValid ? "valid" : "invalid"));
        }


        /// <summary>
        /// Converts an uploaded file to a byte array.
        /// </summary>
        /// <param name="file">The uploaded file.</param>
        /// <returns>Byte array representation or null if not available.</returns>
        private byte[] ConvertFileToByteArray(HttpPostedFileBase file)
        {
            if (file != null && file.ContentLength > 0)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    file.InputStream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if the current enrollment count for the selected grade/stream is below the capacity.
        /// </summary>
        /// <param name="grade">Selected grade.</param>
        /// <param name="stream">Selected stream (if applicable).</param>
        /// <returns>True if capacity is available; otherwise, false.</returns>
        private bool CheckCapacity(int grade, string stream)
        {
            Log("Checking capacity for grade: " + grade + " and stream: " + stream);
            // For Grade 8 and 9: capacity is managed per stream rather than overall.
            if (grade == 8 || grade == 9)
            {
                int currentCount = db.Students.Count(s => s.Grade == grade && s.Stream == stream);
                var streamCapacity = db.Streams.FirstOrDefault(st => st.StreamName == stream);
                Log("Current count for grade " + grade + " stream " + stream + " is " + currentCount +
                    " and max capacity is " + (streamCapacity != null ? streamCapacity.MaxCapacity.ToString() : "undefined"));
                if (streamCapacity != null && currentCount >= streamCapacity.MaxCapacity)
                    return false;
            }
            // For Grades 10, 11, 12, capacity is set per stream.
            else if (grade >= 10)
            {
                int currentCount = db.Students.Count(s => s.Grade == grade && s.Stream == stream);
                Models.Stream streamCapacity = db.Streams.FirstOrDefault(st => st.StreamName == stream);
                Log("Current count for grade " + grade + " stream " + stream + " is " + currentCount +
                    " and max capacity is " + (streamCapacity != null ? streamCapacity.MaxCapacity.ToString() : "undefined"));
                if (streamCapacity != null && currentCount >= streamCapacity.MaxCapacity)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Generates a unique application reference using timestamp and GUID.
        /// </summary>
        /// <returns>A unique application reference string.</returns>
        private string GenerateUniqueReference()
        {
            string refCode = "APP-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            return refCode;
        }

        /// <summary>
        /// Sends a confirmation email to the applicant's parent.
        /// </summary>
        /// <param name="toEmail">Recipient email address.</param>
        /// <param name="applicantName">Applicant's first name.</param>
        /// <param name="appRef">Unique application reference.</param>
        private void SendConfirmationEmail(string toEmail, string applicantName, string appRef)
        {
            Log("Sending confirmation email to: " + toEmail);
            try
            {
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(FROM_EMAIL);
                mail.To.Add(toEmail);
                mail.Subject = "Application Received - Avonford Secondary School";
                mail.Body = $"Dear {applicantName},\n\nThank you for applying to Avonford Secondary School. Your application reference is {appRef}. We will review your application and contact you shortly.\n\nBest regards,\nAvonford Secondary School";
                mail.IsBodyHtml = false;

                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
                smtp.Credentials = new NetworkCredential(FROM_EMAIL, FROM_PASSWORD);
                smtp.EnableSsl = true;

                smtp.Send(mail);
                Log("Confirmation email sent successfully to " + toEmail);
            }
            catch (Exception ex)
            {
                Log("Failed to send email: " + ex.Message);
                // In production, consider retrying or queuing the email
            }
        }

        /// <summary>
        /// Retrieves the list of grades for a dropdown.
        /// </summary>
        /// <returns>List of SelectListItem for grades.</returns>
        private List<SelectListItem> GetGradesList()
        {
            List<SelectListItem> grades = new List<SelectListItem>();
            var gradeList = db.Grades.ToList();
            foreach (var grade in gradeList)
            {
                grades.Add(new SelectListItem
                {
                    Text = grade.GradeName,
                    Value = grade.GradeID.ToString()
                });
            }
            return grades;
        }

        /// <summary>
        /// Retrieves the list of streams for a dropdown.
        /// </summary>
        /// <returns>List of SelectListItem for streams.</returns>
        private List<SelectListItem> GetStreamsList()
        {
            List<SelectListItem> streams = new List<SelectListItem>();
            var streamList = db.Streams.ToList();
            foreach (var stream in streamList)
            {
                streams.Add(new SelectListItem
                {
                    Text = stream.StreamName,
                    Value = stream.StreamName
                });
            }
            return streams;
        }

        #endregion

        #region Advanced Logging & Utility Methods

        /// <summary>
        /// Writes a log message to the App_Data log file with timestamp.
        /// </summary>
        /// <param name="message">Message to log.</param>
        private void Log(string message)
        {
            try
            {
                string logEntry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message + Environment.NewLine;
                System.IO.File.AppendAllText(LogFilePath, logEntry);
            }
            catch (Exception ex)
            {
                // If logging fails, write to Debug output.
                System.Diagnostics.Debug.WriteLine("Logging failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Example of an additional utility method to simulate a complex feature.
        /// This method could be expanded to create a PDF receipt or similar.
        /// </summary>
        private void AdvancedUtilityFeature()
        {
            // Simulate a complex process that takes several steps.
            Log("Starting advanced utility feature.");
            // Step 1: Data aggregation.
            var data = db.Applications.Select(a => new { a.ApplicationID, a.UniqueAppRef }).ToList();
            Log("Data aggregated. Total records: " + data.Count);

            // Step 2: Process data (dummy loop to increase complexity)
            foreach (var item in data)
            {
                // Simulate processing each record.
                Log("Processing Application ID: " + item.ApplicationID + " with ref: " + item.UniqueAppRef);
            }

            // Step 3: Finalize advanced feature.
            Log("Advanced utility feature completed.");
        }

        // Dummy method to simulate additional security measures.
        private void AdditionalSecurityChecks()
        {
            Log("Performing additional security checks.");
            // Add extra checks here.
            // For example, verifying a CAPTCHA, checking IP address, etc.
            Log("Additional security checks passed.");
        }

        // Dummy method to simulate integration with an external service.
        private void ExternalServiceIntegration()
        {
            Log("Starting external service integration.");
            // Simulate a delay or complex processing.
            System.Threading.Thread.Sleep(100);
            Log("External service integration completed successfully.");
        }

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion

        #region LOGIN & LOGOUT

        // GET: Account/Login
        // GET: Account/Login
        [HttpGet]
        public ActionResult Login()
        {
            if (Session["UserRole"] != null)
                return RedirectToRoleHome(Session["UserRole"].ToString());

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Hardcoded admin (recommend replacing with a real Users row soon)
            if (model.Email.Equals(HARD_CODED_ADMIN_EMAIL, StringComparison.OrdinalIgnoreCase) &&
                model.Password == HARD_CODED_ADMIN_PASSWORD)
            {
                Session["UserID"] = 0;                // <-- consider using a real Users row
                Session["UserRole"] = "Admin";
                return RedirectToAction("Index", "Admin");
            }

            // Normalize email for case-insensitive match
            var emailNorm = (model.Email ?? "").Trim().ToLower();
            var user = db.Users.FirstOrDefault(u => u.Email.ToLower() == emailNorm);

            if (user == null || user.PasswordHash != model.Password)
            {
                ModelState.AddModelError("", "Invalid credentials.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Your account is inactive. Contact Admin.");
                return View(model);
            }

            Session["UserID"] = user.UserID;
            Session["UserRole"] = user.Role;

            switch (user.Role)
            {
                case "Teacher":
                    return RedirectToAction("Index", "Teacher");

                case "Student":
                    {
                        var student = db.Students.FirstOrDefault(s => s.UserID == user.UserID);
                        if (student == null)
                        {
                            ModelState.AddModelError("", "No student record found. Contact Admin.");
                            return View(model);
                        }
                        if (student.Status == "Inactive")
                            return RedirectToAction("PaymentRequired");
                        return RedirectToAction("Index", "Student");
                    }

                case "Tutor":
                    {
                        var tutor = db.Tutors.FirstOrDefault(t => t.UserID == user.UserID);
                        if (tutor == null)
                        {
                            ModelState.AddModelError("", "No tutor record found. Contact Admin.");
                            return View(model);
                        }
                        return RedirectToAction("Index", "Tutor");
                    }

                case "Driver":
                    {
                        // Link driver profile by email (your schema doesn’t have a FK)
                        var driver = db.Drivers.FirstOrDefault(d => d.Email != null && d.Email.ToLower() == emailNorm);
                        if (driver == null)
                        {
                            ModelState.AddModelError("", "No driver profile linked to this account.");
                            return View(model);
                        }

                        Session["DriverID"] = driver.DriverID;   // ✔ store DriverID
                        Session["DriverName"] = driver.Name;

                        // Optional: mark online at login
                        // driver.Status = "Available";
                        // db.SaveChanges();

                        return RedirectToAction("Index", "Driver");
                    }

                default:
                    return RedirectToAction("Index", "Home");
            }
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        private ActionResult RedirectToRoleHome(string role)
        {
            switch (role)
            {
                case "Admin": return RedirectToAction("Index", "Admin");
                case "Teacher": return RedirectToAction("Index", "Teacher");
                case "Student": return RedirectToAction("Index", "Student");
                case "Tutor": return RedirectToAction("Index", "Tutor");
                case "Driver": return RedirectToAction("Index", "Driver");
                default: return RedirectToAction("Index", "Home");
            }
        }



        #endregion

        #region STUDENT FEE PAYMENT & ACTIVATION

        // GET: Account/PaymentRequired
        [HttpGet]
        public ActionResult PaymentRequired()
        {
            // Make sure user is a Student
            if (!IsStudentInSession())
            {
                return RedirectToAction("Login");
            }

            // Check student status
            int userID = (int)Session["UserID"];
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);
            if (student == null)
            {
                return RedirectToAction("Login");
            }

            if (student.Status == "Active")
            {
                // Already active => go to student home
                return RedirectToAction("Index", "Student");
            }

            // Show the payment required page
            var model = new PaymentRequiredViewModel
            {
                FeeAmount = SCHOOL_FEE_AMOUNT,
                TermsAccepted = false,
                ErrorMessage = ""
            };
            return View(model);
        }

        // POST: Account/PaymentRequired
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PaymentRequired(PaymentRequiredViewModel model)
        {
            // Make sure user is a Student
            if (!IsStudentInSession())
            {
                return RedirectToAction("Login");
            }

            if (!model.TermsAccepted)
            {
                model.ErrorMessage = "You must accept the terms before proceeding.";
                return View(model);
            }

            // If everything is okay, we proceed to PayPal
            // For demonstration, store the fee in Session
            Session["TotalCost"] = model.FeeAmount;
            return RedirectToAction("PaymentWithPaypal");
        }

        // GET: Account/PaymentWithPaypal
        public ActionResult PaymentWithPaypal()
        {
            // Example from your snippet:
            // We'll adapt it slightly to match your usage.

            if (Session["TotalCost"] == null)
            {
                // If no cost in session, fallback
                return RedirectToAction("PaymentRequired");
            }

            // Retrieve your config from web.config or anywhere
            string clientId = ConfigurationManager.AppSettings["paypal:clientId"];
            string clientSecret = ConfigurationManager.AppSettings["paypal:clientSecret"];

            var config = PayPal.Api.ConfigManager.Instance.GetProperties();
            var accessToken = new PayPal.Api.OAuthTokenCredential(clientId, clientSecret, config).GetAccessToken();
            var apiContext = new PayPal.Api.APIContext(accessToken);

            // Hard-coded currency to "USD" for demonstration
            // Or you can do ZAR if your PayPal account supports it
            double totalCost = Convert.ToDouble(Session["TotalCost"]) / 18;
            // If you want to convert or do anything else, adapt here:
            string totalAsString = totalCost.ToString("F2", CultureInfo.InvariantCulture);

            var payment = new PayPal.Api.Payment()
            {
                intent = "sale",
                payer = new PayPal.Api.Payer { payment_method = "paypal" },
                transactions = new List<PayPal.Api.Transaction>
                {
                    new PayPal.Api.Transaction
                    {
                        description = "School Fees Payment",
                        invoice_number = new Random().Next(100000).ToString(),
                        amount = new PayPal.Api.Amount
                        {
                            currency = "USD",
                            total = totalAsString,
                        }
                    }
                },
                redirect_urls = new PayPal.Api.RedirectUrls
                {
                    return_url = Url.Action("PaymentSuccessful", "Account", null, protocol: Request.Url.Scheme),
                    cancel_url = Url.Action("PaymentFailed", "Account", null, protocol: Request.Url.Scheme)
                }
            };

            var createdPayment = payment.Create(apiContext);
            var approvalUrl = createdPayment.links
                                .FirstOrDefault(link => link.rel == "approval_url")?
                                .href;

            return Redirect(approvalUrl);
        }

        // GET: Account/PaymentSuccessful
        public ActionResult PaymentSuccessful()
        {
            // Called by PayPal after user approves payment
            // Typically we confirm the payment with PayPal
            // For brevity, let's assume success if we land here

            if (!IsStudentInSession())
            {
                return RedirectToAction("Login");
            }

            int userID = (int)Session["UserID"];
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);
            if (student == null)
            {
                return RedirectToAction("Login");
            }

            // Update status to Active
            student.Status = "Active";
            db.SaveChanges();

            // Generate PDF receipt
            byte[] pdfData = GeneratePdfInvoice(student, (decimal)Session["TotalCost"]);

            // Email the PDF to the student's (or parent's) email
            SendPaymentReceiptEmail(student.ParentEmail, student.FirstName, pdfData);

            // Clear the session cost
            Session.Remove("TotalCost");

            // Show a success page or redirect to Student home
            return View("PaymentSuccessful");
        }

        // GET: Account/PaymentFailed
        public ActionResult PaymentFailed()
        {
            // Payment was canceled or failed
            // Show an error message or redirect to PaymentRequired
            return View("PaymentFailed");
        }

        #endregion

        #region PDF GENERATION & EMAIL

        /// <summary>
        /// Generates a simple PDF invoice with school fees breakdown.
        /// This is a placeholder. In a real app, you'd use iTextSharp or a similar library.
        /// </summary>
        private byte[] GeneratePdfInvoice(Student student, decimal amountPaid)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdfDoc = new PdfDocument(writer);
                Document doc = new Document(pdfDoc);

                // Create a bold font for the title.
                PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                Paragraph title = new Paragraph("AVONFORD SECONDARY SCHOOL - FEE INVOICE")
                    .SetFont(boldFont)
                    .SetFontSize(14)
                    .SetTextAlignment(TextAlignment.CENTER);
                doc.Add(title);

                doc.Add(new Paragraph("\n"));
                doc.Add(new Paragraph($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}"));
                doc.Add(new Paragraph($"Student Name: {student.FirstName} {student.LastName}"));
                doc.Add(new Paragraph($"Grade: {student.Grade}  |  Stream: {student.Stream}"));
                doc.Add(new Paragraph($"Nationality ID: {student.NationalityID}"));

                doc.Add(new Paragraph("\nBreakdown of Fees (ZAR):"));
                Table table = new Table(2, false);
                table.SetWidth(UnitValue.CreatePercentValue(100));

                table.AddCell(new Cell().Add(new Paragraph("Tuition")));
                table.AddCell(new Cell().Add(new Paragraph("R28,000")));

                table.AddCell(new Cell().Add(new Paragraph("Transport")));
                table.AddCell(new Cell().Add(new Paragraph("R3,000")));

                table.AddCell(new Cell().Add(new Paragraph("Catering")));
                table.AddCell(new Cell().Add(new Paragraph("R5,000")));

                table.AddCell(new Cell().Add(new Paragraph("Extra Subjects & Materials")));
                table.AddCell(new Cell().Add(new Paragraph("R2,000")));

                // Total row with bold text
                table.AddCell(new Cell().Add(new Paragraph("TOTAL").SetFont(boldFont)));
                table.AddCell(new Cell().Add(new Paragraph($"R{amountPaid}").SetFont(boldFont)));

                doc.Add(table);

                // For italic text, set an italic font.
                PdfFont italicFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE);
                doc.Add(new Paragraph("\nThank you for your payment!"));
                doc.Add(new Paragraph("This is a system-generated invoice.").SetFont(italicFont));

                doc.Close();
                return ms.ToArray();
            }
        }


        private void SendPaymentReceiptEmail(string toEmail, string studentName, byte[] pdfData)
        {
            try
            {
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(FROM_EMAIL);
                mail.To.Add(toEmail);
                mail.Subject = "Payment Receipt - Avonford Secondary School";
                mail.Body = $"Dear {studentName},\n\n" +
                            "Thank you for settling your school fees. Attached is your payment receipt.\n\n" +
                            "Best regards,\nAvonford Secondary School";
                mail.IsBodyHtml = false;

                // Attach the PDF
                if (pdfData != null && pdfData.Length > 0)
                {
                    // We'll call it "Invoice.pdf"
                    mail.Attachments.Add(new Attachment(new MemoryStream(pdfData), "Invoice.pdf", "application/pdf"));
                }

                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
                smtp.Credentials = new NetworkCredential(FROM_EMAIL, FROM_PASSWORD);
                smtp.EnableSsl = true;

                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                // In production, log or handle
                System.Diagnostics.Debug.WriteLine("Error sending receipt email: " + ex.Message);
            }
        }

        #endregion

        #region HELPER METHODS

        /// <summary>
        /// Checks if the current session indicates a Student role
        /// </summary>
        private bool IsStudentInSession()
        {
            if (Session["UserID"] == null || Session["UserRole"] == null)
                return false;

            string role = Session["UserRole"].ToString();
            return (role == "Student");
        }




        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2    
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2        
        // INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2         INC2 INC2 INC2           INC2 INC2 INC2    




        // GET: Account/RegisterTutor
        private List<SelectListItem> GetUniqueSubjectsForGrade(int gradeId)
        {
            var allSubjects = db.GradeSubjects
                .Where(gs => gs.GradeID == gradeId)
                .Select(gs => new { gs.SubjectID, gs.Subject.SubjectName })
                .Distinct()
                .ToList();

            var uniqueSubjects = allSubjects
                .GroupBy(s => s.SubjectID)
                .Select(g => new SelectListItem { Value = g.Key.ToString(), Text = g.First().SubjectName })
                .ToList();

            return uniqueSubjects;
        }

        [HttpGet]
        public ActionResult RegisterTutor()
        {
            var model = new RegisterTutorViewModel();
            model.GradeList = db.Grades
                .Select(g => new SelectListItem { Value = g.GradeID.ToString(), Text = g.GradeName }).ToList();

            foreach (var grade in db.Grades.ToList())
            {
                model.AvailableSubjectsByGrade[grade.GradeID] = GetUniqueSubjectsForGrade(grade.GradeID);
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegisterTutor(RegisterTutorViewModel model, string confirm, FormCollection form)
        {
            model.GradeList = db.Grades
                .Select(g => new SelectListItem { Value = g.GradeID.ToString(), Text = g.GradeName }).ToList();

            foreach (var grade in db.Grades.ToList())
            {
                model.AvailableSubjectsByGrade[grade.GradeID] = GetUniqueSubjectsForGrade(grade.GradeID);
            }

            if (model.SelectedGrades == null || !model.SelectedGrades.Any())
            {
                var postedGrades = form.GetValues("SelectedGrades");
                if (postedGrades != null)
                    model.SelectedGrades = postedGrades.Select(int.Parse).ToList();
            }

            model.SelectedSubjectsPerGrade = new Dictionary<int, List<int>>();
            if (model.SelectedGrades != null)
            {
                foreach (var gradeId in model.SelectedGrades)
                {
                    string key = "subjects_for_grade_" + gradeId;
                    string[] selectedSubjects = form.GetValues(key);
                    var subjectIds = selectedSubjects?.Select(int.Parse).Distinct().ToList() ?? new List<int>();
                    model.SelectedSubjectsPerGrade[gradeId] = subjectIds;
                }
            }

            if (!model.IsConfirmation)
            {
                if (model.ProfilePicture != null && model.ProfilePicture.ContentLength > 0)
                {
                    using (var ms = new MemoryStream())
                    {
                        model.ProfilePicture.InputStream.CopyTo(ms);
                        byte[] picBytes = ms.ToArray();
                        model.ProfilePicBase64 = Convert.ToBase64String(picBytes);
                    }
                }
                if (!ModelState.IsValid)
                    return View(model);

                model.IsConfirmation = true;
                return View("ConfirmTutor", model);
            }
            else
            {
                if (string.IsNullOrEmpty(model.ProfilePicBase64))
                {
                    ModelState.AddModelError("ProfilePicBase64", "Profile picture is required.");
                    return View("ConfirmTutor", model);
                }
                if (!ModelState.IsValid)
                    return View(model);

                var user = new User
                {
                    Email = model.Email,
                    PasswordHash = "Tutor@1234",
                    Role = "Tutor",
                    DateCreated = DateTime.Now,
                    IsActive = true
                };
                db.Users.Add(user);
                db.SaveChanges();

                byte[] picBytes = Convert.FromBase64String(model.ProfilePicBase64);

                var tutor = new Tutor
                {
                    UserID = user.UserID,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Qualifications = model.Qualifications,
                    Bio = model.Bio,
                    ProfilePicture = picBytes,
                    DateCreated = DateTime.Now,
                    IsActive = true
                };
                db.Tutors.Add(tutor);
                db.SaveChanges();

                foreach (var gradeId in model.SelectedGrades)
                {
                    foreach (var subjectId in model.SelectedSubjectsPerGrade[gradeId])
                    {
                        var tgs = new TutorGradeSubject
                        {
                            TutorID = tutor.TutorID,
                            GradeID = gradeId,
                            SubjectID = subjectId
                        };
                        db.TutorGradeSubjects.Add(tgs);
                    }
                }
                db.SaveChanges();

                return RedirectToAction("TutorsList");
            }
        }



        // GET: Account/TutorsList
        public ActionResult TutorsList()
        {
            var tutors = db.Tutors.ToList();
            var tutorVMs = new List<RegisterTutorViewModel>();
            foreach (var tutor in tutors)
            {
                var grades = db.TutorGradeSubjects
                    .Where(t => t.TutorID == tutor.TutorID)
                    .Select(t => t.GradeID)
                    .Distinct()
                    .ToList();

                var subjectsByGrade = new Dictionary<int, List<int>>();
                foreach (var grade in grades)
                {
                    var subjectIds = db.TutorGradeSubjects
                        .Where(t => t.TutorID == tutor.TutorID && t.GradeID == grade)
                        .Select(t => t.SubjectID)
                        .Distinct()
                        .ToList();

                    subjectsByGrade[grade] = subjectIds;
                }

                var gradeList = db.Grades.Select(g => new SelectListItem { Value = g.GradeID.ToString(), Text = g.GradeName }).ToList();

                var availableSubjectsByGrade = new Dictionary<int, List<SelectListItem>>();
                foreach (var grade in db.Grades.ToList())
                {
                    var subjList = db.GradeSubjects
                        .Where(gs => gs.GradeID == grade.GradeID)
                        .Select(gs => new SelectListItem
                        {
                            Value = gs.SubjectID.ToString(),
                            Text = gs.Subject.SubjectName
                        })
                        .ToList() // <-- Materialize the list in memory here!
                        .GroupBy(x => x.Value)
                        .Select(g => g.First())
                        .ToList();

                    availableSubjectsByGrade[grade.GradeID] = subjList;
                }

                string base64 = tutor.ProfilePicture != null && tutor.ProfilePicture.Length > 0
                    ? Convert.ToBase64String(tutor.ProfilePicture)
                    : "";

                tutorVMs.Add(new RegisterTutorViewModel
                {
                    TutorID = tutor.TutorID,
                    FirstName = tutor.FirstName,
                    LastName = tutor.LastName,
                    Email = tutor.Email,
                    Phone = tutor.Phone,
                    Qualifications = tutor.Qualifications,
                    Bio = tutor.Bio,
                    ProfilePicBase64 = base64,
                    SelectedGrades = grades,
                    SelectedSubjectsPerGrade = subjectsByGrade,
                    GradeList = gradeList,
                    AvailableSubjectsByGrade = availableSubjectsByGrade
                });
            }
            return View(tutorVMs);
        }



        // GET: Account/EditTutor/{id}
        public ActionResult EditTutor(int id)
        {
            var tutor = db.Tutors.Find(id);
            if (tutor == null)
                return HttpNotFound();

            var model = new RegisterTutorViewModel
            {
                FirstName = tutor.FirstName,
                LastName = tutor.LastName,
                Email = tutor.Email,
                Phone = tutor.Phone,
                Qualifications = tutor.Qualifications,
                Bio = tutor.Bio,
                SelectedGrades = db.TutorGradeSubjects.Where(t => t.TutorID == id).Select(t => t.GradeID).Distinct().ToList()
            };

            model.GradeList = db.Grades.Select(g => new SelectListItem { Value = g.GradeID.ToString(), Text = g.GradeName }).ToList();

            foreach (var grade in db.Grades.ToList())
            {
                var subjList = db.GradeSubjects
                    .Where(gs => gs.GradeID == grade.GradeID)
                    .Select(gs => new SelectListItem
                    {
                        Value = gs.SubjectID.ToString(),
                        Text = gs.Subject.SubjectName
                    }).ToList();
                model.AvailableSubjectsByGrade[grade.GradeID] = subjList;
                var subjectIds = db.TutorGradeSubjects.Where(t => t.TutorID == id && t.GradeID == grade.GradeID).Select(t => t.SubjectID).ToList();
                model.SelectedSubjectsPerGrade[grade.GradeID] = subjectIds;
            }
            return View(model);
        }

        // POST: Account/EditTutor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTutor(RegisterTutorViewModel model, FormCollection form)
        {
            model.GradeList = db.Grades.Select(g => new SelectListItem { Value = g.GradeID.ToString(), Text = g.GradeName }).ToList();
            foreach (var grade in db.Grades.ToList())
            {
                var subjList = db.GradeSubjects
                    .Where(gs => gs.GradeID == grade.GradeID)
                    .Select(gs => new SelectListItem
                    {
                        Value = gs.SubjectID.ToString(),
                        Text = gs.Subject.SubjectName
                    }).ToList();
                model.AvailableSubjectsByGrade[grade.GradeID] = subjList;
            }

            model.SelectedSubjectsPerGrade = new Dictionary<int, List<int>>();
            foreach (var gradeId in model.SelectedGrades)
            {
                string key = "subjects_for_grade_" + gradeId;
                string[] selectedSubjects = form.GetValues(key);
                var subjectIds = selectedSubjects?.Select(int.Parse).ToList() ?? new List<int>();
                model.SelectedSubjectsPerGrade[gradeId] = subjectIds;
            }

            if (!ModelState.IsValid)
                return View(model);

            var tutor = db.Tutors.FirstOrDefault(t => t.Email == model.Email);
            if (tutor == null)
                return HttpNotFound();

            tutor.FirstName = model.FirstName;
            tutor.LastName = model.LastName;
            tutor.Phone = model.Phone;
            tutor.Qualifications = model.Qualifications;
            tutor.Bio = model.Bio;
            db.SaveChanges();

            var oldAssignments = db.TutorGradeSubjects.Where(tgs => tgs.TutorID == tutor.TutorID).ToList();
            db.TutorGradeSubjects.RemoveRange(oldAssignments);
            foreach (var gradeId in model.SelectedGrades)
            {
                foreach (var subjectId in model.SelectedSubjectsPerGrade[gradeId])
                {
                    db.TutorGradeSubjects.Add(new TutorGradeSubject
                    {
                        TutorID = tutor.TutorID,
                        GradeID = gradeId,
                        SubjectID = subjectId
                    });
                }
            }
            db.SaveChanges();
            return RedirectToAction("TutorsList");
        }

        #endregion

        public ActionResult MyNotifications()
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var notifications = db.Notifications
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.CreatedDate)
                .Select(n => new NotificationVM
                {
                    NotificationID = n.NotificationID,
                    Title = n.Title,
                    Message = n.Message,
                    CreatedDate = n.CreatedDate,
                    IsRead = n.IsRead,
                    RelatedSessionID = n.RelatedSessionID
                })
                .ToList();

           
            foreach (var note in db.Notifications.Where(n => n.UserID == userId && !n.IsRead))
            {
                note.IsRead = true;
            }
            db.SaveChanges();

            return View(notifications);
        }


    }


}
