using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class RegisterTeacherViewModel
    {
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string ContactNumber { get; set; }

        public HttpPostedFileBase ProfilePicture { get; set; }

        // New Subject Fields:
        [Required(ErrorMessage = "Primary subject is required.")]
        [Display(Name = "Primary Subject")]
        public int SubjectID1 { get; set; }  // Required

        [Display(Name = "Secondary Subject (Optional)")]
        public int? SubjectID2 { get; set; } // Optional

        [Display(Name = "Tertiary Subject (Optional)")]
        public int? SubjectID3 { get; set; } // Optional

        // New property to hold the subject list for the dropdowns.
        public IEnumerable<SelectListItem> AvailableSubjects { get; set; }

        public RegisterTeacherViewModel()
        {
            AvailableSubjects = new List<SelectListItem>();
        }
    }
}
