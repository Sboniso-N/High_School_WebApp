using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class RegisterTutorViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "First name is required.")]
        [RegularExpression(@"^[A-Za-z \-']{2,}$", ErrorMessage = "First name must contain only letters.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [RegularExpression(@"^[A-Za-z \-']{2,}$", ErrorMessage = "Last name must contain only letters.")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "South African cell phone number is required.")]
        [RegularExpression(@"^(0[6-8][0-9]{8})$", ErrorMessage = "Cell phone number must be 10 digits and start with 06, 07 or 08.")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Qualifications are required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Qualifications must be at least 3 characters.")]
        public string Qualifications { get; set; }

        [Required(ErrorMessage = "Bio is required.")]
        [StringLength(1000, MinimumLength = 20, ErrorMessage = "Bio must be at least 20 characters.")]
        public string Bio { get; set; }

        public HttpPostedFileBase ProfilePicture { get; set; }
        public string ProfilePicBase64 { get; set; }

        [Required(ErrorMessage = "Select at least one grade (max 2).")]
        public List<int> SelectedGrades { get; set; } = new List<int>();

        public List<SelectListItem> GradeList { get; set; } = new List<SelectListItem>();
        public Dictionary<int, List<int>> SelectedSubjectsPerGrade { get; set; } = new Dictionary<int, List<int>>();
        public Dictionary<int, List<SelectListItem>> AvailableSubjectsByGrade { get; set; } = new Dictionary<int, List<SelectListItem>>();

        public bool IsConfirmation { get; set; }

        public int TutorID { get; set; }


        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SelectedGrades == null || SelectedGrades.Count < 1 || SelectedGrades.Count > 2)
                yield return new ValidationResult("Select at least 1 and at most 2 grades.", new[] { "SelectedGrades" });

            // === DISABLED SUBJECT SELECTION VALIDATION BELOW ===
            // var allSubjectsInAllGrades = new List<int>();
            // foreach (var grade in SelectedGrades)
            // {
            //     if (!SelectedSubjectsPerGrade.ContainsKey(grade) || SelectedSubjectsPerGrade[grade] == null)
            //     {
            //         yield return new ValidationResult($"Please select at least one subject for grade {grade}.");
            //         continue;
            //     }
            //     var subList = SelectedSubjectsPerGrade[grade];
            //     if (subList.Count < 1 || subList.Count > 3)
            //         yield return new ValidationResult($"Select 1-3 subjects for grade {grade}.");
            //     allSubjectsInAllGrades.AddRange(subList);
            // }

            // var seen = new HashSet<int>();
            // foreach (var grade in SelectedGrades)
            // {
            //     if (!SelectedSubjectsPerGrade.ContainsKey(grade)) continue;
            //     foreach (var subject in SelectedSubjectsPerGrade[grade])
            //     {
            //         if (seen.Contains(subject))
            //             yield return new ValidationResult("Cannot assign the same subject in more than one grade.");
            //         else
            //             seen.Add(subject);
            //     }
            // }

            if (!IsConfirmation)
            {
                if (ProfilePicture == null || ProfilePicture.ContentLength == 0)
                    yield return new ValidationResult("Profile picture is required.", new[] { "ProfilePicture" });
            }
            else
            {
                if (string.IsNullOrEmpty(ProfilePicBase64))
                    yield return new ValidationResult("Profile picture is required.", new[] { "ProfilePicBase64" });
            }
        }
    }
}
