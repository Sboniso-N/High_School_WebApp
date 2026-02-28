using Avonford_Secondary_School.Models;
using Avonford_Secondary_School.Models.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Globalization;
using System.Collections.Generic;
using static Avonford_Secondary_School.Models.ViewModels.BorrowVMs;
using static Avonford_Secondary_School.Models.ViewModels.ReturnVMs;



namespace Avonford_Secondary_School.Controllers
{
   
    public class AdminController : Controller
    {
        private HighschoolDbEntities2 db = new HighschoolDbEntities2();

        private const string FROM_EMAIL = "kamvelihlembuthuma23@gmail.com";
        private const string FROM_PASSWORD = "jysuoddhkaqfqoeq";

        #region Admin Home / Dashboard

        public ActionResult Index()
        {
            var model = new AdminIndexViewModel
            {
                TotalPending = db.Applications.Count(a => a.ApplicationStatus == "Pending"),
                TotalApproved = db.Applications.Count(a => a.ApplicationStatus == "Approved"),
                TotalRejected = db.Applications.Count(a => a.ApplicationStatus == "Rejected")
            };
            return View(model);
        }

        #endregion

        #region Pending Applications (Review, View, Admit, Reject)

        public ActionResult PendingApplications()
        {
            var pendingApps = db.Applications
                                .Where(a => a.ApplicationStatus == "Pending")
                                .OrderByDescending(a => a.CreatedDate)
                                .Select(a => new PendingApplicationsItem
                                {
                                    ApplicationID = a.ApplicationID,
                                    FullName = a.FirstName + " " + a.LastName,
                                    SelectedGrade = a.SelectedGrade,
                                    CreatedDate = a.CreatedDate,
                                    ApplicationStatus = a.ApplicationStatus
                                })
                                .ToList();

            var model = new PendingApplicationsViewModel
            {
                PendingList = pendingApps
            };

            return View(model);
        }

