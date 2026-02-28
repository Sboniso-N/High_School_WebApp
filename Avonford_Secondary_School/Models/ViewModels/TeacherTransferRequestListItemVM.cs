using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    // For the list/dashboard view
    public class TeacherTransferRequestListItemVM
    {
        public int TransferRequestID { get; set; }
        public string StudentName { get; set; }
        public string StudentID { get; set; }
        public string OldStream { get; set; }
        public string NewStream { get; set; }
        public int OldGrade { get; set; }
        public int NewGrade { get; set; }
        public string Status { get; set; }
        public DateTime SubmittedDate { get; set; }
        public bool ActionNeeded { get; set; } // For badge
    }

    // For academic performance of each subject
    public class SubjectGradeVM
    {
        public string SubjectName { get; set; }
        public decimal? Grade { get; set; } // Null if not graded yet
    }

    // For detailed review/approval
    public class TeacherTransferRequestReviewVM
    {
        public int TransferRequestID { get; set; }
        public string StudentName { get; set; }
        public string StudentID { get; set; }
        public int OldGrade { get; set; }
        public int NewGrade { get; set; }
        public string OldStream { get; set; }
        public string NewStream { get; set; }
        public string Justification { get; set; }
        public string AttachmentFileName { get; set; }
        public int? AttachmentFileID { get; set; }
        public List<SubjectGradeVM> AcademicPerformance { get; set; }
        public string Status { get; set; }

        // For comments and decision
        public string Action { get; set; } // "Approve" or "Reject"
        public string TeacherComment { get; set; }
        public bool ActionAllowed { get; set; } // Can approve/reject?
        public string ActionBlockedReason { get; set; }
    }

    public class TeacherTransferRequestsDashboardVM
    {
        public string StatusFilter { get; set; }
        public List<TransferRequestListItemVM> Requests { get; set; }
    }

    public class TransferRequestListItemVM
    {
        public int TransferRequestID { get; set; }
        public string StudentName { get; set; }
        public int OldGrade { get; set; }
        public string OldStream { get; set; }
        public int NewGrade { get; set; }
        public string NewStream { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActionRequired { get; set; }
    }


}