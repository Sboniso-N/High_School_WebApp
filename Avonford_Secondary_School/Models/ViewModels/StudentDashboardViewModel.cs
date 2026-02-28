using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class StudentDashboardViewModel
    {
        public string StudentName { get; set; }
        public int Grade { get; set; }
        public string Stream { get; set; }
        public List<StudentSubjectItem> Subjects { get; set; }

        public StudentDashboardViewModel()
        {
            Subjects = new List<StudentSubjectItem>();
        }
    }

    public class StudentSubjectItem
    {
        public int SubjectID { get; set; }
        public string SubjectName { get; set; }
    }
}
