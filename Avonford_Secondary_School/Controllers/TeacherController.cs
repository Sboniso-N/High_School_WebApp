using Avonford_Secondary_School.Models;
using Avonford_Secondary_School.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Controllers
{
    public class TeacherController : Controller
    {
        private HighschoolDbEntities2 db = new HighschoolDbEntities2();

        #region Teacher Dashboard

        // GET: Teacher/Index
        public ActionResult Index()
        {
            // Retrieve teacher by joining the User and Teacher tables.
            int userID = Convert.ToInt32(Session["UserID"]);
            var teacherRecord = db.Teachers.FirstOrDefault(t => t.UserID == userID);
            if (teacherRecord == null)
            {
                // If not found, redirect to login.
                return RedirectToAction("Login", "Account");
            }
            int teacherID = teacherRecord.TeacherID;

            TeacherDashboardViewModel model = new TeacherDashboardViewModel();

            // Get all subject assignments for this teacher from GradeSubjectTeacher table.
            var assignments = db.GradeSubjectTeachers.Where(gst => gst.TeacherID == teacherID).ToList();

            foreach (var assignment in assignments)
            {
                // Retrieve subject name via foreign key relationship.
                string subjectName = assignment.Subject?.SubjectName ?? "Unknown";

                model.AssignedSubjects.Add(new AssignedSubjectItem
                {
                    GradeID = assignment.GradeID,
                    StreamID = assignment.StreamID,
                    SubjectID = assignment.SubjectID,
                    SubjectName = subjectName
                });
            }

            Log("Teacher Dashboard loaded for teacher ID: " + teacherID);

            return View(model);
        }

        #endregion

        #region Assessment Scheduling

        // GET: Teacher/CreateAssessment
        [HttpGet]
        public ActionResult CreateAssessment()
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var teacherRecord = db.Teachers.FirstOrDefault(t => t.UserID == userID);
            if (teacherRecord == null)
            {
                return RedirectToAction("Login", "Account");
            }
            int teacherID = teacherRecord.TeacherID;

            CreateAssessmentViewModel model = new CreateAssessmentViewModel();

            // Retrieve teacher's subject assignments to populate the dropdown.
            var assignments = db.GradeSubjectTeachers
                                .Where(gst => gst.TeacherID == teacherID)
                                .Select(gst => new
                                {
                                    gst.GradeSubjectTeacherID,
                                    Grade = gst.GradeID,
                                    Stream = gst.StreamID != null ? gst.Stream.StreamName : "N/A",
                                    SubjectName = gst.Subject.SubjectName
                                }).ToList();

            model.AvailableSubjects = assignments.Select(a => new SelectListItem
            {
                Value = a.GradeSubjectTeacherID.ToString(),
                Text = $"Grade {a.Grade} {(a.Stream != "N/A" ? ("- " + a.Stream) : "")} - {a.SubjectName}"
            }).ToList();

            // Set default values.
            model.AssessmentDate = DateTime.Now;
            model.StartTime = "09:00";
            model.EndTime = "10:00";
            model.AssessmentType = "Quiz"; // default value

            Log("CreateAssessment GET loaded for teacher ID: " + teacherID);

            return View(model);
        }

        // POST: Teacher/CreateAssessment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateAssessment(CreateAssessmentViewModel model)
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var teacherRecord = db.Teachers.FirstOrDefault(t => t.UserID == userID);
            if (teacherRecord == null)
            {
                return RedirectToAction("Login", "Account");
            }
            int teacherID = teacherRecord.TeacherID;

            if (!ModelState.IsValid)
            {
                // Re-populate the dropdown list.
                var assignments = db.GradeSubjectTeachers
                                    .Where(gst => gst.TeacherID == teacherID)
                                    .Select(gst => new
                                    {
                                        gst.GradeSubjectTeacherID,
                                        Grade = gst.GradeID,
                                        Stream = gst.StreamID != null ? gst.Stream.StreamName : "N/A",
                                        SubjectName = gst.Subject.SubjectName
                                    }).ToList();

                model.AvailableSubjects = assignments.Select(a => new SelectListItem
                {
                    Value = a.GradeSubjectTeacherID.ToString(),
                    Text = $"Grade {a.Grade} {(a.Stream != "N/A" ? ("- " + a.Stream) : "")} - {a.SubjectName}"
                }).ToList();
                return View(model);
            }

            // Retrieve the selected assignment.
            int assignmentID = model.SelectedGradeSubjectTeacherID;
            var assignment = db.GradeSubjectTeachers.Find(assignmentID);
            if (assignment == null)
            {
                ModelState.AddModelError("", "Invalid subject selection.");
                return View(model);
            }

            // Create a new Assessment record.
            Assessment assessment = new Assessment
            {
                AssessmentName = model.AssessmentName,
                AssessmentType = model.AssessmentType,
                AssessmentDate = model.AssessmentDate,
                StartTime = TimeSpan.Parse(model.StartTime),
                EndTime = TimeSpan.Parse(model.EndTime),
                Instructions = model.Instructions,
                CreatedByTeacherID = teacherID,
                SubjectID = assignment.SubjectID,
                DateCreated = DateTime.Now
            };

            db.Assessments.Add(assessment);
            db.SaveChanges();

            Log("New Assessment created with ID: " + assessment.AssessmentID + " by teacher ID: " + teacherID);

            // Redirect teacher to add questions for this assessment.
            return RedirectToAction("AddQuestions", new { assessmentID = assessment.AssessmentID });
        }

        // GET: Teacher/AddQuestions
        [HttpGet]
        public ActionResult AddQuestions(int assessmentID)
        {
            // Retrieve the assessment.
            Assessment assessment = db.Assessments.Find(assessmentID);
            if (assessment == null)
            {
                return HttpNotFound("Assessment not found.");
            }

            AddQuestionViewModel model = new AddQuestionViewModel();
            model.AssessmentID = assessmentID;
            model.AssessmentName = assessment.AssessmentName;

            // Load existing questions if any.
            var questions = db.AssessmentQuestions.Where(q => q.AssessmentID == assessmentID).ToList();
            foreach (var question in questions)
            {
                model.ExistingQuestions.Add(new ExistingQuestionItem
                {
                    QuestionID = question.QuestionID,
                    QuestionType = question.QuestionType,
                    QuestionText = question.QuestionText,
                    OptionA = question.OptionA,
                    OptionB = question.OptionB,
                    OptionC = question.OptionC,
                    OptionD = question.OptionD,
                    AnswerKey = question.AnswerKey
                });
            }

            Log("AddQuestions GET loaded for Assessment ID: " + assessmentID);
            return View(model);
        }

        // POST: Teacher/AddQuestions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddQuestions(AddQuestionViewModel model, string actionType)
        {
            // Always reload existing questions to show in the table.
            model.ExistingQuestions = db.AssessmentQuestions
                .Where(q => q.AssessmentID == model.AssessmentID)
                .Select(q => new ExistingQuestionItem
                {
                    QuestionID = q.QuestionID,
                    QuestionType = q.QuestionType,
                    QuestionText = q.QuestionText,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    AnswerKey = q.AnswerKey
                })
                .ToList();

            if (actionType == "ShowMcq")
            {
                // If teacher wants to add MCQ options, check if QuestionType is "MCQ".
                if (model.QuestionType == "MCQ")
                {
                    // Show the MCQ fields on the page (server side).
                    model.ShowMcqFields = true;
                }
                else
                {
                    // It's a Text question, so show an error.
                    ModelState.AddModelError("", "Only MCQ questions can have multiple-choice options.");
                }

                // Return the view without adding a question yet.
                return View(model);
            }

            // If actionType == "Add" or "Finish", we attempt to add the question (if valid).
            if (!ModelState.IsValid)
            {
                // If model is invalid, just return the view with the errors.
                return View(model);
            }

            if (actionType == "Add")
            {
                // Create the new question record in the DB.
                AssessmentQuestion question = new AssessmentQuestion
                {
                    AssessmentID = model.AssessmentID,
                    QuestionType = model.QuestionType,
                    QuestionText = model.QuestionText,
                    OptionA = (model.QuestionType == "MCQ") ? model.OptionA : null,
                    OptionB = (model.QuestionType == "MCQ") ? model.OptionB : null,
                    OptionC = (model.QuestionType == "MCQ") ? model.OptionC : null,
                    OptionD = (model.QuestionType == "MCQ") ? model.OptionD : null,
                    AnswerKey = (model.QuestionType == "MCQ") ? model.AnswerKey : null
                };

                db.AssessmentQuestions.Add(question);
                db.SaveChanges();

                // Clear input fields for next question.
                ModelState.Clear();
                model.QuestionType = "";
                model.QuestionText = "";
                model.OptionA = "";
                model.OptionB = "";
                model.OptionC = "";
                model.OptionD = "";
                model.AnswerKey = "";
                model.ShowMcqFields = false;

                // Reload the existing questions again to reflect the new addition.
                model.ExistingQuestions = db.AssessmentQuestions
                    .Where(q => q.AssessmentID == model.AssessmentID)
                    .Select(q => new ExistingQuestionItem
                    {
                        QuestionID = q.QuestionID,
                        QuestionType = q.QuestionType,
                        QuestionText = q.QuestionText,
                        OptionA = q.OptionA,
                        OptionB = q.OptionB,
                        OptionC = q.OptionC,
                        OptionD = q.OptionD,
                        AnswerKey = q.AnswerKey
                    })
                    .ToList();
            }
            else if (actionType == "Finish")
            {
                // Finalize the assessment scheduling.
                // Typically you'd just redirect to the teacher dashboard or somewhere else.
                return RedirectToAction("Index", "Teacher");
            }

            return View(model);
        }


        #endregion

        #region Assessment Grading

        // GET: Teacher/GradeAssessments?subjectId=...
        // Lists assessments (for a given subject) that have at least one student attempt.
        public ActionResult GradeAssessments(int subjectId)
        {
            // Retrieve teacher's ID from session (join with Teacher table)
            int userID = Convert.ToInt32(Session["UserID"]);
            var teacher = db.Teachers.FirstOrDefault(t => t.UserID == userID);
            if (teacher == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Retrieve assessments for the subject that were created by this teacher
            // and that have at least one submitted attempt.
            var assessments = db.Assessments
                .Where(a => a.SubjectID == subjectId &&
                            a.CreatedByTeacherID == teacher.TeacherID &&
                            db.AssessmentAttempts.Any(at => at.AssessmentID == a.AssessmentID && at.IsSubmitted))
                .Select(a => new AssessmentGradingItem
                {
                    AssessmentID = a.AssessmentID,
                    AssessmentName = a.AssessmentName,
                    AssessmentType = a.AssessmentType,
                    AssessmentDate = a.AssessmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime
                })
                .ToList();

            GradeAssessmentsViewModel model = new GradeAssessmentsViewModel
            {
                SubjectID = subjectId,
                SubjectName = db.Subjects.Find(subjectId)?.SubjectName ?? "Unknown",
                Assessments = assessments
            };

            return View(model);
        }

        // GET: Teacher/ViewAttempts?assessmentId=...
        public ActionResult ViewAttempts(int assessmentId)
        {
            // Retrieve the assessment.
            var assessment = db.Assessments.Find(assessmentId);
            if (assessment == null)
            {
                return HttpNotFound("Assessment not found.");
            }

            // Retrieve all submitted attempts for this assessment.
            var attempts = db.AssessmentAttempts
                .Where(at => at.AssessmentID == assessmentId && at.IsSubmitted)
                .Select(at => new AttemptItem
                {
                    AttemptID = at.AttemptID,
                    StudentID = at.StudentID,
                    AttemptStartTime = at.AttemptStartTime,
                    StudentName = db.Students.FirstOrDefault(s => s.StudentID == at.StudentID).FirstName + " " +
                                  db.Students.FirstOrDefault(s => s.StudentID == at.StudentID).LastName
                })
                .ToList();

            ViewAttemptsViewModel model = new ViewAttemptsViewModel
            {
                AssessmentID = assessmentId,
                AssessmentName = assessment.AssessmentName,
                Attempts = attempts
            };

            return View(model);
        }

        // GET: Teacher/GradeAttempt?attemptId=...
        public ActionResult GradeAttempt(int attemptId)
        {
            // Retrieve the attempt.
            var attempt = db.AssessmentAttempts.Find(attemptId);
            if (attempt == null)
            {
                return HttpNotFound("Attempt not found.");
            }

            var assessment = db.Assessments.Find(attempt.AssessmentID);
            if (assessment == null)
            {
                return HttpNotFound("Assessment not found.");
            }

            // Retrieve all questions for this assessment.
            var questions = db.AssessmentQuestions
                .Where(q => q.AssessmentID == assessment.AssessmentID)
                .Select(q => new GradingQuestionResponse
                {
                    QuestionID = q.QuestionID,
                    QuestionType = q.QuestionType,
                    QuestionText = q.QuestionText,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    // Attempt to get the student's answer (if it exists).
                    StudentAnswer = db.AssessmentAttemptAnswers
                                        .Where(a => a.AttemptID == attempt.AttemptID && a.QuestionID == q.QuestionID)
                                        .Select(a => a.StudentAnswer)
                                        .FirstOrDefault()
                })
                .ToList();

            AssessmentGradingViewModel model = new AssessmentGradingViewModel
            {
                AttemptID = attempt.AttemptID,
                AssessmentID = assessment.AssessmentID,
                AssessmentName = assessment.AssessmentName,
                StudentName = db.Students.FirstOrDefault(s => s.StudentID == attempt.StudentID).FirstName + " " +
                              db.Students.FirstOrDefault(s => s.StudentID == attempt.StudentID).LastName,
                GradingQuestions = questions
            };

            return View(model);
        }

        // POST: Teacher/GradeAttempt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GradeAttempt(AssessmentGradingViewModel model)
        {
            // For each question, set ScoreAwarded = 1 if IsCorrect is true.
            decimal totalScore = 0;
            int questionCount = 0;

            foreach (var response in model.GradingQuestions)
            {
                response.ScoreAwarded = response.IsCorrect ? 1 : 0;
                totalScore += response.ScoreAwarded;
                questionCount++;

                // Find the corresponding answer record; if not found, create one.
                var answer = db.AssessmentAttemptAnswers.FirstOrDefault(a => a.AttemptID == model.AttemptID && a.QuestionID == response.QuestionID);
                if (answer != null)
                {
                    answer.ScoreAwarded = response.ScoreAwarded;
                }
                else
                {
                    AssessmentAttemptAnswer newAnswer = new AssessmentAttemptAnswer
                    {
                        AttemptID = model.AttemptID,
                        QuestionID = response.QuestionID,
                        StudentAnswer = response.StudentAnswer, // if needed
                        ScoreAwarded = response.ScoreAwarded
                    };
                    db.AssessmentAttemptAnswers.Add(newAnswer);
                }
            }

            // Update the attempt record.
            var attemptToUpdate = db.AssessmentAttempts.Find(model.AttemptID);
            if (attemptToUpdate != null)
            {
                attemptToUpdate.TotalScore = totalScore;
                attemptToUpdate.MaxScore = questionCount;
            }
            db.SaveChanges();

            TempData["GradingMessage"] = $"Grading completed. Final Score: {totalScore}/{questionCount} ({(totalScore / questionCount * 100):F2}%).";
            return RedirectToAction("ViewAttempts", new { assessmentId = model.AssessmentID });
        }

        #endregion

        #region Helper Methods

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private void Log(string message)
        {
            //string logEntry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message + Environment.NewLine;
            //string logFile = Server.MapPath("~/App_Data/TeacherGradingLog.txt");
            //System.IO.File.AppendAllText(logFile, logEntry);
        }

        #endregion

        // GET: Teacher/TransferRequests
        public ActionResult TransferRequests(string filter = "pending")
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var teacher = db.Teachers.FirstOrDefault(t => t.UserID == userID);
            if (teacher == null) return RedirectToAction("Login", "Account");

            int teacherID = teacher.TeacherID;

            // Show "PendingTeacher" if filter == "pending", otherwise all (processed)
            var q = db.StudentTransferRequests.AsQueryable();
            if (filter == "pending")
                q = q.Where(r => r.Status == "PendingTeacher" && (r.TeacherID == teacherID || r.TeacherID == null));
            else if (filter == "processed")
                q = q.Where(r => r.Status != "PendingTeacher" && r.TeacherID == teacherID);

            var list = q.OrderByDescending(r => r.CreatedAt)
                .Select(r => new TeacherTransferRequestListItemVM
                {
                    TransferRequestID = r.TransferRequestID,
                    StudentName = r.Student.FirstName + " " + r.Student.LastName,
                    StudentID = r.Student.StudentID.ToString(),
                    OldStream = r.OldStream,
                    NewStream = r.NewStream,
                    OldGrade = r.OldGrade,
                    NewGrade = r.NewGrade,
                    Status = r.Status,
                    SubmittedDate = r.CreatedAt,
                    ActionNeeded = r.Status == "PendingTeacher"
                }).ToList();

            ViewBag.Filter = filter;
            return View(list);
        }

        // GET: Teacher/ReviewTransferRequest/5
        public ActionResult ReviewTransferRequest(int id)
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var teacher = db.Teachers.FirstOrDefault(t => t.UserID == userID);
            if (teacher == null) return RedirectToAction("Login", "Account");

            var req = db.StudentTransferRequests.Find(id);
            if (req == null) return HttpNotFound();

            // Academic Performance (example: just show dummy random grades unless you have a marks table)
            var student = req.Student;
            var currentSubjects = db.GradeSubjects
                .Where(gs => gs.GradeID == req.OldGrade && (gs.Stream.StreamName == req.OldStream || req.OldGrade < 10))
                .Select(gs => gs.Subject.SubjectName).ToList();

            // Replace with actual marks retrieval if you have such a table
            var grades = new List<SubjectGradeVM>();
            Random rng = new Random(id * 31);
            foreach (var subj in currentSubjects)
            {
                grades.Add(new SubjectGradeVM
                {
                    SubjectName = subj,
                    Grade = 40 + rng.Next(40) // Random grade for demo, 40-79
                });
            }

            // Approval only if all grades >= 50 (as example threshold)
            bool canApprove = grades.All(g => g.Grade >= 50);
            string blockReason = canApprove ? null : "Student does not meet minimum academic requirements for stream transfer.";

            var attachment = req.AttachmentFileID != null
                ? db.TransferRequestFiles.FirstOrDefault(f => f.FileID == req.AttachmentFileID)
                : null;

            var vm = new TeacherTransferRequestReviewVM
            {
                TransferRequestID = req.TransferRequestID,
                StudentName = student.FirstName + " " + student.LastName,
                StudentID = student.StudentID.ToString(),
                OldGrade = req.OldGrade,
                NewGrade = req.NewGrade,
                OldStream = req.OldStream,
                NewStream = req.NewStream,
                Justification = req.Justification,
                AttachmentFileID = attachment?.FileID,
                AttachmentFileName = attachment?.FileName,
                AcademicPerformance = grades,
                Status = req.Status,
                ActionAllowed = canApprove,
                ActionBlockedReason = blockReason
            };

            return View(vm);
        }


        public ActionResult DownloadTransferAttachment(int fileId)
        {
            var file = db.TransferRequestFiles.Find(fileId);
            if (file == null) return HttpNotFound();
            return File(file.FileContent, "application/pdf", file.FileName);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewTransferRequest(TeacherTransferRequestReviewVM model)
        {
            int userID = Convert.ToInt32(Session["UserID"]);
            var teacher = db.Teachers.FirstOrDefault(t => t.UserID == userID);
            if (teacher == null) return RedirectToAction("Login", "Account");

            var req = db.StudentTransferRequests.Find(model.TransferRequestID);
            if (req == null) return HttpNotFound();

            // Validation
            if (model.Action == "Approve")
            {
                if (string.IsNullOrWhiteSpace(model.TeacherComment) || model.TeacherComment.Length < 50)
                {
                    ModelState.AddModelError("TeacherComment", "Supporting comment is required (min 50 chars) for approval.");
                    return View(model);
                }
                req.Status = "TeacherApproved";
                req.TeacherComment = model.TeacherComment;
                req.TeacherID = teacher.TeacherID;
                req.TeacherActionDate = DateTime.Now;
            }
            else if (model.Action == "Reject")
            {
                if (string.IsNullOrWhiteSpace(model.TeacherComment))
                {
                    ModelState.AddModelError("TeacherComment", "Rejection reason is required.");
                    return View(model);
                }
                req.Status = "TeacherRejected";
                req.TeacherComment = model.TeacherComment;
                req.TeacherID = teacher.TeacherID;
                req.TeacherActionDate = DateTime.Now;
            }
            else
            {
                ModelState.AddModelError("", "Select Approve or Reject.");
                return View(model);
            }
            db.SaveChanges();

            // Add to audit log
            db.TransferRequestAuditLogs.Add(new TransferRequestAuditLog
            {
                TransferRequestID = req.TransferRequestID,
                Action = req.Status,
                PerformedBy = userID,
                PerformedByRole = "Teacher",
                ActionDate = DateTime.Now,
                Comment = model.TeacherComment
            });
            db.SaveChanges();

            // Notification to student
            db.Notifications.Add(new Notification
            {
                UserID = req.Student.UserID,
                Title = $"Transfer Request {(req.Status == "TeacherApproved" ? "Approved" : "Rejected")}",
                Message = req.Status == "TeacherApproved" ?
                    "Your stream transfer was approved by your teacher. Await admin review." :
                    "Your stream transfer was rejected by your teacher. Reason: " + model.TeacherComment,
                CreatedDate = DateTime.Now,
                TransferRequestID = req.TransferRequestID
            });
            db.SaveChanges();

            return RedirectToAction("TransferRequests");
        }



    }
}
