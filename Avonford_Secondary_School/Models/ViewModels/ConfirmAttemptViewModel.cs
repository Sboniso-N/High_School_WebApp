using System;
using System.ComponentModel.DataAnnotations;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class ConfirmAttemptViewModel
    {
        public int AttemptID { get; set; }
        public int AssessmentID { get; set; }
        public string AssessmentName { get; set; }
        public string Instructions { get; set; }
        [Display(Name = "Assessment Date")]
        public DateTime AssessmentDate { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}
