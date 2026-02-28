using System;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class NotificationVM
    {
        public int NotificationID { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsRead { get; set; }
        public int? RelatedSessionID { get; set; }
    }
}
