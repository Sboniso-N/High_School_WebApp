using System;
using System.Collections.Generic;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class ApplicationDetailsViewModel
    {
        public int ApplicationID { get; set; }
        public string UniqueAppRef { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string NationalityID { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public string ParentName { get; set; }
        public string ParentID { get; set; }
        public string ParentEmail { get; set; }
        public string ParentContact { get; set; }

        public int SelectedGrade { get; set; }
        public string SelectedStream { get; set; }
        public string ApplicationStatus { get; set; }
        public DateTime CreatedDate { get; set; }

        // Document Storage (for display or download links)
        public byte[] StudentIDDoc { get; set; }
        public byte[] ParentIDDoc { get; set; }
        public byte[] PreviousReportCard { get; set; }
        public byte[] ApplicationForm { get; set; }

        // Capacity Info
        public int CurrentEnrolled { get; set; }
        public int MaxCapacity { get; set; }
        public bool IsCapacityFull { get; set; }

        // Checkboxes
        public bool MeetsGradeCriteria { get; set; }
        public bool IdentityVerified { get; set; }
        public bool NoConcerns { get; set; }
    }
}
