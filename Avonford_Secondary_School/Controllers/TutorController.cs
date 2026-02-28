using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Avonford_Secondary_School.Models;
using Avonford_Secondary_School.Models.ViewModels;
using System.IO;

namespace Avonford_Secondary_School.Controllers
{
    public class TutorController : Controller
    {
        private HighschoolDbEntities2 db = new HighschoolDbEntities2();

        public ActionResult Index()
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var tutor = db.Tutors.FirstOrDefault(t => t.UserID == userId);
            if (tutor == null) return RedirectToAction("Login", "Account");

            string pic = tutor.ProfilePicture != null && tutor.ProfilePicture.Length > 0
                ? Convert.ToBase64String(tutor.ProfilePicture) : "";

            ViewBag.TutorName = tutor.FirstName + " " + tutor.LastName;
            ViewBag.ProfilePic = pic;
            return View();
        }

        public ActionResult MyClasses()
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var tutor = db.Tutors.FirstOrDefault(t => t.UserID == userId);
            var classes = db.TutorClasses.Where(c => c.TutorID == tutor.TutorID).ToList();

            var vm = classes.Select(c => new TutorClassListViewModel
            {
                TutorClassID = c.TutorClassID,
                ClassName = c.ClassName,
                GradeName = c.Grade.GradeName,
                SubjectName = c.Subject.SubjectName,
                Mode = c.Mode,
                Enrolled = c.TutorClassEnrollments.Count(e => e.IsPaid && e.Status == "Active"),
                Capacity = c.Capacity,
                AnnualFee = c.AnnualFee,
                IsActive = c.IsActive,
                CoverImageBase64 = c.CoverImage != null && c.CoverImage.Length > 0 ? Convert.ToBase64String(c.CoverImage) : "",
                CreatedDate = c.CreatedDate
            }).ToList();

