using System;
using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class TeacherAllocationHistoryItem
    {
        public int HistoryID { get; set; }
        public int TeacherID { get; set; }
        public string TeacherName { get; set; }
        public int GradeID { get; set; }
        public string GradeName { get; set; }
        public int StreamID { get; set; }
        public string StreamName { get; set; }
        public int SubjectID { get; set; }
        public string SubjectName { get; set; }
        public DateTime AllocationDate { get; set; }
        public int? AllocatedBy { get; set; }
        public string AllocatedByName { get; set; }
    }

    public class TeacherAllocationHistoryViewModel
    {
        public List<TeacherAllocationHistoryItem> HistoryItems { get; set; } = new List<TeacherAllocationHistoryItem>();
    }
}
