using System.Collections.Generic;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class ChangeTeacherViewModel
    {
        public int GradeSubjectTeacherID { get; set; }
        public int GradeID { get; set; }
        public int? StreamID { get; set; }
        public int SubjectID { get; set; }
        public string SubjectName { get; set; }
        public string StreamName { get; set; }

        // The teacher dropdown – only teachers qualified (i.e. having the subject in one of their fields) will be listed.
        public int? SelectedTeacherID { get; set; }
        public List<SelectListItem> TeacherList { get; set; } = new List<SelectListItem>();
    }
}