            return View(vm);
        }



        [HttpGet]
        public JsonResult GetSubjectsForGrade(int gradeId)
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var tutor = db.Tutors.FirstOrDefault(t => t.UserID == userId);
            var subjectIds = db.TutorGradeSubjects
                .Where(t => t.TutorID == tutor.TutorID && t.GradeID == gradeId)
                .Select(t => t.SubjectID).Distinct().ToList();

            var subjects = db.Subjects.Where(s => subjectIds.Contains(s.SubjectID))
                .Select(s => new { Value = s.SubjectID, Text = s.SubjectName }).ToList();

            return Json(subjects, JsonRequestBehavior.AllowGet);
        }


        [HttpGet]
        public ActionResult CreateClass()
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var tutor = db.Tutors.FirstOrDefault(t => t.UserID == userId);
            var gradeIds = db.TutorGradeSubjects.Where(t => t.TutorID == tutor.TutorID).Select(t => t.GradeID).Distinct().ToList();

            var model = new TutorClassCreateViewModel
            {
                TutorID = tutor.TutorID,
                TutorGrades = db.Grades.Where(g => gradeIds.Contains(g.GradeID)).Select(g => new SelectListItem
                {
                    Text = g.GradeName,
                    Value = g.GradeID.ToString()
                }).ToList(),
                TutorSubjects = new List<SelectListItem>(), // Empty, will populate via JS
                EnrollmentDeadline = DateTime.Today.AddDays(7)
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateClass(TutorClassCreateViewModel model)
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var tutor = db.Tutors.FirstOrDefault(t => t.UserID == userId);

            var gradeIds = db.TutorGradeSubjects.Where(t => t.TutorID == tutor.TutorID).Select(t => t.GradeID).Distinct().ToList();

            model.TutorGrades = db.Grades.Where(g => gradeIds.Contains(g.GradeID)).Select(g => new SelectListItem
            {
                Text = g.GradeName,
                Value = g.GradeID.ToString()
            }).ToList();

            // For repopulating the subject dropdown for selected grade
            if (model.SelectedGradeID > 0)
            {
                var subjectIds = db.TutorGradeSubjects.Where(t => t.TutorID == tutor.TutorID && t.GradeID == model.SelectedGradeID)
                    .Select(t => t.SubjectID).Distinct().ToList();
                model.TutorSubjects = db.Subjects.Where(s => subjectIds.Contains(s.SubjectID)).Select(s => new SelectListItem
                {
                    Text = s.SubjectName,
                    Value = s.SubjectID.ToString()
                }).ToList();
            }
            else
            {
                model.TutorSubjects = new List<SelectListItem>();
            }

            if (!ModelState.IsValid)
                return View(model);

            if (db.TutorClasses.Any(c => c.TutorID == tutor.TutorID && c.ClassName == model.ClassName))
            {
                ModelState.AddModelError("ClassName", "You already have a class with this name.");
                return View(model);
            }

            byte[] coverImg = null;
            if (model.CoverImage != null && model.CoverImage.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    model.CoverImage.InputStream.CopyTo(ms);
                    coverImg = ms.ToArray();
                }
            }

            var tClass = new TutorClass
            {
                TutorID = tutor.TutorID,
                GradeID = model.SelectedGradeID,
                SubjectID = model.SelectedSubjectID,
                ClassName = model.ClassName,
                ClassDescription = model.ClassDescription,
                Capacity = model.Capacity,
                AnnualFee = model.AnnualFee,
                CoverImage = coverImg,
                Location = model.Location,
                Mode = model.Mode,
                EnrollmentDeadline = model.EnrollmentDeadline,
                EntryRequirements = model.EntryRequirements,
                WhatsAppContact = model.WhatsAppContact,
                ScheduleTemplate = model.ScheduleTemplate,
                ResourcesFolder = null,
                IsActive = true,
                CreatedDate = DateTime.Now
            };
            db.TutorClasses.Add(tClass);
            db.SaveChanges();

            if (Request.Files.Count > 0 && Request.Files.GetMultiple("ClassResources") != null)
            {
                var files = Request.Files.GetMultiple("ClassResources");
                foreach (HttpPostedFileBase file in files)
                {
                    if (file != null && file.ContentLength > 0)
                    {
                        using (var ms = new MemoryStream())
                        {
                            file.InputStream.CopyTo(ms);
                            db.TutorClassResources.Add(new TutorClassResource
                            {
                                TutorClassID = tClass.TutorClassID,
                                FileName = Path.GetFileName(file.FileName),
                                FileType = file.ContentType,
                                FileContent = ms.ToArray(),
                                UploadedBy = tutor.UserID,
                                UploadedDate = DateTime.Now
                            });
                        }
                    }
                }
                db.SaveChanges();
            }

            return RedirectToAction("ConfirmClass", new { id = tClass.TutorClassID });
        }

        [HttpPost]
        public ActionResult DeactivateClass(int id)
        {
            var c = db.TutorClasses.Find(id);
            if (c != null)
            {
                c.IsActive = false;
                db.SaveChanges();
            }
            return RedirectToAction("MyClasses");
        }

        public ActionResult ConfirmClass(int id)
        {
            var c = db.TutorClasses.Find(id);
            var grade = db.Grades.Find(c.GradeID);
            var subj = db.Subjects.Find(c.SubjectID);
            var resources = db.TutorClassResources.Where(r => r.TutorClassID == id).ToList();

            var model = new TutorClassConfirmViewModel
            {
                TutorID = c.TutorID,
                SelectedGradeID = c.GradeID,
                SelectedSubjectID = c.SubjectID,
                ClassName = c.ClassName,
                ClassDescription = c.ClassDescription,
                Capacity = c.Capacity,
                AnnualFee = c.AnnualFee,
                CoverImageBase64 = c.CoverImage != null && c.CoverImage.Length > 0 ? Convert.ToBase64String(c.CoverImage) : "",
                Location = c.Location,
                Mode = c.Mode,
                EnrollmentDeadline = c.EnrollmentDeadline,
                EntryRequirements = c.EntryRequirements,
                WhatsAppContact = c.WhatsAppContact,
                ScheduleTemplate = c.ScheduleTemplate,
                ResourceNames = resources.Select(r => r.FileName).ToList(),
                GradeName = grade.GradeName,
                SubjectName = subj.SubjectName
            };
            return View(model);
        }

        public ActionResult ClassDetails(int id)
        {
            var c = db.TutorClasses.Find(id);
            var grade = db.Grades.Find(c.GradeID);
            var subj = db.Subjects.Find(c.SubjectID);

            var resources = c.TutorClassResources
                .OrderByDescending(r => r.UploadedDate)
                .Select(r => new TutorClassResourceViewModel
                {
                    ResourceID = r.ResourceID,
                    FileName = r.FileName,
                    FileType = r.FileType,
                    UploadedDate = r.UploadedDate
                }).ToList();

            var enrollments = c.TutorClassEnrollments.Select(e => new TutorClassEnrollmentViewModel
            {
                EnrollmentID = e.EnrollmentID,
                StudentName = e.StudentID != 0 ? (db.Students.Where(s => s.StudentID == e.StudentID).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault() ?? "N/A") : "N/A",
                StudentEmail = e.StudentID != 0 ? (db.Students.Where(s => s.StudentID == e.StudentID).Select(s => s.ParentEmail).FirstOrDefault() ?? "N/A") : "N/A",
                IsPaid = e.IsPaid,
                Status = e.Status,
                EnrollmentDate = e.EnrollmentDate
            }).ToList();

            var model = new TutorClassDetailViewModel
            {
                TutorClassID = c.TutorClassID,
                ClassName = c.ClassName,
                ClassDescription = c.ClassDescription,
                GradeName = grade.GradeName,
                SubjectName = subj.SubjectName,
                Mode = c.Mode,
                Location = c.Location,
                WhatsAppContact = c.WhatsAppContact,
                EnrollmentDeadline = c.EnrollmentDeadline,
                EntryRequirements = c.EntryRequirements,
                Capacity = c.Capacity,
                Enrolled = enrollments.Count(x => x.IsPaid && x.Status == "Active"),
                AnnualFee = c.AnnualFee,
                IsActive = c.IsActive,
                CoverImageBase64 = c.CoverImage != null && c.CoverImage.Length > 0 ? Convert.ToBase64String(c.CoverImage) : "",
                ScheduleTemplate = c.ScheduleTemplate,
                Resources = resources,
                Enrollments = enrollments
            };
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddResource(int classId, HttpPostedFileBase resourceFile)
        {
            if (resourceFile != null && resourceFile.ContentLength > 0 && Path.GetExtension(resourceFile.FileName).ToLower() == ".pdf")
            {
                var tclass = db.TutorClasses.Find(classId);
                if (tclass != null)
                {
                    using (var ms = new MemoryStream())
                    {
                        resourceFile.InputStream.CopyTo(ms);
                        db.TutorClassResources.Add(new TutorClassResource
                        {
                            TutorClassID = classId,
                            FileName = Path.GetFileName(resourceFile.FileName),
                            FileType = resourceFile.ContentType,
                            FileContent = ms.ToArray(),
                            UploadedBy = Convert.ToInt32(Session["UserID"]),
                            UploadedDate = DateTime.Now
                        });
                        db.SaveChanges();
                    }
                }
            }
            return RedirectToAction("ClassDetails", new { id = classId });
        }

        public ActionResult ViewResource(int id)
        {
            var resource = db.TutorClassResources.Find(id);
            if (resource == null) return HttpNotFound();
            return File(resource.FileContent, "application/pdf", resource.FileName);
        }

        [HttpPost]
        public ActionResult DeleteResource(int id, int classId)
        {
            var resource = db.TutorClassResources.Find(id);
            if (resource != null)
            {
                db.TutorClassResources.Remove(resource);
                db.SaveChanges();
            }
            return RedirectToAction("ClassDetails", new { id = classId });
        }



        [HttpPost]
        public ActionResult UpdateClassStatus(int id, bool isActive)
        {
            var c = db.TutorClasses.Find(id);
            if (c != null)
            {
                c.IsActive = isActive;
                db.SaveChanges();
            }
            return Json(new { success = true, status = c.IsActive ? "Active" : "Inactive" });
        }



        public ActionResult ManageSessions(int classId)
        {
            var c = db.TutorClasses.Find(classId);
            var now = DateTime.Now;
            var upcoming = c.TutorSessions
                .Where(s => !s.IsCancelled && s.SessionDate >= now.Date)
                .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
                .Select(s => new TutorSessionViewModel
                {
                    TutorSessionID = s.TutorSessionID,
                    Title = s.Title,
                    Description = s.Description,
                    Mode = s.Mode,
                    SessionDate = s.SessionDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Location = s.Location,
                    OnlineMeetingLink = s.OnlineMeetingLink,
                    IsCancelled = s.IsCancelled,
                    Resources = db.TutorClassResources
                        .Where(r => r.TutorClassID == s.TutorClassID && r.Description.Contains("Session: " + s.Title))
                        .Select(r => new TutorSessionResourceVM
                        {
                            ResourceID = r.ResourceID,
                            FileName = r.FileName
                        }).ToList()
                }).ToList();

            var vm = new TutorSessionListViewModel
            {
                TutorClassID = classId,
                ClassName = c.ClassName,
                GradeName = c.Grade.GradeName,
                SubjectName = c.Subject.SubjectName,
                Sessions = upcoming
            };
            return View(vm);
        }

        [HttpGet]
        public ActionResult ScheduleSession(int classId)
        {
            var c = db.TutorClasses.Find(classId);
            var vm = new TutorSessionCreateViewModel
            {
                TutorClassID = c.TutorClassID,
                ClassName = c.ClassName,
                Mode = c.Mode,
                ModeOptions = new List<SelectListItem>
        {
            new SelectListItem { Text = "Online", Value = "Online" },
            new SelectListItem { Text = "Physical", Value = "Physical" }
        },
                SessionDate = DateTime.Today,
                StartTime = new TimeSpan(16, 0, 0),
                EndTime = new TimeSpan(17, 0, 0)
            };
            return View(vm);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ScheduleSession(TutorSessionCreateViewModel model, IEnumerable<HttpPostedFileBase> SessionResources)
        {
            var c = db.TutorClasses.Find(model.TutorClassID);

            // For non-blended classes, ensure model.Mode is set (it will come from hidden field)
            if (c.Mode != "Blended")
                model.Mode = c.Mode;

            if (!ModelState.IsValid)
            {
                model.Mode = c.Mode;
                model.ClassName = c.ClassName;
                model.ModeOptions = new List<SelectListItem>
        {
            new SelectListItem { Text = "Online", Value = "Online" },
            new SelectListItem { Text = "Physical", Value = "Physical" }
        };
                return View(model);
            }

            var overlap = db.TutorSessions.Any(s =>
                s.TutorClassID == c.TutorClassID &&
                s.SessionDate == model.SessionDate &&
                ((model.StartTime >= s.StartTime && model.StartTime < s.EndTime) ||
                 (model.EndTime > s.StartTime && model.EndTime <= s.EndTime))
                && !s.IsCancelled
            );
            if (overlap)
            {
                ModelState.AddModelError("", "There is already a session scheduled at this time.");
                model.Mode = c.Mode;
                model.ClassName = c.ClassName;
                model.ModeOptions = new List<SelectListItem>
        {
            new SelectListItem { Text = "Online", Value = "Online" },
            new SelectListItem { Text = "Physical", Value = "Physical" }
        };
                return View(model);
            }

            string location = null;
            string meetingLink = null;

            // Use the actual posted mode value from model.Mode
            if (model.Mode == "Online")
            {
                meetingLink = model.OnlineMeetingLink;
            }
            else if (model.Mode == "Physical")
            {
                location = model.Location;
            }
            else if (model.Mode == "Blended")
            {
                if (!string.IsNullOrWhiteSpace(model.OnlineMeetingLink))
                    meetingLink = model.OnlineMeetingLink;
                if (!string.IsNullOrWhiteSpace(model.Location))
                    location = model.Location;
            }

            var session = new TutorSession
            {
                TutorClassID = c.TutorClassID,
                TutorID = c.TutorID,
                Title = model.Title,
                Description = model.Description,
                SubjectID = c.SubjectID,
                GradeID = c.GradeID,
                SessionDate = model.SessionDate,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                Location = location,
                OnlineMeetingLink = meetingLink,
                Mode = model.Mode, // Always use the posted value
                Resources = null,
                IsCancelled = false,
                CreatedDate = DateTime.Now
            };
            db.TutorSessions.Add(session);
            db.SaveChanges();

            if (SessionResources != null)
            {
                foreach (var file in SessionResources)
                {
                    if (file != null && file.ContentLength > 0)
                    {
                        using (var ms = new MemoryStream())
                        {
                            file.InputStream.CopyTo(ms);
                            db.TutorClassResources.Add(new TutorClassResource
                            {
                                TutorClassID = c.TutorClassID,
                                FileName = Path.GetFileName(file.FileName),
                                FileType = file.ContentType,
                                FileContent = ms.ToArray(),
                                UploadedBy = Convert.ToInt32(Session["UserID"]),
                                UploadedDate = DateTime.Now,
                                Description = $"Session: {model.Title}"
                            });
                        }
                    }
                }
                db.SaveChanges();
            }

            var paidStudentIds = c.TutorClassEnrollments.Where(e => e.IsPaid && e.Status == "Active").Select(e => e.StudentID).ToList();
            foreach (var studentId in paidStudentIds)
            {
                db.SessionReminders.Add(new SessionReminder
                {
                    StudentID = studentId,
                    TutorSessionID = session.TutorSessionID,
                    ReminderSent = false,
                    ReminderSentTime = null
                });
            }
            db.SaveChanges();

            return RedirectToAction("ConfirmSession", new { id = session.TutorSessionID });
        }


        public ActionResult ConfirmSession(int id)
        {
            var s = db.TutorSessions.Find(id);
            var c = db.TutorClasses.Find(s.TutorClassID);

            var vm = new TutorSessionViewModel
            {
                TutorSessionID = s.TutorSessionID,
                TutorClassID = s.TutorClassID ?? 0,
                Title = s.Title,
                Description = s.Description,
                Mode = s.Mode,
                SessionDate = s.SessionDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Location = s.Location,
                OnlineMeetingLink = s.OnlineMeetingLink,
                IsCancelled = s.IsCancelled,
                Resources = db.TutorClassResources
                    .Where(r => r.TutorClassID == c.TutorClassID && r.Description.Contains("Session: " + s.Title))
                    .Select(r => new TutorSessionResourceVM
                    {
                        ResourceID = r.ResourceID,
                        FileName = r.FileName
                    }).ToList()
            };
            return View(vm);
        }

        public ActionResult SessionDetails(int id)
        {
            var s = db.TutorSessions.Find(id);
            var c = db.TutorClasses.Find(s.TutorClassID);

            var vm = new TutorSessionViewModel
            {
                TutorSessionID = s.TutorSessionID,
                TutorClassID = s.TutorClassID ?? 0,
                Title = s.Title,
                Description = s.Description,
                Mode = s.Mode,
                SessionDate = s.SessionDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Location = s.Location,
                OnlineMeetingLink = s.OnlineMeetingLink,
                IsCancelled = s.IsCancelled,
                Resources = db.TutorClassResources
                    .Where(r => r.TutorClassID == c.TutorClassID && r.Description.Contains("Session: " + s.Title))
                    .Select(r => new TutorSessionResourceVM
                    {
                        ResourceID = r.ResourceID,
                        FileName = r.FileName
                    }).ToList()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RescheduleSession(int sessionId, DateTime newDate, TimeSpan newStartTime, TimeSpan newEndTime)
        {
            var s = db.TutorSessions.Find(sessionId);
            if (s == null || s.IsCancelled) return HttpNotFound();

            var c = db.TutorClasses.Find(s.TutorClassID);
            var oldDate = s.SessionDate;
            var oldStart = s.StartTime;
            var oldEnd = s.EndTime;

            s.SessionDate = newDate;
            s.StartTime = newStartTime;
            s.EndTime = newEndTime;
            db.SaveChanges();

            // Notify all enrolled and paid students
            var studentIds = c.TutorClassEnrollments.Where(e => e.IsPaid && e.Status == "Active").Select(e => e.StudentID).ToList();
            var userIds = db.Students.Where(st => studentIds.Contains(st.StudentID)).Select(st => st.UserID).ToList();

            foreach (var userId in userIds)
            {
                db.Notifications.Add(new Notification
                {
                    UserID = userId,
                    Title = "Session Rescheduled",
                    Message = $"The session \"{s.Title}\" for class \"{c.ClassName}\" has been rescheduled to {s.SessionDate:yyyy-MM-dd} from {s.StartTime:hh\\:mm} to {s.EndTime:hh\\:mm}.",
                    RelatedSessionID = s.TutorSessionID,
                    CreatedDate = DateTime.Now,
                    IsRead = false
                });
            }
            db.SaveChanges();

            return RedirectToAction("SessionDetails", new { id = s.TutorSessionID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelSession(int id)
        {
            var session = db.TutorSessions.Find(id);
            if (session != null)
            {
                session.IsCancelled = true;
                db.SaveChanges();

                var c = db.TutorClasses.Find(session.TutorClassID);
                var studentIds = c.TutorClassEnrollments.Where(e => e.IsPaid && e.Status == "Active").Select(e => e.StudentID).ToList();
                var userIds = db.Students.Where(st => studentIds.Contains(st.StudentID)).Select(st => st.UserID).ToList();

                foreach (var userId in userIds)
                {
                    db.Notifications.Add(new Notification
                    {
                        UserID = userId,
                        Title = "Session Cancelled",
                        Message = $"The session \"{session.Title}\" for class \"{c.ClassName}\" scheduled on {session.SessionDate:yyyy-MM-dd} has been cancelled.",
                        RelatedSessionID = session.TutorSessionID,
                        CreatedDate = DateTime.Now,
                        IsRead = false
                    });
                }
                db.SaveChanges();
            }
            return RedirectToAction("ManageSessions", new { classId = session.TutorClassID });
        }


        public ActionResult UpcomingSessions()
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var tutor = db.Tutors.FirstOrDefault(t => t.UserID == userId);

            var upcomingSessions = db.TutorSessions
                .Where(s => s.TutorID == tutor.TutorID && !s.IsCancelled && s.SessionDate >= DateTime.Today)
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
                    OnlineMeetingLink = s.OnlineMeetingLink
                })
                .ToList();

            return View(upcomingSessions);
        }

        public ActionResult ScanAttendance()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ConfirmAttendanceFromQR(string qrData)
        {
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(qrData);
            int studentId = (int)parsed.StudentID;
            int sessionId = (int)parsed.SessionID; // extract from QR code!
            var s = db.TutorSessions.Find(sessionId);

            if (s == null)
                return Json(new { success = false, message = "Session not found." });

            var exists = db.SessionAttendances.Any(a => a.TutorSessionID == sessionId && a.StudentID == studentId && a.AttendanceStatus == "Present");
            if (!exists)
            {
                db.SessionAttendances.Add(new SessionAttendance
                {
                    TutorSessionID = sessionId,
                    StudentID = studentId,
                    TutorID = s.TutorID,
                    CheckInTime = DateTime.Now,
                    AttendanceStatus = "Present"
                });
                db.SaveChanges();
            }
            return Json(new { success = true });
        }


        public ActionResult AttendanceHistory(int sessionId)
        {
            var attendance = db.SessionAttendances
                .Where(a => a.TutorSessionID == sessionId)
                .Select(a => new
                {
                    a.StudentID,
                    StudentName = a.Student.FirstName + " " + a.Student.LastName,
                    a.CheckInTime,
                    a.AttendanceStatus
                }).ToList();

            return View(attendance);
        }





        public ActionResult PrivateSessionRequests()
        {
            int userId = Convert.ToInt32(Session["UserID"]);
            var tutor = db.Tutors.FirstOrDefault(t => t.UserID == userId);

            var requests = db.TutorPrivateBookings
                .Where(r => r.TutorID == tutor.TutorID && r.Status == "Pending")
                .OrderBy(r => r.StartDateTime)
                .Select(r => new PrivateSessionRequestVM
                {
                    RequestID = r.BookingID, // Use BookingID here
            StudentName = r.Student.FirstName + " " + r.Student.LastName,
                    SubjectName = r.Subject.SubjectName,
                    StartTime = r.StartDateTime,    // Use StartDateTime
            EndTime = r.EndDateTime,        // Use EndDateTime
            TopicMessage = r.Topic
                }).ToList();

            return View(requests);
        }

        [HttpGet]
        public ActionResult ReviewSessionRequest(int requestId)
        {
            var request = db.TutorPrivateBookings.Find(requestId);
            if (request == null || request.Status != "Pending")
                return HttpNotFound();

            var vm = new TutorReviewSessionRequestVM
            {
                RequestID = request.BookingID, // Use BookingID
                StudentName = request.Student.FirstName + " " + request.Student.LastName,
                SubjectName = request.Subject.SubjectName,
                StartTime = request.StartDateTime,
                EndTime = request.EndDateTime,
                TopicMessage = request.Topic
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptSessionRequest(int requestId)
        {
            var request = db.TutorPrivateBookings.Find(requestId);
            if (request == null || request.Status != "Pending")
                return HttpNotFound();

            request.Status = "Accepted";
            //request.TutorResponse = "Accepted";
            request.AcceptedAt = DateTime.Now;
            db.SaveChanges();

            // Notify student
            db.Notifications.Add(new Notification
            {
                UserID = request.Student.UserID,
                Title = "Private Session Approved",
                Message = $"Your private session request (ID {request.BookingID}) with {request.Tutor.FirstName} {request.Tutor.LastName} was approved for {request.StartDateTime:yyyy-MM-dd HH:mm}.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });
            db.SaveChanges();

            TempData["Success"] = "Session approved and student notified!";
            return RedirectToAction("PrivateSessionRequests");
        }

        [HttpGet]
        public ActionResult RejectSessionRequest(int requestId)
        {
            var request = db.TutorPrivateBookings.Find(requestId);
            if (request == null || request.Status != "Pending")
                return HttpNotFound();

            var vm = new TutorRejectSessionRequestVM
            {
                RequestID = request.BookingID,
                StudentName = request.Student.FirstName + " " + request.Student.LastName,
                SubjectName = request.Subject.SubjectName,
                StartTime = request.StartDateTime,
                EndTime = request.EndDateTime,
                TopicMessage = request.Topic
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectSessionRequest(TutorRejectSessionRequestVM model)
        {
            var request = db.TutorPrivateBookings.Find(model.RequestID);
            if (request == null || request.Status != "Pending")
                return HttpNotFound();

            if (string.IsNullOrWhiteSpace(model.RejectReason))
            {
                ModelState.AddModelError("RejectReason", "Please provide a rejection reason.");
                // Re-populate VM and return view
                model.StudentName = request.Student.FirstName + " " + request.Student.LastName;
                model.SubjectName = request.Subject.SubjectName;
                model.StartTime = request.StartDateTime;
                model.EndTime = request.EndDateTime;
                model.TopicMessage = request.Topic;
                return View(model);
            }

            request.Status = "Rejected";
            //request.TutorResponse = model.RejectReason;
            request.RejectedAt = DateTime.Now;
            db.SaveChanges();

            // Notify student
            db.Notifications.Add(new Notification
            {
                UserID = request.Student.UserID,
                Title = "Private Session Request Rejected",
                Message = $"We are sorry to inform you, but your booking (ID {request.BookingID}) was rejected for the following reason: {model.RejectReason}",
                CreatedDate = DateTime.Now,
                IsRead = false
            });
            db.SaveChanges();

            TempData["Success"] = "Session rejected and student notified!";
            return RedirectToAction("PrivateSessionRequests");
        }



    }
}
