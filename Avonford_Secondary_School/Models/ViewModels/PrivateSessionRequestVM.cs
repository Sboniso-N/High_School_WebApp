using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class PrivateSessionRequestVM
    {

        public int RequestID { get; set; }
        public string StudentName { get; set; }
        public string SubjectName { get; set; }
    
    

        public int SelectedSubjectID { get; set; }
        public List<SelectListItem> AvailableSubjects { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TopicMessage { get; set; }
        public List<AvailableTutorVM> AvailableTutors { get; set; }
    }

    public class AvailableTutorVM
    {
        public int TutorID { get; set; }
        public string TutorName { get; set; }
        public string ProfilePicBase64 { get; set; }
        public string Qualifications { get; set; }
        public string Bio { get; set; }
        public string Email { get; set; }
    }

    public class PrivateSessionConfirmationVM
    {
        public int RequestID { get; set; }
        public string TutorName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; }
    }

    public class PrivateSessionStatusVM
    {
        public int RequestID { get; set; }
        public string TutorName { get; set; }
        public string SubjectName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; }
        public string TopicMessage { get; set; }
        public string TutorResponse { get; set; }
        public string PaymentStatus { get; set; }
        public string MeetingLink { get; set; }
    }

   

    public class TutorReviewSessionRequestVM : PrivateSessionRequestVM { }

    public class TutorRejectSessionRequestVM : PrivateSessionRequestVM
    {
        public string RejectReason { get; set; }
    }

}
