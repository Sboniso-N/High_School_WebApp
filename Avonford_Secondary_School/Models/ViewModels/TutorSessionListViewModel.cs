using System;
using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class TutorSessionListViewModel
    {
        public int TutorClassID { get; set; }
        public string ClassName { get; set; }
        public string GradeName { get; set; }
        public string SubjectName { get; set; }
        public List<TutorSessionViewModel> Sessions { get; set; }
    }

    public class TutorSessionViewModel
    {
        public int TutorSessionID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Mode { get; set; }
        public DateTime SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; }
        public string OnlineMeetingLink { get; set; }
        public bool IsCancelled { get; set; }
        public List<TutorSessionResourceVM> Resources { get; set; }
        public string DisplayTime => $"{SessionDate:yyyy-MM-dd} {StartTime:hh\\:mm} - {EndTime:hh\\:mm}";

        public int TutorClassID { get; internal set; }
    }

    public class TutorSessionResourceVM
    {
        public int ResourceID { get; set; }
        public string FileName { get; set; }
    }


}
