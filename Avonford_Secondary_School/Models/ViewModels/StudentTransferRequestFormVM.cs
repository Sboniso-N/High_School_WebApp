using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    // For Transfer Request Form
    public class StudentTransferRequestFormVM
    {
        public string CurrentStream { get; set; }
        public int CurrentGrade { get; set; }
        public List<string> CurrentSubjects { get; set; }

        public List<StreamOptionVM> AvailableStreams { get; set; }
        public string SelectedNewStream { get; set; }
        public int SelectedNewGrade { get; set; }

        public string Justification { get; set; }
        public int JustificationCharCount => Justification?.Length ?? 0;

        public HttpPostedFileBase SupportingDocument { get; set; }
        public string SupportingDocumentName { get; set; }

        public bool CanRequestTransfer { get; set; } // Used to disable if window closed
        public string TransferWindowMessage { get; set; } // Info/error for modal

        public int MaxJustificationChars { get; set; } = 600;
        public int? ExistingRequestId { get; set; } // If already requested and pending
    }

    // For listing streams, capacities, and their status
    public class StreamOptionVM
    {
        public string StreamName { get; set; }
        public string Description { get; set; }
        public int Capacity { get; set; }
        public int Enrolled { get; set; }
        public bool IsFull => Enrolled >= Capacity;
        public List<string> Subjects { get; set; }
    }

    // For Preview & Confirmation screen
    public class StudentTransferPreviewVM
    {
        public string CurrentStream { get; set; }
        public int CurrentGrade { get; set; }
        public List<string> CurrentSubjects { get; set; }

        public string NewStream { get; set; }
        public int NewGrade { get; set; }
        public List<string> NewSubjects { get; set; }

        public string Justification { get; set; }
        public string SupportingDocumentName { get; set; }
        public string DiffHtml { get; set; } // optional: HTML diff for highlights
    }

}