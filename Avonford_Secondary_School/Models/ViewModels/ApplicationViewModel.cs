using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class ApplicationViewModel
    {
        // Student Information
        [Required(ErrorMessage = "Student first name is required.")]
        [Display(Name = "Student First Name")]
        public string StudentFirstName { get; set; }

        [Required(ErrorMessage = "Student last name is required.")]
        [Display(Name = "Student Last Name")]
        public string StudentLastName { get; set; }

        [Required(ErrorMessage = "Nationality ID is required.")]
        [Display(Name = "Nationality ID")]
        public string NationalityID { get; set; }

        [Required(ErrorMessage = "Date of Birth is required.")]
        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        // Parent/Guardian Information
        [Required(ErrorMessage = "Parent/Guardian name is required.")]
        [Display(Name = "Parent/Guardian Name")]
        public string ParentName { get; set; }

        [Required(ErrorMessage = "Parent/Guardian ID is required.")]
        [Display(Name = "Parent/Guardian ID")]
        public string ParentID { get; set; }

        [Required(ErrorMessage = "Parent email is required.")]
        [Display(Name = "Parent Email")]
        [DataType(DataType.EmailAddress)]
        public string ParentEmail { get; set; }

        [Required(ErrorMessage = "Parent contact is required.")]
        [Display(Name = "Parent Contact")]
        public string ParentContact { get; set; }

        // Application Specific Information
        [Required(ErrorMessage = "Please select a grade.")]
        [Display(Name = "Grade")]
        public int GradeSelection { get; set; }

        [Display(Name = "Stream (for Grades 10 and above)")]
        public string StreamSelection { get; set; }

        // File Uploads (will be stored as byte arrays in the DB)
        [Required(ErrorMessage = "Student ID document is required.")]
        [Display(Name = "Student ID Document")]
        public HttpPostedFileBase StudentIDDoc { get; set; }

        [Required(ErrorMessage = "Parent ID document is required.")]
        [Display(Name = "Parent ID Document")]
        public HttpPostedFileBase ParentIDDoc { get; set; }

        [Required(ErrorMessage = "Previous report card is required.")]
        [Display(Name = "Previous Report Card")]
        public HttpPostedFileBase PreviousReportCard { get; set; }

        [Required(ErrorMessage = "Application form is required.")]
        [Display(Name = "Application Form")]
        public HttpPostedFileBase ApplicationForm { get; set; }

        // Dropdown lists for Grades and Streams
        public IEnumerable<SelectListItem> AvailableGrades { get; set; }
        public IEnumerable<SelectListItem> AvailableStreams { get; set; }

        // Extra fields for advanced features can be added here.
        // For example: Captcha response, additional comments, etc.
        [Display(Name = "Additional Comments")]
        public string AdditionalComments { get; set; }
    }
}
