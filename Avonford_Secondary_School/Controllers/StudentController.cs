using Avonford_Secondary_School.Models;
using Avonford_Secondary_School.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Stripe;
using Stripe.Checkout;
using System.IO;

namespace Avonford_Secondary_School.Controllers
{
    public class StudentController : Controller
    {
        private HighschoolDbEntities2 db = new HighschoolDbEntities2();

        #region Dashboard & Subject List

        // GET: Student/Index
        public ActionResult Index()
        {
            // Retrieve the logged-in student's record using UserID from session.
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);
            if (student == null)
                return RedirectToAction("Login", "Account");

            // Build a list of subjects based on student's Grade and Stream.
            var subjects = db.GradeSubjects
                .Where(gs => gs.GradeID == student.Grade &&
                            (student.Grade < 10 || gs.StreamID == null || (gs.Stream != null && gs.Stream.StreamName == student.Stream)))
                .Select(gs => new StudentSubjectItem
                {
                    SubjectID = gs.SubjectID,
                    SubjectName = gs.Subject.SubjectName
                })
                .Distinct()
                .ToList();

            StudentDashboardViewModel model = new StudentDashboardViewModel
            {
                StudentName = student.FirstName + " " + student.LastName,
                Grade = student.Grade,
                Stream = student.Stream,
                Subjects = subjects
            };

            return View(model);
        }

        // GET: Student/SubjectAssessments?subjectId=...
        public ActionResult SubjectAssessments(int subjectId)
        {
            // Get the current student record.
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);
            if (student == null)
                return RedirectToAction("Login", "Account");

            DateTime now = DateTime.Now;

            var assessments = db.Assessments
                .Where(a => a.SubjectID == subjectId &&
                           (a.AssessmentDate > now.Date ||
                            (a.AssessmentDate == now.Date && a.StartTime <= now.TimeOfDay && a.EndTime >= now.TimeOfDay) ||
                            (a.AssessmentDate == now.Date && now.TimeOfDay < a.StartTime)))
                .Select(a => new AssessmentItem
                {
                    AssessmentID = a.AssessmentID,
                    AssessmentName = a.AssessmentName,
                    AssessmentType = a.AssessmentType,
                    AssessmentDate = a.AssessmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    // Initially determine if within the open window.
                    CanStartAttempt = (a.AssessmentDate == now.Date && now.TimeOfDay >= a.StartTime && now.TimeOfDay <= a.EndTime)
                })
                .ToList();

            // For each assessment, check if the student already has a submitted attempt.
            foreach (var assessment in assessments)
            {
                bool alreadyAttempted = db.AssessmentAttempts.Any(at =>
                    at.AssessmentID == assessment.AssessmentID &&
                    at.StudentID == student.StudentID &&
                    at.IsSubmitted);
                assessment.AlreadyAttempted = alreadyAttempted;
                if (alreadyAttempted)
                {
                    assessment.CanStartAttempt = false;
                }
            }


            SubjectAssessmentsViewModel model = new SubjectAssessmentsViewModel
            {
                SubjectID = subjectId,
                SubjectName = db.Subjects.Find(subjectId)?.SubjectName ?? "Unknown",
                Assessments = assessments
            };

