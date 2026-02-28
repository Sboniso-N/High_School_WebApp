using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class StudentTransferTrackingVM
    {
        public int TransferRequestID { get; set; }
        public string OldStream { get; set; }
        public string NewStream { get; set; }
        public int OldGrade { get; set; }
        public int NewGrade { get; set; }
        public string Justification { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class StudentTransferStatusDetailVM
    {
        public int TransferRequestID { get; set; }
        public string OldStream { get; set; }
        public string NewStream { get; set; }
        public int OldGrade { get; set; }
        public int NewGrade { get; set; }
        public string Justification { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<TransferRequestAuditLog> AuditTrail { get; set; }
    }

}