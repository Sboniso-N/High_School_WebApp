using System;
using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class ViewAttemptsViewModel
    {
        public int AssessmentID { get; set; }
        public string AssessmentName { get; set; }
        public List<AttemptItem> Attempts { get; set; }

        public ViewAttemptsViewModel()
        {
            Attempts = new List<AttemptItem>();
        }
    }

    public class AttemptItem
    {
        public int AttemptID { get; set; }
        public int StudentID { get; set; }
        public string StudentName { get; set; }
        public DateTime AttemptStartTime { get; set; }
    }
}
