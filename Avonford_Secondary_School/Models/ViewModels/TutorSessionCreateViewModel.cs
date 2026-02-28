using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class TutorSessionCreateViewModel
    {
        public int TutorClassID { get; set; }
        public string ClassName { get; set; }
        public string Mode { get; set; }
        [Required]
        [StringLength(150, MinimumLength = 5)]
        public string Title { get; set; }
        [Required]
        [StringLength(1500, MinimumLength = 10)]
        public string Description { get; set; }
        [Required]
        [DataType(DataType.Date)]
        public DateTime SessionDate { get; set; }
        [Required]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; }
        [Required]
        [DataType(DataType.Time)]
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; }
        public string OnlineMeetingLink { get; set; }
        public string Instructions { get; set; }
        public IEnumerable<SelectListItem> ModeOptions { get; set; }
        public List<TutorSessionResourceVM> ExistingResources { get; set; }
       
    }
}