        [HttpGet]
        public ActionResult ViewApplication(int id)
        {
            var application = db.Applications.Find(id);
            if (application == null)
            {
                return HttpNotFound("Application not found.");
            }

            // Calculate capacity using the selected stream for Grades 8 & 9.
            int currentEnrolled = 0;
            int maxCapacity = 0;
            bool isFull = false;

            if (application.SelectedGrade == 8 || application.SelectedGrade == 9)
            {
                // For Grade 8 & 9, count only students in the same stream
                currentEnrolled = db.Students.Count(s => s.Grade == application.SelectedGrade && s.Stream == application.SelectedStream);
                var streamInfo = db.Streams.FirstOrDefault(st => st.StreamName == application.SelectedStream);
                if (streamInfo != null)
                {
                    maxCapacity = streamInfo.MaxCapacity;
                    if (currentEnrolled >= streamInfo.MaxCapacity)
                        isFull = true;
                }
            }
            else
            {
                currentEnrolled = db.Students.Count(s => s.Grade == application.SelectedGrade && s.Stream == application.SelectedStream);
                var streamInfo = db.Streams.FirstOrDefault(st => st.StreamName == application.SelectedStream);
                if (streamInfo != null)
                {
                    maxCapacity = streamInfo.MaxCapacity;
                    if (currentEnrolled >= streamInfo.MaxCapacity)
                        isFull = true;
                }
            }

            var model = new ApplicationDetailsViewModel
            {
                ApplicationID = application.ApplicationID,
                UniqueAppRef = application.UniqueAppRef,
                FirstName = application.FirstName,
                LastName = application.LastName,
                NationalityID = application.NationalityID,
                DateOfBirth = application.DateOfBirth,
                ParentName = application.ParentName,
                ParentID = application.ParentID,
                ParentEmail = application.ParentEmail,
                ParentContact = application.ParentContact,
                SelectedGrade = application.SelectedGrade,
                SelectedStream = application.SelectedStream,
                ApplicationStatus = application.ApplicationStatus,
                CreatedDate = application.CreatedDate,
                StudentIDDoc = application.StudentIDDoc,
                ParentIDDoc = application.ParentIDDoc,
                PreviousReportCard = application.PreviousReportCard,
                ApplicationForm = application.ApplicationForm,
                CurrentEnrolled = currentEnrolled,
                MaxCapacity = maxCapacity,
                IsCapacityFull = isFull
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AdmitApplication(ApplicationDetailsViewModel model)
        {
            var application = db.Applications.Find(model.ApplicationID);
            if (application == null) return HttpNotFound("Application not found.");

            if (model.IsCapacityFull)
            {
                // capacity is full
                ModelState.AddModelError("", "No space available for this grade/stream.");
                return RedirectToAction("ViewApplication", new { id = model.ApplicationID });
            }

            // Check checkboxes
            if (!model.MeetsGradeCriteria || !model.IdentityVerified || !model.NoConcerns)
            {
                ModelState.AddModelError("", "All criteria must be met to admit this student.");
                return RedirectToAction("ViewApplication", new { id = model.ApplicationID });
            }

            // Approve
            application.ApplicationStatus = "Approved";
            application.ApprovedDate = DateTime.Now;
            db.SaveChanges();

            // Create user
            string defaultPassword = "Admin@2004"; // or hashed
            var hashed = HashPassword(defaultPassword);

            var newUser = new User
            {
                Email = application.ParentEmail,
                PasswordHash = hashed,
                Role = "Student",
                IsActive = true,
                DateCreated = DateTime.Now
            };
            db.Users.Add(newUser);
            db.SaveChanges();

            // Create student
            var newStudent = new Student
            {
                UserID = newUser.UserID,
                FirstName = application.FirstName,
                LastName = application.LastName,
                NationalityID = application.NationalityID,
                Grade = application.SelectedGrade,
                Stream = application.SelectedStream,
                Status = "Inactive", // fees not paid
                DateOfBirth = application.DateOfBirth,
                ParentName = application.ParentName,
                ParentID = application.ParentID,
                ParentEmail = application.ParentEmail,
                ParentContact = application.ParentContact
            };
            db.Students.Add(newStudent);
            db.SaveChanges();

            // Email
            SendApprovalEmail(application.ParentEmail, application.FirstName, defaultPassword);

            return RedirectToAction("PendingApplications");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectApplication(int applicationId)
        {
            var application = db.Applications.Find(applicationId);
            if (application == null) return HttpNotFound("Application not found.");

            application.ApplicationStatus = "Rejected";
            application.RejectedDate = DateTime.Now;
            db.SaveChanges();

            SendRejectionEmail(application.ParentEmail, application.FirstName);

            return RedirectToAction("PendingApplications");
        }

        #endregion




        [HttpGet]
        public ActionResult ViewDocument(int id, string docType)
        {
            // Find the application by id
            var application = db.Applications.Find(id);
            if (application == null)
            {
                return HttpNotFound("Application not found.");
            }

            byte[] fileBytes = null;
            string fileName = "document.pdf";
            string contentType = "application/pdf";

            switch (docType)
            {
                case "StudentIDDoc":
                    fileBytes = application.StudentIDDoc;
                    fileName = "StudentID.pdf";
                    break;
                case "ParentIDDoc":
                    fileBytes = application.ParentIDDoc;
                    fileName = "ParentID.pdf";
                    break;
                case "PreviousReportCard":
                    fileBytes = application.PreviousReportCard;
                    fileName = "ReportCard.pdf";
                    break;
                case "ApplicationForm":
                    fileBytes = application.ApplicationForm;
                    fileName = "ApplicationForm.pdf";
                    break;
                default:
                    return HttpNotFound("Document type not found.");
            }

            // Set the Content-Disposition header to inline so the document is shown in the browser
            Response.AppendHeader("Content-Disposition", "inline; filename=" + fileName);
            return File(fileBytes, contentType);
        }





        #region Teacher Registration
        [HttpGet]
        public ActionResult RegisterTeacher()
        {
            var model = new RegisterTeacherViewModel();
            // Populate AvailableSubjects list from your Subjects table
            model.AvailableSubjects = db.Subjects.Select(s => new SelectListItem
            {
                Value = s.SubjectID.ToString(),
                Text = s.SubjectName
            }).ToList();
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegisterTeacher(RegisterTeacherViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Create User with Role=Teacher
            string defaultPassword = "Admin@2004";
            var hashed = HashPassword(defaultPassword);

            var newUser = new User
            {
                Email = model.Email,
                PasswordHash = hashed,
                Role = "Teacher",
                IsActive = true,
                DateCreated = DateTime.Now
            };
            db.Users.Add(newUser);
            db.SaveChanges();

            // Convert profile picture to byte[] if uploaded
            byte[] profilePicBytes = null;
            if (model.ProfilePicture != null && model.ProfilePicture.ContentLength > 0)
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    model.ProfilePicture.InputStream.CopyTo(ms);
                    profilePicBytes = ms.ToArray();
                }
            }

            // Create Teacher record, including the subject fields
            var newTeacher = new Teacher
            {
                UserID = newUser.UserID,
                FirstName = model.FirstName,
                LastName = model.LastName,
                OtherDetails = model.ContactNumber,
                ProfilePicture = profilePicBytes,
                // Assign subject IDs. Note: if SubjectID2/3 are not provided, they remain null.
                SubjectID1 = model.SubjectID1,
                SubjectID2 = model.SubjectID2,
                SubjectID3 = model.SubjectID3
            };
            db.Teachers.Add(newTeacher);
            db.SaveChanges();

            // Optionally, send a welcome/credentials email.
            // SendTeacherWelcomeEmail(model.Email, defaultPassword);

            return RedirectToAction("Index");
        }


        #endregion

        #region Teacher Allocation
        [HttpGet]
        public ActionResult AllocateTeacher(int selectedGrade = 8, int? selectedStreamID = null)
        {
            var vm = new TeacherAllocationViewModel
            {
                SelectedGrade = selectedGrade,
                SelectedStreamID = selectedStreamID
            };

            // For Grades 8 & 9, populate stream dropdown with only A, B, and C.
            if (selectedGrade == 8 || selectedGrade == 9)
            {
                var streams = db.Streams.Where(st => st.StreamName == "A" || st.StreamName == "B" || st.StreamName == "C").ToList();
                foreach (var s in streams)
                {
                    vm.Streams.Add(new TeacherAllocationViewModel.StreamItem
                    {
                        StreamID = s.StreamID,
                        StreamName = s.StreamName
                    });
                }
                if (vm.SelectedStreamID == null && vm.Streams.Count > 0)
                {
                    vm.SelectedStreamID = vm.Streams.First().StreamID;
                }
            }
            else if (selectedGrade >= 10)
            {
                // For Grades 10-12, populate with the designated streams (e.g., Physical Science, General, Accounting)
                var streams = db.Streams.Where(st => st.StreamName != "A" && st.StreamName != "B" && st.StreamName != "C").ToList();
                foreach (var s in streams)
                {
                    vm.Streams.Add(new TeacherAllocationViewModel.StreamItem
                    {
                        StreamID = s.StreamID,
                        StreamName = s.StreamName
                    });
                }
                if (vm.SelectedStreamID == null && vm.Streams.Count > 0)
                {
                    vm.SelectedStreamID = vm.Streams.First().StreamID;
                }
            }

            // Gather subjects from GradeSubjects using the selected grade and stream.
            var query = db.GradeSubjects.Where(gs => gs.GradeID == selectedGrade);
            if (vm.SelectedStreamID.HasValue)
            {
                query = query.Where(gs => gs.StreamID == vm.SelectedStreamID.Value);
            }
            var subjects = query.ToList();

            // For each subject, check for an existing GradeSubjectTeacher row and check if the assigned teacher is qualified.
            foreach (var sub in subjects)
            {
                // Look up any existing allocation.
                var gst = db.GradeSubjectTeachers.FirstOrDefault(g =>
                    g.GradeID == sub.GradeID &&
                    g.StreamID == sub.StreamID &&
                    g.SubjectID == sub.SubjectID);

                int? teacherId = gst?.TeacherID;
                string teacherName = "None";

                if (teacherId.HasValue)
                {
                    var teacherObj = db.Teachers.Find(teacherId.Value);
                    if (teacherObj != null)
                    {
                        // Verify that the teacher is qualified for this subject.
                        if (teacherObj.SubjectID1 == sub.SubjectID || teacherObj.SubjectID2 == sub.SubjectID || teacherObj.SubjectID3 == sub.SubjectID)
                        {
                            teacherName = teacherObj.FirstName + " " + teacherObj.LastName;
                        }
                        else
                        {
                            teacherName = "None"; // Teacher not qualified; force re-allocation.
                        }
                    }
                }

                var subjectName = db.Subjects.Find(sub.SubjectID)?.SubjectName ?? "Unknown";

                vm.Subjects.Add(new SubjectAllocationItem
                {
                    GradeSubjectTeacherID = gst?.GradeSubjectTeacherID ?? 0,
                    GradeID = sub.GradeID,
                    StreamID = sub.StreamID,
                    SubjectID = sub.SubjectID,
                    SubjectName = subjectName,
                    TeacherID = teacherId,
                    TeacherName = teacherName
                });
            }

            return View(vm);
        }



        [HttpGet]
        public ActionResult ChangeTeacher(int gradeSubjectTeacherID, int gradeID, int? streamID, int subjectID)
        {
            // Retrieve subject info.
            var subject = db.Subjects.Find(subjectID);
            var subjectName = subject != null ? subject.SubjectName : "Unknown";
            string streamName = null;
            if (streamID.HasValue)
            {
                var st = db.Streams.Find(streamID.Value);
                if (st != null)
                    streamName = st.StreamName;
            }

            var vm = new ChangeTeacherViewModel
            {
                GradeSubjectTeacherID = gradeSubjectTeacherID,
                GradeID = gradeID,
                StreamID = streamID,
                SubjectID = subjectID,
                SubjectName = subjectName,
                StreamName = streamName
            };

            // Build teacher dropdown: only include teachers qualified in the selected subject.
            var qualifiedTeachers = db.Teachers.Where(t =>
                 t.SubjectID1 == subjectID || t.SubjectID2 == subjectID || t.SubjectID3 == subjectID)
                 .ToList();

            vm.TeacherList.Add(new SelectListItem { Text = "No Teacher", Value = "" });
            foreach (var t in qualifiedTeachers)
            {
                vm.TeacherList.Add(new SelectListItem
                {
                    Value = t.TeacherID.ToString(),
                    Text = t.FirstName + " " + t.LastName
                });
            }

            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeTeacher(ChangeTeacherViewModel model)
        {
            // Parse the SelectedTeacherID if provided.
            int? teacherID = null;
            if (model.SelectedTeacherID.HasValue && model.SelectedTeacherID.Value > 0)
            {
                teacherID = model.SelectedTeacherID.Value;
            }

            // Look for an existing allocation record.
            GradeSubjectTeacher gst = null;
            if (model.GradeSubjectTeacherID > 0)
            {
                gst = db.GradeSubjectTeachers.Find(model.GradeSubjectTeacherID);
            }
            else
            {
                gst = db.GradeSubjectTeachers.FirstOrDefault(g =>
                    g.GradeID == model.GradeID &&
                    g.StreamID == model.StreamID &&
                    g.SubjectID == model.SubjectID);
                if (gst == null)
                {
                    gst = new GradeSubjectTeacher
                    {
                        GradeID = model.GradeID,
                        StreamID = model.StreamID,
                        SubjectID = model.SubjectID
                    };
                    db.GradeSubjectTeachers.Add(gst);
                    db.SaveChanges();
                }
            }

            // Update the teacher assignment.
            gst.TeacherID = teacherID;
            db.SaveChanges();

            // Insert a history record.
            var historyRecord = new TeacherAllocationHistory
            {
                TeacherID = teacherID ?? 0, // Consider handling the zero-case appropriately.
                GradeID = model.GradeID,
                StreamID = model.StreamID ?? 0,
                SubjectID = model.SubjectID,
                AllocationDate = DateTime.Now,
                AllocatedBy = Session["UserID"] != null ? (int)Session["UserID"] : 0
            };
            db.TeacherAllocationHistories.Add(historyRecord);
            db.SaveChanges();

            return RedirectToAction("AllocateTeacher", new { selectedGrade = model.GradeID, selectedStreamID = model.StreamID });
        }

        #endregion

        #region Helper Methods

        private string HashPassword(string password)
        {
            // For simplicity, return plain text.
            // In production, use a real hashing algorithm.
            return password;
        }

        private void SendApprovalEmail(string toEmail, string applicantName, string defaultPassword)
        {
            try
            {
                string body = $"Dear {applicantName},\n\n" +
                              "Congratulations! Your application has been approved.\n" +
                              "Your default login credentials are:\n" +
                              $"Email: {toEmail}\n" +
                              $"Password: {defaultPassword}\n\n" +
                              "Please proceed to pay your school fees to activate your account.\n\n" +
                              "Best regards,\nAvonford Secondary School";

                MailMessage mail = new MailMessage
                {
                    From = new MailAddress(FROM_EMAIL),
                    Subject = "Application Approved - Avonford Secondary School",
                    Body = body,
                    IsBodyHtml = false
                };
                mail.To.Add(toEmail);

                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(FROM_EMAIL, FROM_PASSWORD),
                    EnableSsl = true
                };
                smtp.Send(mail);
            }
            catch (Exception)
            {
                // handle or log error
            }
        }

        private void SendRejectionEmail(string toEmail, string applicantName)
        {
            try
            {
                string body = $"Dear {applicantName},\n\n" +
                              "We regret to inform you that your application has been rejected.\n" +
                              "For further details, please contact the school administration.\n\n" +
                              "Best regards,\nAvonford Secondary School";

                MailMessage mail = new MailMessage
                {
                    From = new MailAddress(FROM_EMAIL),
                    Subject = "Application Rejected - Avonford Secondary School",
                    Body = body,
                    IsBodyHtml = false
                };
                mail.To.Add(toEmail);

                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(FROM_EMAIL, FROM_PASSWORD),
                    EnableSsl = true
                };
                smtp.Send(mail);
            }
            catch (Exception)
            {
                // handle or log error
            }
        }

        public FileResult DownloadDocument(int id, string docType)
        {
            var application = db.Applications.Find(id);
            if (application == null)
                return null;

            byte[] fileBytes = null;
            string fileName = "document.pdf";
            string contentType = "application/pdf";

            switch (docType)
            {
                case "StudentIDDoc":
                    fileBytes = application.StudentIDDoc;
                    fileName = "StudentID.pdf";
                    break;
                case "ParentIDDoc":
                    fileBytes = application.ParentIDDoc;
                    fileName = "ParentID.pdf";
                    break;
                case "PreviousReportCard":
                    fileBytes = application.PreviousReportCard;
                    fileName = "ReportCard.pdf";
                    break;
                case "ApplicationForm":
                    fileBytes = application.ApplicationForm;
                    fileName = "ApplicationForm.pdf";
                    break;
            }

            return File(fileBytes, contentType, fileName);
        }



        [HttpGet]
        public ActionResult TeacherAllocationHistory()
        {
            // Query teacher allocation history with necessary joins
            var historyQuery = from h in db.TeacherAllocationHistories
                               join t in db.Teachers on h.TeacherID equals t.TeacherID
                               join g in db.Grades on h.GradeID equals g.GradeID
                               join s in db.Streams on h.StreamID equals s.StreamID
                               join subj in db.Subjects on h.SubjectID equals subj.SubjectID
                               // Left join with Users for the AllocatedBy info; if not found, use "Unknown"
                               join u in db.Users on h.AllocatedBy equals u.UserID into userGroup
                               from u in userGroup.DefaultIfEmpty()
                               select new TeacherAllocationHistoryItem
                               {
                                   HistoryID = h.HistoryID,
                                   TeacherID = h.TeacherID,
                                   TeacherName = t.FirstName + " " + t.LastName,
                                   GradeID = h.GradeID,
                                   GradeName = g.GradeName,
                                   StreamID = h.StreamID,
                                   StreamName = s.StreamName,
                                   SubjectID = h.SubjectID,
                                   SubjectName = subj.SubjectName,
                                   AllocationDate = h.AllocationDate,
                                   AllocatedBy = h.AllocatedBy,
                                   AllocatedByName = u != null ? u.Email : "Unknown"
                               };

            var model = new TeacherAllocationHistoryViewModel
            {
                HistoryItems = historyQuery.OrderByDescending(x => x.AllocationDate).ToList()
            };

            return View(model);
        }



        #endregion








        // 1. Admin Transfer Request Queue (Screen 1)
        public ActionResult TransferRequests(string status = "TeacherApproved")
        {
            var q = db.StudentTransferRequests.AsQueryable();
            if (!string.IsNullOrEmpty(status))
                q = q.Where(r => r.Status == status);

            var list = q.OrderByDescending(r => r.CreatedAt)
                .Select(r => new AdminTransferRequestListItemVM
                {
                    TransferRequestID = r.TransferRequestID,
                    StudentName = r.Student.FirstName + " " + r.Student.LastName,
                    StudentID = r.Student.StudentID,
                    OldStream = r.OldStream,
                    NewStream = r.NewStream,
                    OldGrade = r.OldGrade,
                    NewGrade = r.NewGrade,
                    Status = r.Status,
                    SubmittedDate = r.CreatedAt,
                    StatusColor = r.Status == "TeacherApproved" ? "info"
                                : r.Status == "AdminApproved" ? "success"
                                : r.Status == "AdminRejected" ? "danger"
                                : "secondary"
                }).ToList();

            ViewBag.Status = status;
            return View(list);
        }

        // 2. Request Review & Timetable Update Panel (Screen 2)
        [HttpGet]
        public ActionResult ReviewTransferRequest(int id)
        {
            var req = db.StudentTransferRequests.Find(id);
            if (req == null) return HttpNotFound();

            // Find the student
            var student = req.Student;

            // Capacity calculation
            var streamRow = db.Streams.FirstOrDefault(s => s.StreamName == req.NewStream);
            int maxSeats = streamRow?.MaxCapacity ?? 0;
            int currentSeatCount = db.Students.Count(s => s.Grade == req.NewGrade && s.Stream == req.NewStream && s.Status == "Active");
            bool isFull = maxSeats > 0 && currentSeatCount >= maxSeats;

            // Conflict checker: (Simple demo: find duplicates in GradeSubjects)
            var newSubjects = db.GradeSubjects
                .Where(gs => gs.GradeID == req.NewGrade && gs.Stream.StreamName == req.NewStream)
                .Select(gs => gs.Subject.SubjectName)
                .ToList();
            var oldSubjects = db.GradeSubjects
                .Where(gs => gs.GradeID == req.OldGrade && gs.Stream.StreamName == req.OldStream)
                .Select(gs => gs.Subject.SubjectName)
                .ToList();
            var conflicts = newSubjects.Intersect(oldSubjects).ToList();

            // Timetable impact preview - for demo, just list old and new subjects
            string timetableHtml = $"<div><b>Old Subjects:</b><ul>{string.Join("", oldSubjects.Select(s => $"<li>{s}</li>"))}</ul>" +
                                   $"<b>New Subjects:</b><ul>{string.Join("", newSubjects.Select(s => $"<li>{s}</li>"))}</ul></div>";

            // Attachment
            var attachment = req.AttachmentFileID != null
                ? db.TransferRequestFiles.FirstOrDefault(f => f.FileID == req.AttachmentFileID)
                : null;

            // Audit log
            var audit = db.TransferRequestAuditLogs
                .Where(a => a.TransferRequestID == id)
                .OrderBy(a => a.ActionDate)
                .ToList()
                .Select(a => new AuditLogItem
                {
                    Action = a.Action,
                    PerformedByRole = a.PerformedByRole,
                    PerformedByName = db.Users.FirstOrDefault(u => u.UserID == a.PerformedBy)?.Email ?? "System",
                    ActionDate = a.ActionDate,
                    Comment = a.Comment
                }).ToList();

            var vm = new AdminTransferRequestReviewVM
            {
                TransferRequestID = req.TransferRequestID,
                StudentName = student.FirstName + " " + student.LastName,
                StudentID = student.StudentID,
                OldGrade = req.OldGrade,
                NewGrade = req.NewGrade,
                OldStream = req.OldStream,
                NewStream = req.NewStream,
                Justification = req.Justification,
                TeacherComment = req.TeacherComment,
                Status = req.Status,
                AttachmentFileID = attachment?.FileID,
                AttachmentFileName = attachment?.FileName,
                CurrentSeatCount = currentSeatCount,
                MaxSeats = maxSeats,
                IsStreamFull = isFull,
                ConflictingSubjects = conflicts,
                TimetableImpactPreviewHtml = timetableHtml,
                AuditTrail = audit
            };

            return View(vm);
        }

        // 3. Document Viewer (PDF preview in-browser)
        public ActionResult PreviewTransferAttachment(int fileId)
        {
            var file = db.TransferRequestFiles.Find(fileId);
            if (file == null) return HttpNotFound();
            Response.AppendHeader("Content-Disposition", "inline; filename=" + file.FileName);
            return File(file.FileContent, file.FileType ?? "application/pdf");
        }

        // 4. Admin Approve/Reject logic
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewTransferRequest(AdminTransferRequestReviewVM model)
        {
            var req = db.StudentTransferRequests.Find(model.TransferRequestID);
            if (req == null) return HttpNotFound();

            int adminId = (int)Session["UserID"];

            if (model.Action == "Approve")
            {
                // Check for capacity
                var streamRow = db.Streams.FirstOrDefault(s => s.StreamName == req.NewStream);
                int maxSeats = streamRow?.MaxCapacity ?? 0;
                int currentSeatCount = db.Students.Count(s => s.Grade == req.NewGrade && s.Stream == req.NewStream && s.Status == "Active");
                if (maxSeats > 0 && currentSeatCount >= maxSeats)
                {
                    ModelState.AddModelError("", "Target stream is full. Cannot approve transfer.");
                    return RedirectToAction("ReviewTransferRequest", new { id = req.TransferRequestID });
                }

                // Schedule update: Remove old, add new
                var student = req.Student;
                student.Grade = req.NewGrade;
                student.Stream = req.NewStream;
                db.SaveChanges();

                // (Optional) You could remove old GradeSubject/Enrollment records and add new here

                // Update request status
                req.Status = "AdminApproved";
                req.AdminComment = model.AdminComment;
                req.AdminID = adminId;
                req.AdminActionDate = DateTime.Now;
                db.SaveChanges();

                // Audit log
                db.TransferRequestAuditLogs.Add(new TransferRequestAuditLog
                {
                    TransferRequestID = req.TransferRequestID,
                    Action = "AdminApproved",
                    PerformedBy = adminId,
                    PerformedByRole = "Admin",
                    ActionDate = DateTime.Now,
                    Comment = model.AdminComment,
                    BeforeState = "", // Optionally JSON serialize old student state
                    AfterState = "" // Optionally JSON serialize new state
                });
                db.SaveChanges();

                // Notify all: student, old teacher, new teachers
                // (You can add notification/email logic here...)

                TempData["Success"] = "Transfer request approved and schedule updated.";
            }
            else if (model.Action == "Reject")
            {
                if (string.IsNullOrWhiteSpace(model.AdminComment))
                {
                    ModelState.AddModelError("AdminComment", "Rejection reason is required.");
                    return RedirectToAction("ReviewTransferRequest", new { id = req.TransferRequestID });
                }
                req.Status = "AdminRejected";
                req.AdminComment = model.AdminComment;
                req.AdminID = adminId;
                req.AdminActionDate = DateTime.Now;
                db.SaveChanges();

                // Audit log
                db.TransferRequestAuditLogs.Add(new TransferRequestAuditLog
                {
                    TransferRequestID = req.TransferRequestID,
                    Action = "AdminRejected",
                    PerformedBy = adminId,
                    PerformedByRole = "Admin",
                    ActionDate = DateTime.Now,
                    Comment = model.AdminComment
                });
                db.SaveChanges();

                // Notify all: student, teacher
                // (Add notification/email logic...)

                TempData["Error"] = "Transfer request rejected.";
            }
            else
            {
                ModelState.AddModelError("", "Select Approve or Reject.");
                return RedirectToAction("ReviewTransferRequest", new { id = req.TransferRequestID });
            }

            return RedirectToAction("TransferRequests");
        }





        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }


















