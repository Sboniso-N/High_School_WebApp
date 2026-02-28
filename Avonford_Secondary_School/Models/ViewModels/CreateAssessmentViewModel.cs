using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class CreateAssessmentViewModel
    {
        [Required(ErrorMessage = "Please select a subject assignment.")]
        [Display(Name = "Subject")]
        public int SelectedGradeSubjectTeacherID { get; set; }

        [Required(ErrorMessage = "Assessment Name is required.")]
        [Display(Name = "Assessment Name")]
        public string AssessmentName { get; set; }

        [Required(ErrorMessage = "Assessment Type is required.")]
        [Display(Name = "Assessment Type")]
        public string AssessmentType { get; set; } // e.g., Quiz, Test, Exam

        [Required(ErrorMessage = "Assessment Date is required.")]
        [Display(Name = "Assessment Date")]
        public DateTime AssessmentDate { get; set; }

        [Required(ErrorMessage = "Start Time is required.")]
        [Display(Name = "Start Time (HH:mm)")]
        public string StartTime { get; set; }

        [Required(ErrorMessage = "End Time is required.")]
        [Display(Name = "End Time (HH:mm)")]
        public string EndTime { get; set; }

        [Display(Name = "Instructions")]
        public string Instructions { get; set; }

        // List for dropdown: teacher's assigned subjects.
        public IEnumerable<SelectListItem> AvailableSubjects { get; set; }
    }
}
