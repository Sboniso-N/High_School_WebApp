using System;
using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class GradeAssessmentsViewModel
    {
        public int SubjectID { get; set; }
        public string SubjectName { get; set; }
        public List<AssessmentGradingItem> Assessments { get; set; }

        public GradeAssessmentsViewModel()
        {
            Assessments = new List<AssessmentGradingItem>();
        }
    }

    public class AssessmentGradingItem
    {
        public int AssessmentID { get; set; }
        public string AssessmentName { get; set; }
        public string AssessmentType { get; set; }
        public DateTime AssessmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}