        ///////////////////////////////////////////////////////////////////////
        ///


       
        [HttpGet]
        public ActionResult Drivers(string status = "", string q = "")
        {
            // NOTE: your DbSet is singular: db.Driver
            var baseQuery = db.Drivers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                baseQuery = baseQuery.Where(d => d.Status == status);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                baseQuery = baseQuery.Where(d =>
                    d.Name.Contains(q) || d.Phone.Contains(q) || (d.Email != null && d.Email.Contains(q)));
            }

            // materialize first, then compute per-driver counts
            var list = baseQuery
                .OrderBy(d => d.Name)
                .ToList();

            var vm = new DriverListFilterVM();

            vm.Drivers = list.Select(d =>
            {
                var todayCount = GetDriverDeliveriesToday(d.DriverID);
                var capLeft = Math.Max(0, d.MaxDailyDeliveries - todayCount);

                return new DriverListItemVM
                {
                    DriverID = d.DriverID,
                    Name = d.Name,
                    VehicleType = d.VehicleType,
                    Phone = d.Phone,
                    Email = d.Email,
                    Status = d.Status,
                    MaxDailyDeliveries = d.MaxDailyDeliveries,
                    DeliveriesToday = todayCount,
                    CapacityLeft = capLeft,
                    PhotoBase64 = ToBase64(d.Photo),
                    LastKnownLatitude = d.LastKnownLatitude,
                    LastKnownLongitude = d.LastKnownLongitude
                };
            }).ToList();

