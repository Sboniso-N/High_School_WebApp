using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class TeacherDashboardViewModel
    {
        public List<AssignedSubjectItem> AssignedSubjects { get; set; }
        public TeacherDashboardViewModel()
        {
            AssignedSubjects = new List<AssignedSubjectItem>();
        }
    }

    public class AssignedSubjectItem
    {
        public int GradeID { get; set; }
        public int? StreamID { get; set; }
        public int SubjectID { get; set; }
        public string SubjectName { get; set; }
    }
}
