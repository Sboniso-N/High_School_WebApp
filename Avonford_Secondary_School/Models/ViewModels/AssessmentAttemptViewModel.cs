using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class AssessmentAttemptViewModel
    {
        public int AttemptID { get; set; }
        public int AssessmentID { get; set; }
        public string AssessmentName { get; set; }
        public string Instructions { get; set; }
        [Display(Name = "Assessment Date")]
        public DateTime AssessmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        // This property is used by the client-side timer (server sends the target end datetime)
        public DateTime TimerEnd { get; set; }

        // List of questions for the attempt.
        public List<QuestionResponseItem> Questions { get; set; }
        // Holds student responses
        public List<QuestionResponseItem> Responses { get; set; }

        public AssessmentAttemptViewModel()
        {
            Questions = new List<QuestionResponseItem>();
            Responses = new List<QuestionResponseItem>();
        }
    }

    public class QuestionResponseItem
    {
        public int QuestionID { get; set; }
        public string QuestionType { get; set; }
        public string QuestionText { get; set; }
        // For MCQ
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        // For responses: if MCQ, holds selected option; if text, holds text answer.
        [Required(ErrorMessage = "Please provide an answer.")]
        public string StudentAnswer { get; set; }
    }
}