            vm.Total = vm.Drivers.Count;
            vm.AvailableCount = vm.Drivers.Count(x => x.Status == "Available");
            vm.OnDeliveryCount = vm.Drivers.Count(x => x.Status == "OnDelivery");
            vm.OfflineCount = vm.Drivers.Count(x => x.Status == "Offline");

            return View(vm);
        }

        // using System.Data.Entity;   // (make sure this is at the top)

        [HttpGet]
        public ActionResult CreateDriver()
        {
            return View(new DriverFormVM
            {
                LicenseExpiry = DateTime.Today.AddYears(2),
                MaxDailyDeliveries = 5,
                DefaultAvailable = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateDriver(DriverFormVM model)
        {
            if (model.LicenseExpiry <= DateTime.Today)
                ModelState.AddModelError(nameof(model.LicenseExpiry), "License expiry must be in the future.");

            if (string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError(nameof(model.Email), "Email is required (used for driver login).");

            if (!ModelState.IsValid)
                return View(model);

            // Read optional photo bytes
            byte[] photoBytes = null;
            if (model.Photo != null && model.Photo.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    model.Photo.InputStream.CopyTo(ms);
                    photoBytes = ms.ToArray();
                }
            }

            var email = model.Email.Trim();
            const string defaultPassword = "Admin@2004";

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    // 1) Ensure a Users row exists for this driver (for login)
                    var user = db.Users.FirstOrDefault(u => u.Email == email);

                    if (user == null)
                    {
                        user = new User
                        {
                            Email = email,
                            PasswordHash = defaultPassword,     // (plain for now, matches your current login check)
                            Role = "Driver",
                            DateCreated = DateTime.Now,
                            IsActive = true
                        };
                        db.Users.Add(user);
                        db.SaveChanges(); // get UserID
                    }
                    else if (!string.Equals(user.Role, "Driver", StringComparison.OrdinalIgnoreCase))
                    {
                        // Email is taken by a different role; fail with a friendly message
                        ModelState.AddModelError(nameof(model.Email),
                            "This email already belongs to a non-driver account. Use a different email.");
                        tx.Rollback();
                        return View(model);
                    }
                    // else: user exists and is already a Driver → reuse it

                    // 2) Create the Driver row
                    var entity = new Driver
                    {
                        Name = model.Name,
                        VehicleType = model.VehicleType,
                        Phone = model.Phone,
                        Email = email,                   // keep in sync with Users.Email
                        Photo = photoBytes,
                        LicenseNo = model.LicenseNo,
                        LicenseExpiry = model.LicenseExpiry,
                        MaxDailyDeliveries = model.MaxDailyDeliveries,
                        Status = model.DefaultAvailable ? "Available" : "Offline",
                        CreatedAt = DateTime.Now
                    };

                    db.Drivers.Add(entity);
                    db.SaveChanges();

                    tx.Commit();

                    TempData["Toast"] = $"Driver created. Login: {email} / {defaultPassword}";
                    return RedirectToAction("Drivers");
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    ModelState.AddModelError("", "Could not create driver. " + ex.Message);
                    return View(model);
                }
            }
        }


        [HttpGet]
        public ActionResult EditDriver(int id)
        {
            var d = db.Drivers.FirstOrDefault(x => x.DriverID == id);
            if (d == null) return HttpNotFound();

            var vm = new DriverFormVM
            {
                DriverID = d.DriverID,
                Name = d.Name,
                VehicleType = d.VehicleType,
                Phone = d.Phone,
                Email = d.Email,
                LicenseNo = d.LicenseNo,
                LicenseExpiry = d.LicenseExpiry,
                MaxDailyDeliveries = d.MaxDailyDeliveries,
                DefaultAvailable = d.Status == "Available"
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditDriver(DriverFormVM model)
        {
            if (!model.DriverID.HasValue) return HttpNotFound();

            if (model.LicenseExpiry <= DateTime.Today)
                ModelState.AddModelError(nameof(model.LicenseExpiry), "License expiry must be in the future.");

            if (!ModelState.IsValid)
                return View(model);

            var d = db.Drivers.FirstOrDefault(x => x.DriverID == model.DriverID.Value);
            if (d == null) return HttpNotFound();

            d.Name = model.Name;
            d.VehicleType = model.VehicleType;
            d.Phone = model.Phone;
            d.Email = model.Email;
            d.LicenseNo = model.LicenseNo;
            d.LicenseExpiry = model.LicenseExpiry;
            d.MaxDailyDeliveries = model.MaxDailyDeliveries;
            d.Status = model.DefaultAvailable ? "Available" : "Offline";

            if (model.Photo != null && model.Photo.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    model.Photo.InputStream.CopyTo(ms);
                    d.Photo = ms.ToArray();
                }
            }

            db.SaveChanges();
            return RedirectToAction("Drivers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteDriver(int id)
        {
            var d = db.Drivers.FirstOrDefault(x => x.DriverID == id);
            if (d == null) return HttpNotFound();

            // block delete if assigned to active deliveries
            bool hasActive = db.BookOrders.Any(o =>
                o.DriverID == id &&
                (o.Status == "AwaitingDriver" || o.Status == "OutForDelivery" || o.Status == "DeliveryUnderway"));

            if (hasActive)
            {
                TempData["Error"] = "Driver has active deliveries. Unassign first.";
                return RedirectToAction("Drivers");
            }

            db.Drivers.Remove(d);
            db.SaveChanges();
            return RedirectToAction("Drivers");
        }

        // ---------- Screen 3: Orders awaiting driver ----------
        [HttpGet]
        public ActionResult OrdersAwaitingDriver()
        {
            var orders = db.BookOrders
                .Include(o => o.BooksForSale)
                .Where(o => o.DeliveryType == "Delivery" && o.Status == "AwaitingDriver")
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            var vm = new OrdersAwaitingDriverVM
            {
                Orders = orders.Select(o =>
                {
                    var mainImgBytes = db.BookImages
                        .Where(i => i.BookID == o.BookID && i.IsMain)
                        .Select(i => i.ImageBytes)
                        .FirstOrDefault();

                    var buyer = db.Users.FirstOrDefault(u => u.UserID == o.BuyerID);
                    string buyerName = buyer != null ? buyer.Email : $"User #{o.BuyerID}";

                    return new OrdersAwaitingDriverItemVM
                    {
                        OrderID = o.OrderID,
                        BookID = o.BookID,
                        BookTitle = o.BooksForSale?.Title,
                        Condition = o.BooksForSale?.Condition,
                        BuyerName = buyerName,
                        BuyerEmail = buyer?.Email,
                        DeliveryAddress = o.DeliveryAddress,
                        DeliveryFee = o.DeliveryFee,
                        CreatedAt = o.CreatedAt,
                        MainImageBase64 = ToBase64(mainImgBytes)
                    };
                }).ToList()
            };

            vm.Total = vm.Orders.Count;
            return View(vm);
        }

        // ---------- Screen 4: Assign driver (GET) ----------
        [HttpGet]
        public ActionResult AssignDriver(int orderId)
        {
            var o = db.BookOrders
                .Include(x => x.BooksForSale)
                .FirstOrDefault(x => x.OrderID == orderId);

            if (o == null) return HttpNotFound();
            if (o.DeliveryType != "Delivery" || o.Status != "AwaitingDriver")
            {
                TempData["Error"] = "Order is not awaiting a driver.";
                return RedirectToAction("OrdersAwaitingDriver");
            }

            var book = o.BooksForSale;
            var buyer = db.Users.FirstOrDefault(u => u.UserID == o.BuyerID);
            var mainImg = db.BookImages
                .Where(i => i.BookID == o.BookID && i.IsMain)
                .Select(i => i.ImageBytes)
                .FirstOrDefault();

            var vm = new AssignDriverVM
            {
                OrderID = o.OrderID,
                BookID = o.BookID,
                BookTitle = book?.Title,
                Condition = book?.Condition,
                BuyerName = buyer?.Email ?? $"User #{o.BuyerID}",
                BuyerEmail = buyer?.Email,
                DeliveryAddress = o.DeliveryAddress,
                DeliveryFee = o.DeliveryFee,
                OrderDate = o.CreatedAt,
                MainImageBase64 = ToBase64(mainImg)
            };

            // Build driver list with capacity & disabled state
            var all = db.Drivers.OrderBy(d => d.Name).ToList();
            foreach (var d in all)
            {
                var todayCount = GetDriverDeliveriesToday(d.DriverID);
                var capLeft = Math.Max(0, d.MaxDailyDeliveries - todayCount);

                bool disabled = false;
                string why = "";
                if (d.Status == "Offline")
                {
                    disabled = true; why = "Driver offline";
                }
                else if (capLeft <= 0)
                {
                    disabled = true; why = "Capacity full for today";
                }

                vm.Drivers.Add(new AssignDriverDriverItemVM
                {
                    DriverID = d.DriverID,
                    Name = d.Name,
                    PhotoBase64 = ToBase64(d.Photo),
                    Status = d.Status,
                    MaxDailyDeliveries = d.MaxDailyDeliveries,
                    DeliveriesToday = todayCount,
                    CapacityLeft = capLeft,
                    LastKnownLatitude = d.LastKnownLatitude,
                    LastKnownLongitude = d.LastKnownLongitude,
                    IsDisabled = disabled,
                    DisabledReason = why
                });
            }

            return View(vm);
        }

        // ---------- Assign driver (POST) ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignDriver(AssignDriverPostVM model)
        {
            // Don't hard fail on ModelState — we validate the essentials ourselves
            // Fix date if binder failed due to locale (e.g., "2025/08/10")
            if (!model.ExpectedDeliveryDate.HasValue)
            {
                var raw = Request.Form["ExpectedDeliveryDate"];
                model.ExpectedDeliveryDate = CoerceDate(raw);
            }

            if (model == null || model.OrderID <= 0 || model.DriverID <= 0)
            {
                TempData["Error"] = "Please select a driver and try again.";
                return RedirectToAction("AssignDriver", new { orderId = model?.OrderID ?? 0 });
            }

            var order = db.BookOrders.FirstOrDefault(o => o.OrderID == model.OrderID);
            if (order == null) return HttpNotFound();

            if (order.DeliveryType != "Delivery" || order.Status != "AwaitingDriver")
            {
                TempData["Error"] = "Order is not awaiting a driver.";
                return RedirectToAction("OrdersAwaitingDriver");
            }

            var d = db.Drivers.FirstOrDefault(x => x.DriverID == model.DriverID);
            if (d == null)
            {
                TempData["Error"] = "Driver not found.";
                return RedirectToAction("AssignDriver", new { orderId = model.OrderID });
            }

            // capacity & availability checks
            var todayCount = GetDriverDeliveriesToday(d.DriverID);
            var capLeft = Math.Max(0, d.MaxDailyDeliveries - todayCount);
            if (d.Status == "Offline" || capLeft <= 0)
            {
                TempData["Error"] = d.Status == "Offline" ? "Driver is offline." : "Driver capacity full for today.";
                return RedirectToAction("AssignDriver", new { orderId = model.OrderID });
            }

            // Resolve AssignedBy safely (handles your hard-coded admin login)
            int assignedBy = 0;
            if (Session["UserID"] != null)
            {
                int.TryParse(Session["UserID"].ToString(), out assignedBy);
            }
            if (assignedBy <= 0)
            {
                // Seed/obtain a real Admin user id so FK is valid
                assignedBy = EnsureSystemAdminUser();
            }

            // Assign
            order.DriverID = d.DriverID;
            order.Status = "OutForDelivery";   // matches CHECK constraint
            order.UpdatedAt = DateTime.Now;

            db.DriverAssignments.Add(new DriverAssignment
            {
                OrderID = order.OrderID,
                DriverID = d.DriverID,
                AssignedBy = assignedBy,
                AssignedAt = DateTime.Now,
                ExpectedDeliveryDate = model.ExpectedDeliveryDate, // may be null (that’s OK)
                Notes = model.Notes
            });

            // If the driver now has any active orders, mark as OnDelivery
            bool hasActive = db.BookOrders.Any(o =>
                o.DriverID == d.DriverID &&
                (o.Status == "OutForDelivery" || o.Status == "DeliveryUnderway"));

            d.Status = hasActive ? "OnDelivery" : d.Status;

            db.SaveChanges();

            // Notify buyer
            db.Notifications.Add(new Notification
            {
                UserID = order.BuyerID,
                Title = "Your order is on its way",
                Message = $"Driver {d.Name} has been assigned to deliver your book (Order #{order.OrderID}).",
                IsRead = false,
                CreatedDate = DateTime.Now,
                OrderID = order.OrderID
            });
            db.SaveChanges();

            TempData["Success"] = "Driver assigned successfully.";
            return RedirectToAction("OrdersAwaitingDriver");
        }

        // ---------- helpers ----------
        private int GetDriverDeliveriesToday(int driverId)
        {
            var start = DateTime.Today;
            var end = start.AddDays(1);

            return db.BookOrders.Count(o =>
                o.DriverID == driverId &&
                o.CreatedAt >= start && o.CreatedAt < end &&
                (o.Status == "AwaitingDriver" || o.Status == "OutForDelivery" || o.Status == "DeliveryUnderway"));
        }

        private static string ToBase64(byte[] bytes)
        {
            return (bytes == null || bytes.Length == 0) ? null : Convert.ToBase64String(bytes);
        }

        // Ensure there's a valid admin user to satisfy FK on DriverAssignments.AssignedBy
        private int EnsureSystemAdminUser()
        {
            const string sysEmail = "system.admin@avonford.local";
            var admin = db.Users.FirstOrDefault(u => u.Email == sysEmail);
            if (admin != null) return admin.UserID;

            admin = new User
            {
                Email = sysEmail,
                PasswordHash = "!",     // not used for login
                Role = "Admin",
                IsActive = true,
                DateCreated = DateTime.Now
            };
            db.Users.Add(admin);
            db.SaveChanges();
            return admin.UserID;
        }

        // Be forgiving about browser/culture date formats
        private DateTime? CoerceDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            DateTime dt;
            string[] formats = { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "MM/dd/yyyy" };
            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt.Date;

            if (DateTime.TryParse(raw, out dt))
                return dt.Date;

            return null;
        }













        

            // GET: /Admin/ReturnsQueue?status=Pending
            [HttpGet]
            public ActionResult ReturnsQueue(string status = "Pending")
            {
                var q = db.Returns.AsQueryable();
                if (!string.IsNullOrWhiteSpace(status))
                    q = q.Where(r => r.Status == status);

                // materialize basic info, then join users by query to avoid heavy navprops
                var rows = q.OrderByDescending(r => r.CreatedAt)
                            .Select(r => new
                            {
                                r.ReturnID,
                                r.OrderID,
                                r.BuyerUserID,
                                r.SellerUserID,
                                r.Reason,
                                r.Status,
                                r.CreatedAt,
                                PhotoCount = db.ReturnImages.Count(i => i.ReturnID == r.ReturnID)
                            })
                            .ToList();

                var userIds = rows.SelectMany(r => new[] { r.BuyerUserID, r.SellerUserID }).Distinct().ToList();
                var emails = db.Users.Where(u => userIds.Contains(u.UserID))
                                     .Select(u => new { u.UserID, u.Email })
                                     .ToList();

                var vm = new AdminReturnsQueueVM
                {
                    Filter = status,
                    Items = rows.Select(r => new AdminReturnRowVM
                    {
                        ReturnID = r.ReturnID,
                        OrderID = r.OrderID,
                        BuyerEmail = emails.FirstOrDefault(e => e.UserID == r.BuyerUserID)?.Email,
                        SellerEmail = emails.FirstOrDefault(e => e.UserID == r.SellerUserID)?.Email,
                        Reason = r.Reason,
                        Status = r.Status,
                        CreatedAt = r.CreatedAt,
                        AgeDays = (int)Math.Floor((DateTime.Now - r.CreatedAt).TotalDays),
                        PhotoCount = r.PhotoCount
                    }).ToList()
                };

                return View(vm);
            }

            // GET: /Admin/ReviewReturn/{id}
            [HttpGet]
            public ActionResult ReviewReturn(int id)
            {
                var r = db.Returns.FirstOrDefault(x => x.ReturnID == id);
                if (r == null) return HttpNotFound();

                var order = db.BookOrders.Include(o => o.BooksForSale).FirstOrDefault(o => o.OrderID == r.OrderID);
                if (order == null) return HttpNotFound();

                var buyerEmail = db.Users.Where(u => u.UserID == r.BuyerUserID).Select(u => u.Email).FirstOrDefault();
                var sellerEmail = db.Users.Where(u => u.UserID == r.SellerUserID).Select(u => u.Email).FirstOrDefault();

                var images = db.ReturnImages.Where(i => i.ReturnID == r.ReturnID)
                                            .Select(i => i.ImageBytes)
                                            .ToList();

                var vm = new AdminReturnReviewVM
                {
                    ReturnID = r.ReturnID,
                    OrderID = r.OrderID,
                    BuyerEmail = buyerEmail,
                    SellerEmail = sellerEmail,
                    BookTitle = order.BooksForSale?.Title,
                    BookValue = order.Subtotal,
                    DeliveryFee = order.DeliveryFee,
                    Reason = r.Reason,
                    Description = r.Description,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    ImageBase64 = images.Select(b => Convert.ToBase64String(b)).ToList()
                };

                return View(vm);
            }

            // POST: /Admin/ApproveReturn
            [HttpPost]
            [ValidateAntiForgeryToken]
            public ActionResult ApproveReturn(AdminReturnDecisionVM model)
            {
                if (!ModelState.IsValid) return RedirectToAction("ReturnsQueue");

                var r = db.Returns.FirstOrDefault(x => x.ReturnID == model.ReturnID);
                if (r == null) return HttpNotFound();
                if (r.Status == "Declined" || r.Status == "Refunded") return RedirectToAction("ReviewReturn", new { id = r.ReturnID });

                var order = db.BookOrders.FirstOrDefault(o => o.OrderID == r.OrderID);
                if (order == null) return HttpNotFound();

                // amounts
                var bookValue = order.Subtotal; // refund base
                var deliveryPart = model.IncludeDeliveryFee ? order.DeliveryFee : 0m;
                var refundTotal = bookValue + deliveryPart;

                // seller clawback: 80% of book value (platform fee stays with platform)
                var sellerDebit = Math.Round(bookValue * 0.80m, 2, MidpointRounding.AwayFromZero);

                // wallets
                var buyerW = EnsureWallet(order.BuyerID);
                var sellerW = EnsureWallet(order.SellerID);

                var sellerBefore = sellerW.AvailableBalance;
                sellerW.AvailableBalance = sellerBefore - sellerDebit;
                sellerW.LastUpdated = DateTime.Now;

                db.WalletTransactions.Add(new WalletTransaction
                {
                    UserID = order.SellerID,
                    Amount = sellerDebit,
                    Direction = "Debit",
                    Reason = "ReturnRefund",
                    RefType = "Return",
                    RefID = r.ReturnID,
                    CreatedAt = DateTime.Now,
                    BeforeBalance = sellerBefore,
                    AfterBalance = sellerW.AvailableBalance,
                    Notes = $"Clawback for RMA #{r.ReturnID} (Order #{order.OrderID})"
                });

                var buyerBefore = buyerW.AvailableBalance;
                buyerW.AvailableBalance = buyerBefore + refundTotal;
                buyerW.LastUpdated = DateTime.Now;

                db.WalletTransactions.Add(new WalletTransaction
                {
                    UserID = order.BuyerID,
                    Amount = refundTotal,
                    Direction = "Credit",
                    Reason = "ReturnRefund",
                    RefType = "Return",
                    RefID = r.ReturnID,
                    CreatedAt = DateTime.Now,
                    BeforeBalance = buyerBefore,
                    AfterBalance = buyerW.AvailableBalance,
                    Notes = $"Refund for RMA #{r.ReturnID} (Order #{order.OrderID})"
                });

                // update return + order
                r.Status = "Refunded"; // approve and immediately refund
                r.AdminID = (Session["UserID"] != null) ? (int?)Convert.ToInt32(Session["UserID"]) : null;
                r.AdminComment = model.Comment;
                r.DecisionAt = DateTime.Now;
                r.RefundAmountBookValue = bookValue;
                r.RefundAmountDelivery = deliveryPart;
                r.UpdatedAt = DateTime.Now;

                // optional: mark order as Refunded
                order.Status = "Refunded";
                order.UpdatedAt = DateTime.Now;

                // audit + notifications
                db.ReturnsAuditLogs.Add(new ReturnsAuditLog
                {
                    ReturnID = r.ReturnID,
                    Action = "Approved+Refunded",
                    PerformedBy = r.AdminID,
                    PerformedByRole = "Admin",
                    ActionDate = DateTime.Now,
                    Comment = model.Comment
                });

                db.Notifications.Add(new Notification
                {
                    UserID = order.BuyerID,
                    Title = "Return approved",
                    Message = $"Your return (RMA #{r.ReturnID}) was approved. R {refundTotal:0.00} credited to your wallet.",
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    OrderID = order.OrderID
                });
                db.Notifications.Add(new Notification
                {
                    UserID = order.SellerID,
                    Title = "Return approved - funds debited",
                    Message = $"Return (RMA #{r.ReturnID}) approved. R {sellerDebit:0.00} debited from your wallet.",
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    OrderID = order.OrderID
                });

                db.SaveChanges();
                TempData["Toast"] = "Return approved and refund posted.";
                return RedirectToAction("ReviewReturn", new { id = r.ReturnID });
            }

            // POST: /Admin/DeclineReturn
            [HttpPost]
            [ValidateAntiForgeryToken]
            public ActionResult DeclineReturn(AdminReturnDecisionVM model)
            {
                if (!ModelState.IsValid) return RedirectToAction("ReturnsQueue");

                var r = db.Returns.FirstOrDefault(x => x.ReturnID == model.ReturnID);
                if (r == null) return HttpNotFound();
                if (r.Status == "Refunded" || r.Status == "Declined") return RedirectToAction("ReviewReturn", new { id = r.ReturnID });

                var order = db.BookOrders.FirstOrDefault(o => o.OrderID == r.OrderID);
                if (order == null) return HttpNotFound();

                r.Status = "Declined";
                r.AdminID = (Session["UserID"] != null) ? (int?)Convert.ToInt32(Session["UserID"]) : null;
                r.AdminComment = model.Comment;
                r.DecisionAt = DateTime.Now;
                r.UpdatedAt = DateTime.Now;

                db.ReturnsAuditLogs.Add(new ReturnsAuditLog
                {
                    ReturnID = r.ReturnID,
                    Action = "Declined",
                    PerformedBy = r.AdminID,
                    PerformedByRole = "Admin",
                    ActionDate = DateTime.Now,
                    Comment = model.Comment
                });

                db.Notifications.Add(new Notification
                {
                    UserID = order.BuyerID,
                    Title = "Return declined",
                    Message = $"Your return (RMA #{r.ReturnID}) was declined. Reason: {model.Comment}",
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    OrderID = order.OrderID
                });

                db.SaveChanges();
                TempData["Toast"] = "Return declined.";
                return RedirectToAction("ReviewReturn", new { id = r.ReturnID });
            }

            // POST: /Admin/NeedInfoReturn
            [HttpPost]
            [ValidateAntiForgeryToken]
            public ActionResult NeedInfoReturn(AdminReturnDecisionVM model)
            {
                if (!ModelState.IsValid) return RedirectToAction("ReturnsQueue");

                var r = db.Returns.FirstOrDefault(x => x.ReturnID == model.ReturnID);
                if (r == null) return HttpNotFound();
                if (r.Status == "Refunded" || r.Status == "Declined") return RedirectToAction("ReviewReturn", new { id = r.ReturnID });

                var order = db.BookOrders.FirstOrDefault(o => o.OrderID == r.OrderID);
                if (order == null) return HttpNotFound();

                r.Status = "NeedsInfo";
                r.AdminID = (Session["UserID"] != null) ? (int?)Convert.ToInt32(Session["UserID"]) : null;
                r.AdminComment = model.Comment;
                r.UpdatedAt = DateTime.Now;

                db.ReturnsAuditLogs.Add(new ReturnsAuditLog
                {
                    ReturnID = r.ReturnID,
                    Action = "NeedsInfo",
                    PerformedBy = r.AdminID,
                    PerformedByRole = "Admin",
                    ActionDate = DateTime.Now,
                    Comment = model.Comment
                });

                db.Notifications.Add(new Notification
                {
                    UserID = order.BuyerID,
                    Title = "More info needed for your return",
                    Message = $"RMA #{r.ReturnID}: {model.Comment}",
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    OrderID = order.OrderID
                });

                db.SaveChanges();
                TempData["Toast"] = "Requested more info from buyer.";
                return RedirectToAction("ReviewReturn", new { id = r.ReturnID });
            }

            // ==== helpers (Admin) ====
            private UserWallet EnsureWallet(int userId)
            {
                var w = db.UserWallets.FirstOrDefault(x => x.UserID == userId);
                if (w == null)
                {
                    w = new UserWallet
                    {
                        UserID = userId,
                        AvailableBalance = 0m,
                        PendingHoldBalance = 0m,
                        LastUpdated = DateTime.Now
                    };
                    db.UserWallets.Add(w);
                    db.SaveChanges();
                }
                return w;
            }














        // super-simple gate; tweak to your auth
        private bool IsAdmin() =>
            Session["UserID"] != null && (
                string.Equals(Convert.ToString(Session["UserRole"]), "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Convert.ToString(Session["IsAdmin"]), "1")
            );

        private int GetUserId() => Convert.ToInt32(Session["UserID"]);

        // settlement policy
        private const decimal LENDER_SHARE = 0.80m;                // 80% of borrow fee to lender
        private const decimal LATE_FEE_SHARE_TO_LENDER = 1.00m;    // 100% of late fee to lender

        // GET: /Admin/BorrowReturns
        [HttpGet]
        public ActionResult BorrowReturns()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var rows = db.BorrowReservations
                .Include(r => r.BooksForSale)
                .Where(r => r.Status == "ReturnedPendingApproval")
                .OrderBy(r => r.DueAt ?? r.EndAt)
                .ToList() // materialize so we can do Date math
                .Select(r =>
                {
                    var now = DateTime.Now;
                    var retAt = r.ReturnAt ?? now;
                    var dueAt = r.DueAt ?? r.EndAt.Date.AddHours(23).AddMinutes(59);

                    var usageDays = Math.Max(1, (int)Math.Ceiling((retAt - r.StartAt).TotalDays));
                    var lateDays = retAt > dueAt ? Math.Max(0, (int)Math.Ceiling((retAt - dueAt).TotalDays)) : 0;

                    var borrowerEmail = db.Users.Where(u => u.UserID == r.BorrowerUserID).Select(u => u.Email).FirstOrDefault();

                    return new AdminBorrowQueueRowVM
                    {
                        ReservationID = r.ReservationID,
                        BookID = r.BookID,
                        Title = r.BooksForSale?.Title,
                        BorrowerUserID = r.BorrowerUserID,
                        BorrowerEmail = borrowerEmail,
                        StartAt = r.StartAt,
                        DueAt = r.DueAt,
                        ReturnAt = r.ReturnAt,
                        EstUsageDays = usageDays,
                        EstLateDays = lateDays,
                        FeePerDay = r.FeePerDaySnapshot,
                        LateFeePerDay = r.LateFeePerDaySnapshot,
                        Status = r.Status
                    };
                })
                .ToList();

            var vm = new AdminBorrowQueueVM
            {
                Items = rows,
                TotalPending = rows.Count
            };

            return View(vm); // (we’ll build the admin queue view later)
        }
        // GET: /Admin/BorrowReturnReview/{reservationId}
        [HttpGet]
        public ActionResult BorrowReturnReview(int reservationId)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var r = db.BorrowReservations
                      .Include(x => x.BooksForSale)
                      .FirstOrDefault(x => x.ReservationID == reservationId);
            if (r == null) return HttpNotFound();
            if (r.Status != "ReturnedPendingApproval")
                return RedirectToAction("BorrowReturns");

            var retAt = r.ReturnAt ?? DateTime.Now;
            var dueAt = r.DueAt ?? r.EndAt.Date.AddHours(23).AddMinutes(59);

            var usageDays = Math.Max(1, (int)Math.Ceiling((retAt - r.StartAt).TotalDays));
            var lateDays = retAt > dueAt ? Math.Max(0, (int)Math.Ceiling((retAt - dueAt).TotalDays)) : 0;

            var borrowFee = Math.Round(r.FeePerDaySnapshot * usageDays, 2);
            var lateFee = Math.Round(r.LateFeePerDaySnapshot * lateDays, 2);

            var borrowerEmail = db.Users.Where(u => u.UserID == r.BorrowerUserID).Select(u => u.Email).FirstOrDefault();

            var vm = new AdminBorrowReviewVM
            {
                ReservationID = r.ReservationID,
                BookID = r.BookID,
                Title = r.BooksForSale?.Title,
                BorrowerUserID = r.BorrowerUserID,
                BorrowerEmail = borrowerEmail,

                StartAt = r.StartAt,
                DueAt = r.DueAt,
                ReturnAt = r.ReturnAt,

                UsageDays = usageDays,
                LateDays = lateDays,
                FeePerDay = r.FeePerDaySnapshot,
                LateFeePerDay = r.LateFeePerDaySnapshot,

                BorrowFeeFinal = borrowFee,
                LateFeeFinal = lateFee,
                DamageFee = 0m,
                TotalDue = borrowFee + lateFee
            };

            return View(vm); // (admin review page later)
        }
        // POST: /Admin/BorrowReturnApprove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BorrowReturnApprove(AdminBorrowReviewVM model)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var r = db.BorrowReservations
                      .Include(x => x.BooksForSale)
                      .FirstOrDefault(x => x.ReservationID == model.ReservationID);
            if (r == null) return HttpNotFound();
            if (r.Status != "ReturnedPendingApproval")
                return RedirectToAction("BorrowReturns");

            var retAt = r.ReturnAt ?? DateTime.Now;
            var dueAt = r.DueAt ?? r.EndAt.Date.AddHours(23).AddMinutes(59);

            var usageDays = Math.Max(1, (int)Math.Ceiling((retAt - r.StartAt).TotalDays));
            var lateDays = retAt > dueAt ? Math.Max(0, (int)Math.Ceiling((retAt - dueAt).TotalDays)) : 0;

            var borrowFee = Math.Round(r.FeePerDaySnapshot * usageDays, 2);
            var lateFee = Math.Round(r.LateFeePerDaySnapshot * lateDays, 2);
            var damage = Math.Max(0m, Math.Round(model.DamageFee, 2));
            var total = borrowFee + lateFee + damage;

            // persist on reservation (this is the source of truth)
            r.ActualUsageDays = usageDays;
            r.LateDays = lateDays;
            r.BorrowFeeFinal = borrowFee;
            r.LateFeeFinal = lateFee;
            r.DamageFee = damage;
            r.TotalDue = total;

            r.AdminReviewerID = GetUserId();
            r.ApprovalAt = DateTime.Now;
            r.Status = "AwaitingPayment";
            r.UpdatedAt = DateTime.Now;

            // audit
            db.BorrowAuditLogs.Add(new BorrowAuditLog
            {
                ReservationID = r.ReservationID,
                Action = "ReturnApproved",
                PerformedBy = r.AdminReviewerID ?? GetUserId(),
                PerformedByRole = "Admin",
                ActionDate = DateTime.Now,

                Comment = (model.AdminNote ?? "").Trim()
            });

            // notify borrower
            db.Notifications.Add(new Notification
            {
                UserID = r.BorrowerUserID,
                Title = "Invoice ready for your borrow",
                Message = $"Reservation #{r.ReservationID} has been reviewed. Total due: R{total:0.00}.",
                CreatedDate = DateTime.Now,
                IsRead = false
            });

            db.SaveChanges();
            return RedirectToAction("BorrowInvoice", "Admin", new { reservationId = r.ReservationID });
        }
        // POST: /Admin/BorrowReturnReject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BorrowReturnReject(int reservationId, string reason)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var r = db.BorrowReservations.FirstOrDefault(x => x.ReservationID == reservationId);
            if (r == null) return HttpNotFound();
            if (r.Status != "ReturnedPendingApproval")
                return RedirectToAction("BorrowReturns");

            r.Status = "ReturnRejected";
            r.UpdatedAt = DateTime.Now;

            db.BorrowAuditLogs.Add(new BorrowAuditLog
            {
                ReservationID = r.ReservationID,
                Action = "ReturnRejected",
                PerformedBy = GetUserId(),
                PerformedByRole = "Admin",
                ActionDate = DateTime.Now,
                Comment = (reason ?? "").Trim()
            });

            db.Notifications.Add(new Notification
            {
                UserID = r.BorrowerUserID,
                Title = "Return rejected",
                Message = string.IsNullOrWhiteSpace(reason) ? "Your return was rejected." : $"Reason: {reason}",
                CreatedDate = DateTime.Now,
                IsRead = false
            });

            db.SaveChanges();
            return RedirectToAction("BorrowReturns");
        }




    }
}
