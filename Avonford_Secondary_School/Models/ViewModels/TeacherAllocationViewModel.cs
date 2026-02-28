using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class SubjectAllocationItem
    {
        public int GradeSubjectTeacherID { get; set; } 
        public int GradeID { get; set; }
        public int? StreamID { get; set; }
        public int SubjectID { get; set; }
        public string SubjectName { get; set; }
        public int? TeacherID { get; set; }
        public string TeacherName { get; set; } 
    }

    public class TeacherAllocationViewModel
    {
        public int SelectedGrade { get; set; }
        public int? SelectedStreamID { get; set; }
        public List<SubjectAllocationItem> Subjects { get; set; }

        // Collection for stream dropdowns
        public List<StreamItem> Streams { get; set; }
        public class StreamItem
        {
            public int StreamID { get; set; }
            public string StreamName { get; set; }
        }

        public TeacherAllocationViewModel()
        {
            Subjects = new List<SubjectAllocationItem>();
            Streams = new List<StreamItem>();
        }
    }
}
