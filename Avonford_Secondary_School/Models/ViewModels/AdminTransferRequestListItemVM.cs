using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class AdminTransferRequestListItemVM
    {
        public int TransferRequestID { get; set; }
        public string StudentName { get; set; }
        public int StudentID { get; set; }
        public string OldStream { get; set; }
        public string NewStream { get; set; }
        public int OldGrade { get; set; }
        public int NewGrade { get; set; }
        public string Status { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string StatusColor { get; set; } 
    }

}