using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class StudentTutorClassCardVM
    {
        public int TutorClassID { get; set; }
        public string ClassName { get; set; }
        public string SubjectName { get; set; }
        public string GradeName { get; set; }
        public string TutorName { get; set; }
        public string TutorProfilePic { get; set; }
        public string ScheduleTemplate { get; set; }
        public decimal AnnualFee { get; set; }
        public int Capacity { get; set; }
        public int Enrolled { get; set; }
        public string CoverImageBase64 { get; set; }
        public bool IsPaid { get; set; }
    }

    public class StudentMyClassesListVM
    {
        public List<StudentTutorClassCardVM> PaidClasses { get; set; }
    }

    public class StudentFindClassesListVM
    {
        public List<StudentTutorClassCardVM> UnpaidClasses { get; set; }
    }

    public class StudentClassDetailVM
    {
        public int TutorClassID { get; set; }
        public string ClassName { get; set; }
        public string SubjectName { get; set; }
        public string GradeName { get; set; }
        public string TutorName { get; set; }
        public string TutorProfilePic { get; set; }
        public string TutorBio { get; set; }
        public string TutorQualifications { get; set; }
        public string ScheduleTemplate { get; set; }
        public string Description { get; set; }
        public decimal AnnualFee { get; set; }
        public int Capacity { get; set; }
        public int Enrolled { get; set; }
        public string CoverImageBase64 { get; set; }
        public string Mode { get; set; }
        public string Location { get; set; }
        public string WhatsAppContact { get; set; }
        public DateTime? EnrollmentDeadline { get; set; }
        public string EntryRequirements { get; set; }
        public List<string> ResourceFiles { get; set; }
        public bool IsPaid { get; set; }
        public bool CanPay { get; set; }
        public bool CanExit { get; set; }
  
        public int TutorID { get; internal set; }

        public List<UpcomingSessionVM> UpcomingSessions { get; set; } // Replace List<string>
    

    }

    public class StudentExitClassVM
    {
        public int EnrollmentID { get; set; }
        public int TutorClassID { get; set; }
        public string ClassName { get; set; }
        public string Reason { get; set; }
    }

    public class StudentTutorInfoVM
    {
        public string TutorName { get; set; }
        public string TutorProfilePic { get; set; }
        public string Bio { get; set; }
        public string Qualifications { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    public class EnrollmentConfirmationVM
    {
        public string ClassName { get; set; }
        public string TutorName { get; set; }
        public string SubjectName { get; set; }
        public string PaymentReference { get; set; }
        public DateTime PaidAt { get; set; }
        public decimal Amount { get; set; }
        public string Tutor { get; internal set; }
        public string Grade { get; internal set; }
        public decimal PaidAmount { get; internal set; }
    }

}