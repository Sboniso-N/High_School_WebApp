using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class AttendSession
    {
    }


    public class UpcomingSessionVM
    {
        public int SessionId { get; set; }
        public int SessionID { get; internal set; }
        public int? TutorClassID { get; set; }
        public string ClassName { get; set; }
        public string Subject { get; set; }
        public string TutorName { get; set; }
        public string Mode { get; set; }
        public DateTime SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; }
        public string OnlineMeetingLink { get; set; }
        public bool CanJoin { get; set; }
        public bool CanViewDirections { get; set; }
        public bool CanConfirmAttendance { get; set; }
        public bool HasAttended { get; set; }
        public List<UpcomingSessionVM> UpcomingSessions { get; set; } // Replace List<string>
        public string TutorProfilePic { get; set; } // Already present? Ensure it's used
        public string DisplayString { get; internal set; }
    
       
     

    }

    public class QRCodeVM
    {
        public string QRData { get; set; }
        public byte[] QRImageBytes { get; set; }
    }

    public class AttendanceConfirmationVM
    {
        public string StudentName { get; set; }
        public string ClassName { get; set; }
        public string TutorName { get; set; }
        public DateTime TimeConfirmed { get; set; }
        public string Status { get; set; }
    }

    public class SessionFeedbackVM
    {
        public int TutorSessionID { get; set; }
        public int StudentID { get; set; }
        public int TutorID { get; set; }
        public int Rating { get; set; }
        public string Feedback { get; set; }
        public bool Submitted { get; set; }
        public string TutorName { get; set; }
        public string ClassName { get; set; }
        public DateTime SessionDate { get; set; }
    }

}