using System;
using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class PendingApplicationsItem
    {
        public int ApplicationID { get; set; }
        public string FullName { get; set; }
        public int SelectedGrade { get; set; }
        public DateTime CreatedDate { get; set; }
        public string ApplicationStatus { get; set; }
    }

    public class PendingApplicationsViewModel
    {
        public List<PendingApplicationsItem> PendingList { get; set; }
    }
}