            return View(model);
        }

        #endregion

        #region Assessment Attempt Workflow

        // GET: Student/StartAttempt?assessmentId=...
        public ActionResult StartAttempt(int assessmentId)
        {
            // Retrieve the assessment.
            var assessment = db.Assessments.Find(assessmentId);
            if (assessment == null)
                return HttpNotFound("Assessment not found.");

            // Create a new AssessmentAttempt record.
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);
            if (student == null)
                return RedirectToAction("Login", "Account");

            AssessmentAttempt attempt = new AssessmentAttempt
            {
                AssessmentID = assessmentId,
                StudentID = student.StudentID,
                AttemptStartTime = DateTime.Now,
                IsSubmitted = false
            };
            db.AssessmentAttempts.Add(attempt);
            db.SaveChanges();

            // Store the AttemptID in session to track the attempt.
            Session["CurrentAttemptID"] = attempt.AttemptID;

            // Redirect to a confirmation/instruction page.
            return RedirectToAction("ConfirmAttempt", new { attemptId = attempt.AttemptID });
        }

        // GET: Student/ConfirmAttempt?attemptId=...
        public ActionResult ConfirmAttempt(int attemptId)
        {
            // Retrieve the attempt and associated assessment.
            var attempt = db.AssessmentAttempts.Find(attemptId);
            if (attempt == null)
                return HttpNotFound("Attempt not found.");

            var assessment = db.Assessments.Find(attempt.AssessmentID);
            if (assessment == null)
                return HttpNotFound("Assessment not found.");

            // Build a simple view model for instructions.
            ConfirmAttemptViewModel model = new ConfirmAttemptViewModel
            {
                AttemptID = attempt.AttemptID,
                AssessmentID = assessment.AssessmentID,
                AssessmentName = assessment.AssessmentName,
                Instructions = assessment.Instructions,
                EndTime = assessment.EndTime, // used for timer calculation
                AssessmentDate = assessment.AssessmentDate
            };

            return View(model);
        }

        // POST: Student/ConfirmAttempt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmAttempt(ConfirmAttemptViewModel model)
        {
            // Once confirmed, redirect to the attempt page.
            return RedirectToAction("AttemptAssessment", new { attemptId = model.AttemptID });
        }

        // GET: Student/AttemptAssessment?attemptId=...
        public ActionResult AttemptAssessment(int attemptId)
        {
            // Retrieve attempt and associated assessment.
            var attempt = db.AssessmentAttempts.Find(attemptId);
            if (attempt == null)
                return HttpNotFound("Attempt not found.");

            var assessment = db.Assessments.Find(attempt.AssessmentID);
            if (assessment == null)
                return HttpNotFound("Assessment not found.");

            // Retrieve all questions for this assessment.
            var questions = db.AssessmentQuestions
                .Where(q => q.AssessmentID == assessment.AssessmentID)
                .Select(q => new QuestionResponseItem
                {
                    QuestionID = q.QuestionID,
                    QuestionType = q.QuestionType,
                    QuestionText = q.QuestionText,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD
                }).ToList();

            // Build the attempt view model.
            AssessmentAttemptViewModel modelVM = new AssessmentAttemptViewModel
            {
                AttemptID = attempt.AttemptID,
                AssessmentID = assessment.AssessmentID,
                AssessmentName = assessment.AssessmentName,
                Instructions = assessment.Instructions,
                StartTime = assessment.StartTime,
                EndTime = assessment.EndTime,
                AssessmentDate = assessment.AssessmentDate,
                Questions = questions,
                TimerEnd = DateTime.Today.Add(assessment.AssessmentDate == DateTime.Today ? assessment.EndTime : new TimeSpan(23, 59, 59))
            };

            return View(modelVM);
        }

        // POST: Student/SubmitAttempt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitAttempt(AssessmentAttemptViewModel model)
        {
            // Retrieve the attempt record.
            var attempt = db.AssessmentAttempts.Find(model.AttemptID);
            if (attempt == null)
                return HttpNotFound("Attempt not found.");

            // Loop through each question response in the model.
            foreach (var response in model.Responses)
            {
                AssessmentAttemptAnswer answer = new AssessmentAttemptAnswer
                {
                    AttemptID = attempt.AttemptID,
                    QuestionID = response.QuestionID,
                    StudentAnswer = response.StudentAnswer
                };
                db.AssessmentAttemptAnswers.Add(answer);
            }

            // Update attempt submission.
            attempt.IsSubmitted = true;
            attempt.AttemptEndTime = DateTime.Now;
            db.SaveChanges();

            // Redirect to a submission confirmation page.
            return RedirectToAction("SubmissionConfirmation", new { attemptId = attempt.AttemptID });
        }

        // GET: Student/SubmissionConfirmation?attemptId=...
        public ActionResult SubmissionConfirmation(int attemptId)
        {
            var attempt = db.AssessmentAttempts.Find(attemptId);
            if (attempt == null)
                return HttpNotFound("Attempt not found.");

            SubmissionConfirmationViewModel model = new SubmissionConfirmationViewModel
            {
                AttemptID = attempt.AttemptID,
                SubmissionTime = attempt.AttemptEndTime ?? DateTime.Now
            };

            return View(model);
        }

        #endregion

        #region Helper Methods

        [HttpGet]
        public ActionResult ReviewAttempts(int subjectId)
        {
            // Get the current student record using session.
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);
            if (student == null)
                return RedirectToAction("Login", "Account");

            // Retrieve attempts for assessments of the given subject where the attempt is submitted.
            var attemptsQuery = from at in db.AssessmentAttempts
                                join a in db.Assessments on at.AssessmentID equals a.AssessmentID
                                where at.StudentID == student.StudentID
                                      && a.SubjectID == subjectId
                                      && at.IsSubmitted
                                select new
                                {
                                    at.AttemptID,
                                    at.AttemptStartTime,
                                    at.AttemptEndTime,
                                    at.TotalScore,
                                    at.MaxScore
                                };

            var attemptsList = attemptsQuery.ToList();

            List<AttemptReviewItem> reviewItems = new List<AttemptReviewItem>();

            foreach (var at in attemptsList)
            {
                // For each attempt, get all answers along with question text.
                var answersQuery = from ans in db.AssessmentAttemptAnswers
                                   join q in db.AssessmentQuestions on ans.QuestionID equals q.QuestionID
                                   where ans.AttemptID == at.AttemptID
                                   select new AttemptAnswerReview
                                   {
                                       QuestionID = q.QuestionID,
                                       QuestionText = q.QuestionText,
                                       StudentAnswer = ans.StudentAnswer,
                                       ScoreAwarded = ans.ScoreAwarded ?? 0,
                                       IsCorrect = (ans.ScoreAwarded ?? 0) == 1
                                   };

                var answerItems = answersQuery.ToList();

                reviewItems.Add(new AttemptReviewItem
                {
                    AttemptID = at.AttemptID,
                    AttemptStartTime = at.AttemptStartTime,
                    AttemptEndTime = at.AttemptEndTime,
                    TotalScore = at.TotalScore ?? 0,
                    MaxScore = at.MaxScore ?? 0,
                    Answers = answerItems
                });
            }

            string subjectName = db.Subjects.Find(subjectId)?.SubjectName ?? "Unknown";

            StudentAttemptReviewViewModel model = new StudentAttemptReviewViewModel
            {
                SubjectID = subjectId,
                SubjectName = subjectName,
                Attempts = reviewItems
            };

            return View(model);
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        #endregion

        public ActionResult MyTutorClasses()
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);

            var paidEnrollments = db.TutorClassEnrollments
                .Where(e => e.StudentID == student.StudentID && e.IsPaid && e.Status == "Active")
                .ToList();

            var paidClasses = paidEnrollments
                .Select(e =>
                {
                    var c = e.TutorClass;
                    var t = c.Tutor;
                    return new StudentTutorClassCardVM
                    {
                        TutorClassID = c.TutorClassID,
                        ClassName = c.ClassName,
                        SubjectName = c.Subject.SubjectName,
                        GradeName = c.Grade.GradeName,
                        TutorName = t.FirstName + " " + t.LastName,
                        TutorProfilePic = t.ProfilePicture != null ? Convert.ToBase64String(t.ProfilePicture) : null,
                        ScheduleTemplate = c.ScheduleTemplate,
                        AnnualFee = c.AnnualFee,
                        Capacity = c.Capacity,
                        Enrolled = c.TutorClassEnrollments.Count(x => x.IsPaid && x.Status == "Active"),
                        CoverImageBase64 = c.CoverImage != null ? Convert.ToBase64String(c.CoverImage) : null,
                        IsPaid = true
                    };
                }).ToList();

            var model = new StudentMyClassesListVM { PaidClasses = paidClasses };
            return View(model);
        }

        public ActionResult FindTutorClasses()
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);

            var paidClassIds = db.TutorClassEnrollments
                .Where(e => e.StudentID == student.StudentID && e.IsPaid && e.Status == "Active")
                .Select(e => e.TutorClassID)
                .ToList();

            var grade = student.Grade;
            var stream = student.Stream;

            var gradeSubjects = db.GradeSubjects
                .Where(gs => gs.GradeID == grade && (grade < 10 || gs.Stream.StreamName == stream))
                .Select(gs => gs.SubjectID)
                .ToList();

            var eligibleClasses = db.TutorClasses
                .Where(c => !paidClassIds.Contains(c.TutorClassID)
                        && c.GradeID == grade
                        && gradeSubjects.Contains(c.SubjectID)
                        && c.IsActive)
                .ToList();

            var list = eligibleClasses.Select(c =>
            {
                var t = c.Tutor;
                return new StudentTutorClassCardVM
                {
                    TutorClassID = c.TutorClassID,
                    ClassName = c.ClassName,
                    SubjectName = c.Subject.SubjectName,
                    GradeName = c.Grade.GradeName,
                    TutorName = t.FirstName + " " + t.LastName,
                    TutorProfilePic = t.ProfilePicture != null ? Convert.ToBase64String(t.ProfilePicture) : null,
                    ScheduleTemplate = c.ScheduleTemplate,
                    AnnualFee = c.AnnualFee,
                    Capacity = c.Capacity,
                    Enrolled = c.TutorClassEnrollments.Count(x => x.IsPaid && x.Status == "Active"),
                    CoverImageBase64 = c.CoverImage != null ? Convert.ToBase64String(c.CoverImage) : null,
                    IsPaid = false
                };
            }).ToList();

            var model = new StudentFindClassesListVM { UnpaidClasses = list };
            return View(model);
        }

        public ActionResult ClassDetails(int classId)
        {
            var userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);

            var classEntity = db.TutorClasses.Find(classId);
            if (classEntity == null) return HttpNotFound();

            var enrollment = db.TutorClassEnrollments
                .FirstOrDefault(e => e.TutorClassID == classId && e.StudentID == student.StudentID && e.IsPaid && e.Status == "Active");

            var tutor = db.Tutors.Find(classEntity.TutorID);

            var resourceObjs = db.TutorClassResources
                .Where(r => r.TutorClassID == classId)
                .Select(r => new { r.FileName, r.ResourceID })
                .ToList();

            var sessionData = db.TutorSessions
                .Where(s => s.TutorClassID == classId && !s.IsCancelled && s.SessionDate >= DateTime.Today)
                .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
                .Select(s => new
                {
                    s.TutorSessionID,
                    s.SessionDate,
                    s.StartTime,
                    s.EndTime,
                    s.Title,
                    s.Mode,
                    s.OnlineMeetingLink
                })
                .Take(5)
                .ToList();

            // Updated: build UpcomingSessions as a list of rich objects for easier rendering in view
            var upcomingSessions = sessionData
                .Select(s => new UpcomingSessionVM
                {
                    SessionID = s.TutorSessionID,
                    DisplayString = $"{s.SessionDate:yyyy-MM-dd} {s.StartTime:hh\\:mm}-{s.EndTime:hh\\:mm} - {s.Title}",
                    Mode = s.Mode,
                    OnlineMeetingLink = s.OnlineMeetingLink
                })
                .ToList();

            var vm = new StudentClassDetailVM
            {
                TutorClassID = classId,
                ClassName = classEntity.ClassName,
                Description = classEntity.ClassDescription,
                SubjectName = classEntity.Subject.SubjectName,
                GradeName = classEntity.Grade.GradeName,
                TutorName = tutor.FirstName + " " + tutor.LastName,
                TutorID = tutor.TutorID,
                TutorProfilePic = tutor.ProfilePicture != null ? Convert.ToBase64String(tutor.ProfilePicture) : null,
                Capacity = classEntity.Capacity,
                Enrolled = classEntity.TutorClassEnrollments.Count(x => x.IsPaid && x.Status == "Active"),
                AnnualFee = classEntity.AnnualFee,
                CoverImageBase64 = classEntity.CoverImage != null ? Convert.ToBase64String(classEntity.CoverImage) : null,
                Mode = classEntity.Mode,
                Location = classEntity.Location,
                EnrollmentDeadline = classEntity.EnrollmentDeadline,
                EntryRequirements = classEntity.EntryRequirements,
                WhatsAppContact = classEntity.WhatsAppContact,
                ScheduleTemplate = classEntity.ScheduleTemplate,
                ResourceFiles = resourceObjs.Select(x => $"{x.FileName}|{x.ResourceID}").ToList(),
                UpcomingSessions = upcomingSessions,
                CanPay = enrollment == null,
                CanExit = enrollment != null
            };

            // Set ViewBag.EnrollmentID for Exit link (if paid)
            if (enrollment != null)
                ViewBag.EnrollmentID = enrollment.EnrollmentID;

            return View(vm);
        }

        public ActionResult ViewTutor(int tutorId)
        {
            var t = db.Tutors.Find(tutorId);
            var vm = new StudentTutorInfoVM
            {
                TutorName = t.FirstName + " " + t.LastName,
                TutorProfilePic = t.ProfilePicture != null ? Convert.ToBase64String(t.ProfilePicture) : null,
                Bio = t.Bio,
                Qualifications = t.Qualifications,
                Email = t.Email,
                Phone = t.Phone
            };
            return View(vm);
        }

        [HttpGet]
        public ActionResult PayForClass(int classId)
        {
            Stripe.StripeConfiguration.ApiKey =
                System.Configuration.ConfigurationManager.AppSettings["StripeSecretKey"];

            var userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);
            if (student == null) return RedirectToAction("Login", "Account");

            var tclass = db.TutorClasses.Find(classId);
            if (tclass == null) return HttpNotFound();

            var amountCents = (long)(tclass.AnnualFee * 100M);

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
        {
            new Stripe.Checkout.SessionLineItemOptions
            {
                PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                {
                    Currency = "zar",
                    ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"{tclass.ClassName} ({tclass.Grade.GradeName} - {tclass.Subject.SubjectName})"
                    },
                    UnitAmount = amountCents
                },
                Quantity = 1
            }
        },
                Mode = "payment",
                SuccessUrl = Url.Action("PayForClassCallback", "Student", new { classId = tclass.TutorClassID }, protocol: Request.Url.Scheme),
                CancelUrl = Url.Action("ClassDetails", "Student", new { classId = tclass.TutorClassID }, protocol: Request.Url.Scheme),
                Metadata = new Dictionary<string, string>
        {
            { "student_id", student.StudentID.ToString() },
            { "class_id", tclass.TutorClassID.ToString() }
        }
            };
            var service = new Stripe.Checkout.SessionService();
            var session = service.Create(options);

            return Redirect(session.Url);
        }

        [HttpGet]
        public ActionResult PayForClassCallback(int classId)
        {
            var userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);
            if (student == null) return RedirectToAction("Login", "Account");

            var tclass = db.TutorClasses.Find(classId);
            if (tclass == null) return HttpNotFound();

            var existing = db.TutorClassEnrollments.FirstOrDefault(e =>
                e.StudentID == student.StudentID && e.TutorClassID == classId);

            if (existing == null)
            {
                var enroll = new TutorClassEnrollment
                {
                    TutorClassID = classId,
                    StudentID = student.StudentID,
                    EnrollmentDate = DateTime.Now,
                    IsPaid = true,
                    PaymentDate = DateTime.Now,
                    PaymentAmount = tclass.AnnualFee,
                    Status = "Active"
                };
                db.TutorClassEnrollments.Add(enroll);
                db.SaveChanges();
            }
            else
            {
                existing.IsPaid = true;
                existing.PaymentDate = DateTime.Now;
                existing.PaymentAmount = tclass.AnnualFee;
                existing.Status = "Active";
                db.SaveChanges();
            }

            db.Notifications.Add(new Notification
            {
                UserID = student.UserID,
                Title = "Class Enrollment Successful",
                Message = $"You have been enrolled in {tclass.ClassName} for {tclass.Grade.GradeName}.",
                CreatedDate = DateTime.Now,
                RelatedSessionID = null
            });
            db.SaveChanges();

            return RedirectToAction("EnrollmentConfirmation", new { classId = tclass.TutorClassID });
        }

        [HttpPost]
        public ActionResult ConfirmStripePayment(string sessionId, int classId)
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);

            var enrollment = db.TutorClassEnrollments.FirstOrDefault(e => e.StudentID == student.StudentID && e.TutorClassID == classId);

            if (enrollment == null)
            {
                enrollment = new TutorClassEnrollment
                {
                    TutorClassID = classId,
                    StudentID = student.StudentID,
                    EnrollmentDate = DateTime.Now,
                    IsPaid = true,
                    PaymentDate = DateTime.Now,
                    PaymentAmount = db.TutorClasses.Find(classId).AnnualFee,
                    Status = "Active"
                };
                db.TutorClassEnrollments.Add(enrollment);
            }
            else
            {
                enrollment.IsPaid = true;
                enrollment.PaymentDate = DateTime.Now;
                enrollment.PaymentAmount = db.TutorClasses.Find(classId).AnnualFee;
                enrollment.Status = "Active";
            }
            db.SaveChanges();

            var c = db.TutorClasses.Find(classId);

            var model = new EnrollmentConfirmationVM
            {
                ClassName = c.ClassName,
                TutorName = c.Tutor.FirstName + " " + c.Tutor.LastName,
                SubjectName = c.Subject.SubjectName,
                PaymentReference = sessionId,
                PaidAt = DateTime.Now,
                Amount = c.AnnualFee
            };
            return View("EnrollmentConfirmation", model);
        }

        [HttpGet]
        public ActionResult EnrollmentConfirmation(int classId)
        {
            var userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);
            var tclass = db.TutorClasses.Find(classId);

            var model = new EnrollmentConfirmationVM
            {
                ClassName = tclass.ClassName,
                SubjectName = tclass.Subject.SubjectName,
                Grade = tclass.Grade.GradeName,
                Tutor = tclass.Tutor.FirstName + " " + tclass.Tutor.LastName,
                PaidAmount = tclass.AnnualFee
            };
            return View(model);
        }

        [HttpGet]
        public ActionResult ExitClass(int enrollmentId)
        {
            var e = db.TutorClassEnrollments.Find(enrollmentId);
            var model = new StudentExitClassVM
            {
                EnrollmentID = enrollmentId,
                TutorClassID = e.TutorClassID,
                ClassName = e.TutorClass.ClassName
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExitClass(StudentExitClassVM model)
        {
            var e = db.TutorClassEnrollments.Find(model.EnrollmentID);
            e.Status = "Exited";
            e.ExitReason = model.Reason;
            e.ExitDate = DateTime.Now;
            db.TutorClassExitHistories.Add(new TutorClassExitHistory
            {
                EnrollmentID = e.EnrollmentID,
                StudentID = e.StudentID,
                TutorClassID = e.TutorClassID,
                ExitReason = model.Reason,
                ExitDate = DateTime.Now
            });
            db.SaveChanges();

            var tutor = e.TutorClass.Tutor;
            var tutorUserId = tutor.UserID;

            db.Notifications.Add(new Notification
            {
                UserID = tutorUserId,
                Title = "Class Exit",
                Message = $"A student has exited your class \"{e.TutorClass.ClassName}\": Reason - {model.Reason}",
                CreatedDate = DateTime.Now,
                IsRead = false
            });
            db.SaveChanges();

            return RedirectToAction("MyTutorClasses");
        }

        public ActionResult EnrollmentHistory()
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);

            var history = db.TutorClassExitHistories
                .Where(h => h.StudentID == student.StudentID)
                .OrderByDescending(h => h.ExitDate)
                .Select(h => new
                {
                    h.TutorClass,
                    h.ExitReason,
                    h.ExitDate
                }).ToList();

            return View(history);
        }

        public ActionResult ViewResource(int resourceId)
        {
            var res = db.TutorClassResources.Find(resourceId);
            if (res == null) return HttpNotFound();
            return File(res.FileContent, "application/pdf", res.FileName);
        }



        public ActionResult UpcomingSessions()
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);

            var upcomingSessions = db.TutorSessions
                .Where(s => (s.TutorClass.TutorClassEnrollments.Any(e => e.StudentID == student.StudentID && e.IsPaid && e.Status == "Active"))
                    && !s.IsCancelled && s.SessionDate >= DateTime.Today)
                .OrderBy(s => s.SessionDate)
                .Select(s => new UpcomingSessionVM
                {
                    SessionId = s.TutorSessionID,
                    TutorClassID = s.TutorClassID,
                    ClassName = s.TutorClass.ClassName,
                    Subject = s.TutorClass.Subject.SubjectName,
                    TutorName = s.Tutor.FirstName + " " + s.Tutor.LastName,
                    Mode = s.Mode,
                    SessionDate = s.SessionDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Location = s.Location,
                    OnlineMeetingLink = s.OnlineMeetingLink,
                    CanJoin = s.Mode == "Online" && !s.IsCancelled,
                    CanViewDirections = s.Mode == "Physical" && !s.IsCancelled && !string.IsNullOrEmpty(s.Location),
                    CanConfirmAttendance = s.Mode == "Physical" && !s.IsCancelled,
                    HasAttended = db.SessionAttendances.Any(a => a.TutorSessionID == s.TutorSessionID && a.StudentID == student.StudentID && a.AttendanceStatus == "Present")
                })
                .ToList();

            return View(upcomingSessions);
        }

        public ActionResult SessionDetail(int sessionId)
        {
            var s = db.TutorSessions.Find(sessionId);
            var userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(st => st.UserID == userId);

            var hasAttended = db.SessionAttendances.Any(a => a.TutorSessionID == s.TutorSessionID && a.StudentID == student.StudentID && a.AttendanceStatus == "Present");

            var vm = new UpcomingSessionVM
            {
                SessionId = s.TutorSessionID,
                TutorClassID = s.TutorClassID,
                ClassName = s.TutorClass?.ClassName,
                Subject = s.TutorClass?.Subject?.SubjectName,
                TutorName = s.Tutor.FirstName + " " + s.Tutor.LastName,
                Mode = s.Mode,
                SessionDate = s.SessionDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Location = s.Location,
                OnlineMeetingLink = s.OnlineMeetingLink,
                CanJoin = s.Mode == "Online" && !s.IsCancelled,
                CanViewDirections = s.Mode == "Physical" && !s.IsCancelled && !string.IsNullOrEmpty(s.Location),
                CanConfirmAttendance = s.Mode == "Physical" && !s.IsCancelled && !hasAttended,
                HasAttended = hasAttended
            };
            return View(vm);
        }

        public ActionResult GenerateAttendanceQRCode(int sessionId)
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);
            var session = db.TutorSessions.Find(sessionId);

            var qrData = new
            {
                StudentID = student.StudentID,
                StudentName = student.FirstName + " " + student.LastName,
                SessionID = session.TutorSessionID,
                TutorClassID = session.TutorClassID,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(qrData);

            using (var qrGenerator = new QRCoder.QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(json, QRCoder.QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new QRCoder.QRCode(qrCodeData))
            using (var bitmap = qrCode.GetGraphic(20))
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                var qrImageBytes = ms.ToArray();
                var vm = new QRCodeVM { QRData = json, QRImageBytes = qrImageBytes };
                return File(qrImageBytes, "image/png");
            }
        }

        public ActionResult AttendanceConfirmation(int sessionId)
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);
            var session = db.TutorSessions.Find(sessionId);

            var attendance = db.SessionAttendances.FirstOrDefault(a => a.TutorSessionID == sessionId && a.StudentID == student.StudentID);

            var vm = new AttendanceConfirmationVM
            {
                StudentName = student.FirstName + " " + student.LastName,
                ClassName = session.TutorClass?.ClassName,
                TutorName = session.Tutor.FirstName + " " + session.Tutor.LastName,
                TimeConfirmed = attendance?.CheckInTime ?? DateTime.Now,
                Status = attendance?.AttendanceStatus ?? "Pending"
            };

            return View(vm);
        }
        [HttpGet]
        public ActionResult RateSession(int sessionId)
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);
            var session = db.TutorSessions.Find(sessionId);

            var existing = db.SessionFeedbacks.FirstOrDefault(f =>
                f.TutorSessionID == sessionId && f.StudentID == student.StudentID);

            var vm = new SessionFeedbackVM
            {
                TutorSessionID = sessionId,
                StudentID = student.StudentID,
                TutorID = session.TutorID,
                TutorName = session.Tutor.FirstName + " " + session.Tutor.LastName,
                ClassName = session.TutorClass?.ClassName,
                SessionDate = session.SessionDate,
                Rating = existing?.Rating ?? 0,
                Feedback = existing?.Feedback,
                Submitted = existing != null
            };
            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RateSession(SessionFeedbackVM model)
        {
            var feedback = db.SessionFeedbacks.FirstOrDefault(f =>
                f.TutorSessionID == model.TutorSessionID && f.StudentID == model.StudentID);

            if (feedback == null)
            {
                db.SessionFeedbacks.Add(new SessionFeedback
                {
                    TutorSessionID = model.TutorSessionID,
                    StudentID = model.StudentID,
                    TutorID = model.TutorID,
                    Rating = model.Rating,
                    Feedback = model.Feedback,
                    CreatedDate = DateTime.Now
                });
            }
            else
            {
                feedback.Rating = model.Rating;
                feedback.Feedback = model.Feedback;
                feedback.CreatedDate = DateTime.Now;
            }
            db.SaveChanges();

            model.Submitted = true;
            return View("MyTutorClasses", model);
        }



        [HttpGet]
        public ActionResult BookPrivateSession()
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);

            // Subjects only from student's grade/stream
            var subjects = db.GradeSubjects
                .Where(gs => gs.GradeID == student.Grade &&
                    (student.Grade < 10 || gs.Stream.StreamName == student.Stream))
                .Select(gs => new SelectListItem
                {
                    Value = gs.SubjectID.ToString(),
                    Text = gs.Subject.SubjectName
                }).Distinct().ToList();

            var model = new PrivateSessionRequestVM
            {
                AvailableSubjects = subjects,
                StartTime = DateTime.Now.AddHours(1),
                EndTime = DateTime.Now.AddHours(2)
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookPrivateSession(PrivateSessionRequestVM model)
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);

            model.AvailableSubjects = db.GradeSubjects
                .Where(gs => gs.GradeID == student.Grade &&
                    (student.Grade < 10 || gs.Stream.StreamName == student.Stream))
                .Select(gs => new SelectListItem
                {
                    Value = gs.SubjectID.ToString(),
                    Text = gs.Subject.SubjectName
                }).Distinct().ToList();

            if (!ModelState.IsValid)
                return View(model);

            // Check for valid time range
            if (model.EndTime <= model.StartTime || model.StartTime < DateTime.Now)
            {
                ModelState.AddModelError("", "Invalid session time. Please choose a valid time window.");
                return View(model);
            }

            // Tutors qualified for the subject/grade and not in conflict
            var allTutors = db.TutorGradeSubjects
                .Where(tgs => tgs.GradeID == student.Grade && tgs.SubjectID == model.SelectedSubjectID)
                .Select(tgs => tgs.Tutor)
                .Distinct().ToList();

            var availableTutors = new List<AvailableTutorVM>();
            foreach (var tutor in allTutors)
            {
                // Check for private session conflicts for this tutor
                bool isFree = !db.TutorPrivateBookings.Any(r =>
                    r.TutorID == tutor.TutorID &&
                    r.Status == "Accepted" &&
                    (
                        (model.StartTime < r.EndDateTime && model.StartTime >= r.StartDateTime) ||
                        (model.EndTime > r.StartDateTime && model.EndTime <= r.EndDateTime) ||
                        (model.StartTime <= r.StartDateTime && model.EndTime >= r.EndDateTime)
                    )
                );

                if (!isFree) continue;

                availableTutors.Add(new AvailableTutorVM
                {
                    TutorID = tutor.TutorID,
                    TutorName = tutor.FirstName + " " + tutor.LastName,
                    ProfilePicBase64 = tutor.ProfilePicture != null ? Convert.ToBase64String(tutor.ProfilePicture) : null,
                    Qualifications = tutor.Qualifications,
                    Bio = tutor.Bio,
                    Email = tutor.Email
                });
            }

            model.AvailableTutors = availableTutors;

            // Store booking details in TempData for next step
            TempData["PrivateSessionRequestVM"] = model;
            return View("AvailableTutors", model);
        }

        [HttpGet]
        public ActionResult TutorProfile(int tutorId)
        {
            var tutor = db.Tutors.Find(tutorId);
            if (tutor == null) return HttpNotFound();

            var vm = new AvailableTutorVM
            {
                TutorID = tutor.TutorID,
                TutorName = tutor.FirstName + " " + tutor.LastName,
                ProfilePicBase64 = tutor.ProfilePicture != null ? Convert.ToBase64String(tutor.ProfilePicture) : null,
                Qualifications = tutor.Qualifications,
                Bio = tutor.Bio,
                Email = tutor.Email
            };
            return View(vm);
        }

        
        public ActionResult PrivateSessionConfirmation()
        {
            var model = TempData["ConfirmationVM"] as PrivateSessionConfirmationVM;
            if (model == null)
            {
                // If user visits directly or on refresh, fallback: redirect or show error.
                TempData["Error"] = "No booking information was found. Please book a session first.";
                return RedirectToAction("BookPrivateSession");
            }
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitPrivateSessionRequest(int tutorId, string topicMessage)
        {
            // Retrieve stored booking info
            var bookingVM = TempData["PrivateSessionRequestVM"] as PrivateSessionRequestVM;
            if (bookingVM == null) return RedirectToAction("BookPrivateSession");

            int userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);

            
            bool studentConflict = db.TutorPrivateBookings.Any(r =>
                r.StudentID == student.StudentID &&
                r.Status == "Accepted" &&
                (
                    (bookingVM.StartTime < r.EndDateTime && bookingVM.StartTime >= r.StartDateTime) ||
                    (bookingVM.EndTime > r.StartDateTime && bookingVM.EndTime <= r.EndDateTime) ||
                    (bookingVM.StartTime <= r.StartDateTime && bookingVM.EndTime >= r.EndDateTime)
                )
            );
            if (studentConflict)
            {
                TempData["Error"] = "You already have a session scheduled in this time window.";
                return RedirectToAction("BookPrivateSession");
            }

            // Create the pending request
            var request = new TutorPrivateBooking
            {
                StudentID = student.StudentID,
                TutorID = tutorId,
                SubjectID = bookingVM.SelectedSubjectID,
                GradeID = student.Grade,
                StartDateTime = bookingVM.StartTime,
                EndDateTime = bookingVM.EndTime,
                Topic = topicMessage,
                Status = "Pending",
                RequestedAt = DateTime.Now
            };
            db.TutorPrivateBookings.Add(request);
            db.SaveChanges();

           
            db.Notifications.Add(new Notification
            {
                UserID = db.Tutors.Find(tutorId).UserID,
                Title = "New Private Session Request",
                Message = $"You have a new session request from {student.FirstName} {student.LastName} for {bookingVM.StartTime:yyyy-MM-dd HH:mm}.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });
            db.SaveChanges();

            TempData["ConfirmationVM"] = new PrivateSessionConfirmationVM
            {
                RequestID = request.BookingID,
                TutorName = db.Tutors.Find(tutorId).FirstName + " " + db.Tutors.Find(tutorId).LastName,
                StartTime = bookingVM.StartTime,
                EndTime = bookingVM.EndTime,
                Status = "Pending"
            };

            return RedirectToAction("PrivateSessionConfirmation");
        }

        public ActionResult MyPrivateSessions()
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userId);

            var sessions = db.TutorPrivateBookings
                .Where(r => r.StudentID == student.StudentID)
                .OrderByDescending(r => r.RequestedAt)
                .Select(r => new PrivateSessionStatusVM
                {
                    RequestID = r.BookingID,
                    TutorName = r.Tutor.FirstName + " " + r.Tutor.LastName,
                    SubjectName = r.Subject.SubjectName,
                    StartTime = r.StartDateTime,
                    EndTime = r.EndDateTime,
                    Status = r.Status,
                    TopicMessage = r.Topic,
                    PaymentStatus = r.Status
                }).ToList();

            return View(sessions);
        }










        [HttpGet]
        public ActionResult RequestTransfer()
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);
            if (student == null)
                return RedirectToAction("Login", "Account");

            // Remove the window check – students can always request a transfer!

            // Find streams, subjects, enrolled, capacities
            var streams = db.Streams.ToList();
            var streamOptions = streams.Select(str =>
            {
                var capacity = str.MaxCapacity;
                int enrolled = db.Students.Count(s => s.Grade == student.Grade && s.Stream == str.StreamName && s.Status == "Active");
                var streamSubjects = db.GradeSubjects
                    .Where(gs => gs.GradeID == student.Grade && gs.Stream.StreamName == str.StreamName)
                    .Select(gs => gs.Subject.SubjectName)
                    .ToList();
                return new StreamOptionVM
                {
                    StreamName = str.StreamName,
                    Description = GetStreamDescription(str.StreamName),
                    Capacity = capacity,
                    Enrolled = enrolled,
                    Subjects = streamSubjects
                };
            }).ToList();

            // Get current subjects
            var currentSubjects = db.GradeSubjects
                .Where(gs => gs.GradeID == student.Grade && (student.Grade < 10 || gs.Stream.StreamName == student.Stream))
                .Select(gs => gs.Subject.SubjectName).ToList();

            var model = new StudentTransferRequestFormVM
            {
                CurrentStream = student.Stream,
                CurrentGrade = student.Grade,
                CurrentSubjects = currentSubjects,
                AvailableStreams = streamOptions,
                CanRequestTransfer = true, // (You can leave this or remove from your viewmodel/views if not needed)
                TransferWindowMessage = "" // (Likewise)
            };

            return View(model);
        }


        // POST: Student/RequestTransfer (Step 1 submit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestTransfer(StudentTransferRequestFormVM model)
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);

            // Re-populate streams and current info for redisplay if validation fails
            var streams = db.Streams.ToList();
            model.AvailableStreams = streams.Select(str =>
            {
                var capacity = str.MaxCapacity;
                int enrolled = db.Students.Count(s => s.Grade == student.Grade && s.Stream == str.StreamName && s.Status == "Active");
                var streamSubjects = db.GradeSubjects
                    .Where(gs => gs.GradeID == student.Grade && gs.Stream.StreamName == str.StreamName)
                    .Select(gs => gs.Subject.SubjectName)
                    .ToList();
                return new StreamOptionVM
                {
                    StreamName = str.StreamName,
                    Description = "", // Optional: add your stream descriptions
                    Capacity = capacity,
                    Enrolled = enrolled,
                    Subjects = streamSubjects
                };
            }).ToList();

            model.CurrentStream = student.Stream;
            model.CurrentGrade = student.Grade;
            model.CurrentSubjects = db.GradeSubjects
                .Where(gs => gs.GradeID == student.Grade && (student.Grade < 10 || gs.Stream.StreamName == student.Stream))
                .Select(gs => gs.Subject.SubjectName).ToList();

            // Add your normal validations if needed
            if (string.IsNullOrWhiteSpace(model.SelectedNewStream))
            {
                ModelState.AddModelError("SelectedNewStream", "Please select a stream.");
            }
            if (string.IsNullOrWhiteSpace(model.Justification) || model.Justification.Length < 10)
            {
                ModelState.AddModelError("Justification", "Please provide a justification (at least 10 characters).");
            }
            if (model.SupportingDocument != null &&
                (!model.SupportingDocument.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    || model.SupportingDocument.ContentLength > 4 * 1024 * 1024))
            {
                ModelState.AddModelError("SupportingDocument", "Only PDF files up to 4MB are allowed.");
            }

            if (!ModelState.IsValid)
                return View(model);

            // Begin Save Transfer Request
            try
            {
                var transfer = new StudentTransferRequest
                {
                    StudentID = student.StudentID,
                    OldStream = model.CurrentStream,
                    NewStream = model.SelectedNewStream,
                    OldGrade = model.CurrentGrade,
                    NewGrade = model.CurrentGrade,
                    Justification = model.Justification,
                    AttachmentFileID = null, // To be updated if file present
                    Status = "PendingTeacher",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsActive = true
                };

                db.StudentTransferRequests.Add(transfer);
                db.SaveChanges(); // Generates TransferRequestID

                // Save the file if present
                if (model.SupportingDocument != null)
                {
                    string uploadedFileName = Path.GetFileName(model.SupportingDocument.FileName);
                    byte[] uploadedFileData;
                    using (var br = new BinaryReader(model.SupportingDocument.InputStream))
                    {
                        uploadedFileData = br.ReadBytes(model.SupportingDocument.ContentLength);
                    }
                    var transferFile = new TransferRequestFile
                    {
                        TransferRequestID = transfer.TransferRequestID, // Must exist now!
                        FileName = uploadedFileName,
                        FileType = "application/pdf",
                        FileContent = uploadedFileData,
                        UploadedAt = DateTime.Now,
                        UploadedBy = userID
                    };
                    db.TransferRequestFiles.Add(transferFile);
                    db.SaveChanges();

                    // Now update the transfer request to point to the file
                    transfer.AttachmentFileID = transferFile.FileID;
                    db.SaveChanges();
                }

                // Optionally: Audit log, notification, etc.

                return RedirectToAction("TransferSubmissionSuccess", new { id = transfer.TransferRequestID });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                // Log validation errors for debugging
                var errors = new System.Text.StringBuilder();
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        errors.AppendLine($"Property: {validationError.PropertyName} Error: {validationError.ErrorMessage}");
                    }
                }
                // Optionally, display or log errors
                ModelState.AddModelError("", errors.ToString());
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Unexpected error: " + ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public ActionResult TransferSubmissionSuccess(int id)
        {
            var transfer = db.StudentTransferRequests.Find(id);
            if (transfer == null)
                return HttpNotFound();

            // Optionally include more related info
            var file = transfer.AttachmentFileID.HasValue ? db.TransferRequestFiles.Find(transfer.AttachmentFileID.Value) : null;

            var model = new StudentTransferPreviewVM
            {
                CurrentStream = transfer.OldStream,
                CurrentGrade = transfer.OldGrade,
                CurrentSubjects = db.GradeSubjects.Where(gs => gs.GradeID == transfer.OldGrade && gs.Stream.StreamName == transfer.OldStream).Select(gs => gs.Subject.SubjectName).ToList(),
                NewStream = transfer.NewStream,
                NewGrade = transfer.NewGrade,
                NewSubjects = db.GradeSubjects.Where(gs => gs.GradeID == transfer.NewGrade && gs.Stream.StreamName == transfer.NewStream).Select(gs => gs.Subject.SubjectName).ToList(),
                Justification = transfer.Justification,
                SupportingDocumentName = file?.FileName
            };

            return View(model);
        }


        // GET: Student/PreviewTransferRequest (Step 2)
        [HttpGet]
        public ActionResult PreviewTransferRequest()
        {
            var preview = Session["TransferPreviewVM"] as StudentTransferPreviewVM;
            if (preview == null)
            {
                TempData["Error"] = "Preview data expired. Please complete the transfer request form again.";
                return RedirectToAction("RequestTransfer");
            }
            return View(preview);
        }

        // POST: Student/ConfirmTransferRequest (final submission)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmTransferRequest()
        {
            var preview = Session["TransferPreviewVM"] as StudentTransferPreviewVM;
            var justification = Session["TransferJustification"] as string;
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);

            if (preview == null || student == null)
                return RedirectToAction("RequestTransfer");

            // Check stream capacity again
            var targetStreamObj = db.Streams.FirstOrDefault(s => s.StreamName == preview.NewStream);
            int enrolled = db.Students.Count(s => s.Grade == student.Grade && s.Stream == preview.NewStream && s.Status == "Active");
            if (enrolled >= targetStreamObj.MaxCapacity)
            {
                TempData["Error"] = "Stream is now full. Try another.";
                return RedirectToAction("RequestTransfer");
            }

            // Handle file upload
            int? fileId = null;
            if (Session["TransferRequestUploadedFileData"] is byte[] fileData && Session["TransferRequestUploadedFileName"] is string fileName)
            {
                var transferFile = new TransferRequestFile
                {
                    FileName = fileName,
                    FileType = "application/pdf",
                    FileContent = fileData,
                    UploadedAt = DateTime.Now,
                    UploadedBy = userID
                };
                db.TransferRequestFiles.Add(transferFile);
                db.SaveChanges();
                fileId = transferFile.FileID;
                // Clear session after saving
                Session.Remove("TransferRequestUploadedFileData");
                Session.Remove("TransferRequestUploadedFileName");
            }

            // Create the transfer request
            var transfer = new StudentTransferRequest
            {
                StudentID = student.StudentID,
                OldStream = preview.CurrentStream,
                NewStream = preview.NewStream,
                OldGrade = preview.CurrentGrade,
                NewGrade = preview.NewGrade,
                Justification = justification,
                AttachmentFileID = fileId,
                Status = "PendingTeacher",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsActive = true
            };
            db.StudentTransferRequests.Add(transfer);
            db.SaveChanges();

            // Audit trail
            db.TransferRequestAuditLogs.Add(new TransferRequestAuditLog
            {
                TransferRequestID = transfer.TransferRequestID,
                Action = "Submitted",
                PerformedBy = userID,
                PerformedByRole = "Student",
                ActionDate = DateTime.Now,
                Comment = justification
            });

            // Notify current teacher (find teacher for student/stream/grade)
            var teacher = db.GradeSubjectTeachers.FirstOrDefault(t =>
                t.GradeID == transfer.OldGrade &&
                t.StreamID == db.Streams.FirstOrDefault(st => st.StreamName == transfer.OldStream).StreamID
            );
            if (teacher != null)
            {
                db.Notifications.Add(new Notification
                {
                    UserID = db.Teachers.Find(teacher.TeacherID).UserID,
                    Title = "New Transfer Request",
                    Message = $"A new transfer request was submitted by {student.FirstName} {student.LastName}.",
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    TransferRequestID = transfer.TransferRequestID
                });
            }
            db.SaveChanges();

            // Clear preview from session after successful submission
            Session.Remove("TransferPreviewVM");
            Session.Remove("TransferJustification");

            TempData["Success"] = "Your transfer request has been submitted!";
            return RedirectToAction("Index"); // Or to a status/thank you page
        }


        // === Utility Methods for Transfer ===
        private bool IsTransferWindowOpen(out string msg)
        {
            msg = "Transfer requests are currently OPEN for testing.";
            return true;
        }

        private string GetStreamDescription(string streamName)
        {
            switch (streamName)
            {
                case "Physical Science": return "Focus on maths, science, and analytical subjects.";
                case "General": return "Flexible, a broad range of subjects for multiple careers.";
                case "Accounting": return "Specialized in commerce, business, and accounting.";
                default: return "";
            }
        }

        private string GenerateDiffHtml(List<string> oldList, List<string> newList)
        {
            // Simple diff: mark additions in green, removals in red
            var removed = oldList.Except(newList).ToList();
            var added = newList.Except(oldList).ToList();
            var html = "<ul>";
            foreach (var s in oldList)
                html += $"<li{(removed.Contains(s) ? " style='color:red'" : "")}>{s}</li>";
            foreach (var s in added)
                html += $"<li style='color:green'>{s} (new)</li>";
            html += "</ul>";
            return html;
        }



        public ActionResult MyTransferRequests()
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);

            var requests = db.StudentTransferRequests
                .Where(r => r.StudentID == student.StudentID)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new StudentTransferTrackingVM
                {
                    TransferRequestID = r.TransferRequestID,
                    OldStream = r.OldStream,
                    NewStream = r.NewStream,
                    OldGrade = r.OldGrade,
                    NewGrade = r.NewGrade,
                    Justification = r.Justification,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                }).ToList();

            return View(requests);
        }


        public ActionResult TrackTransferRequest(int id)
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var student = db.Students.FirstOrDefault(s => s.UserID == userID);

            var request = db.StudentTransferRequests
                .Where(r => r.TransferRequestID == id && r.StudentID == student.StudentID)
                .FirstOrDefault();

            if (request == null)
                return HttpNotFound();

            // Fetch audit log for progress tracking
            var audit = db.TransferRequestAuditLogs
                .Where(a => a.TransferRequestID == request.TransferRequestID)
                .OrderBy(a => a.ActionDate)
                .ToList();

            var model = new StudentTransferStatusDetailVM
            {
                TransferRequestID = request.TransferRequestID,
                OldStream = request.OldStream,
                NewStream = request.NewStream,
                OldGrade = request.OldGrade,
                NewGrade = request.NewGrade,
                Justification = request.Justification,
                Status = request.Status,
                CreatedAt = request.CreatedAt,
                UpdatedAt = request.UpdatedAt,
                AuditTrail = audit
            };

            return View(model);
        }

    }
}
