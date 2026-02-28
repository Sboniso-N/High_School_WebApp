using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class TutorClassCreateViewModel
    {
        [Required]
        public int TutorID { get; set; }

        [Required(ErrorMessage = "Grade is required.")]
        public int SelectedGradeID { get; set; }
        public List<SelectListItem> TutorGrades { get; set; } = new List<SelectListItem>();

        [Required(ErrorMessage = "Subject is required.")]
        public int SelectedSubjectID { get; set; }
        public List<SelectListItem> TutorSubjects { get; set; } = new List<SelectListItem>();

        [Required(ErrorMessage = "Class Name is required.")]
        [StringLength(150, MinimumLength = 4)]
        public string ClassName { get; set; }
        public string Status { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(1500, MinimumLength = 20)]
        public string ClassDescription { get; set; }

        [Required(ErrorMessage = "Capacity is required.")]
        [Range(1, 120, ErrorMessage = "Capacity must be between 1 and 120.")]
        public int Capacity { get; set; }

        [Required(ErrorMessage = "Annual fee is required.")]
        [Range(0, 999999, ErrorMessage = "Fee must be >= 0.")]
        public decimal AnnualFee { get; set; }

        public HttpPostedFileBase CoverImage { get; set; }
        public string CoverImageBase64 { get; set; }

        [StringLength(255)]
        public string Location { get; set; }

        [Required]
        public string Mode { get; set; }

        [Required(ErrorMessage = "Enrollment deadline is required.")]
        public DateTime? EnrollmentDeadline { get; set; }

        [StringLength(255)]
        public string EntryRequirements { get; set; }

        [StringLength(50)]
        public string WhatsAppContact { get; set; }

        [StringLength(255)]
        public string ScheduleTemplate { get; set; }

        public List<HttpPostedFileBase> ClassResources { get; set; }
    }

    public class TutorClassConfirmViewModel : TutorClassCreateViewModel
    {
        public List<string> ResourceNames { get; set; }
        public string GradeName { get; set; }
        public string SubjectName { get; set; }
    }

    public class TutorClassListViewModel
    {
        public int TutorClassID { get; set; }
        public string ClassName { get; set; }
        public string GradeName { get; set; }
        public string SubjectName { get; set; }
        public string Mode { get; set; }
        public int Enrolled { get; set; }
        public int Capacity { get; set; }
        public decimal AnnualFee { get; set; }
        public string CoverImageBase64 { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class TutorClassDetailViewModel
    {
        public int TutorClassID { get; set; }
        public string ClassName { get; set; }
        public string ClassDescription { get; set; }
        public string GradeName { get; set; }
        public string SubjectName { get; set; }
        public string Mode { get; set; }
        public string Location { get; set; }
        public string WhatsAppContact { get; set; }
        public DateTime? EnrollmentDeadline { get; set; }
        public string EntryRequirements { get; set; }
        public int Capacity { get; set; }
        public int Enrolled { get; set; }
        public decimal AnnualFee { get; set; }
        public bool IsActive { get; set; }
        public string CoverImageBase64 { get; set; }
        public string ScheduleTemplate { get; set; }
        public List<TutorClassResourceViewModel> Resources { get; set; }
        public List<TutorClassEnrollmentViewModel> Enrollments { get; set; }
    }

    public class TutorClassResourceViewModel
    {
        public int ResourceID { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public DateTime UploadedDate { get; set; }
    }

    public class TutorClassEnrollmentViewModel
    {
        public int EnrollmentID { get; set; }
        public string StudentName { get; set; }
        public string StudentEmail { get; set; }
        public bool IsPaid { get; set; }
        public string Status { get; set; }
        public DateTime EnrollmentDate { get; set; }
    }
}
