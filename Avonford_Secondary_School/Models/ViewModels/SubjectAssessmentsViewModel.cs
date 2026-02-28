using System;
using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class SubjectAssessmentsViewModel
    {
        public int SubjectID { get; set; }
        public string SubjectName { get; set; }
        public List<AssessmentItem> Assessments { get; set; }

        public SubjectAssessmentsViewModel()
        {
            Assessments = new List<AssessmentItem>();
        }
    }

    public class AssessmentItem
    {
        public int AssessmentID { get; set; }
        public string AssessmentName { get; set; }
        public string AssessmentType { get; set; }
        public DateTime AssessmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool CanStartAttempt { get; set; }
        public bool AlreadyAttempted { get; set; }
    }
}
