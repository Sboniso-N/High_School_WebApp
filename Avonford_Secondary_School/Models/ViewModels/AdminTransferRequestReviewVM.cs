using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class AdminTransferRequestReviewVM
    {
        public int TransferRequestID { get; set; }
        public string StudentName { get; set; }
        public int StudentID { get; set; }
        public string OldStream { get; set; }
        public string NewStream { get; set; }
        public int OldGrade { get; set; }
        public int NewGrade { get; set; }
        public string Justification { get; set; }
        public string TeacherComment { get; set; }
        public string Status { get; set; }
        public int? AttachmentFileID { get; set; }
        public string AttachmentFileName { get; set; }

        // Seat/capacity info
        public int CurrentSeatCount { get; set; }
        public int MaxSeats { get; set; }
        public bool IsStreamFull { get; set; }

        // Conflict checkers
        public List<string> ConflictingSubjects { get; set; } = new List<string>();
        public string TimetableImpactPreviewHtml { get; set; }

        // For Admin action
        public string AdminComment { get; set; }
        public string Action { get; set; } // "Approve" or "Reject"

        // Audit log
        public List<AuditLogItem> AuditTrail { get; set; } = new List<AuditLogItem>();
    }
    public class AuditLogItem
    {
        public string Action { get; set; }
        public string PerformedByRole { get; set; }
        public string PerformedByName { get; set; }
        public DateTime ActionDate { get; set; }
        public string Comment { get; set; }
    }
}