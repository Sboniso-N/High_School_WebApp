using System;
using System.Collections.Generic;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class AttemptAnswerReview
    {
        public int QuestionID { get; set; }
        public string QuestionText { get; set; }
        public string StudentAnswer { get; set; }
        public decimal ScoreAwarded { get; set; }
        // Assuming a score of 1 indicates a correct answer.
        public bool IsCorrect { get; set; }
    }

    public class AttemptReviewItem
    {
        public int AttemptID { get; set; }
        public DateTime AttemptStartTime { get; set; }
        public DateTime? AttemptEndTime { get; set; }
        public decimal TotalScore { get; set; }
        public decimal MaxScore { get; set; }
        public List<AttemptAnswerReview> Answers { get; set; } = new List<AttemptAnswerReview>();
    }

    public class StudentAttemptReviewViewModel
    {
        public int SubjectID { get; set; }
        public string SubjectName { get; set; }
        public List<AttemptReviewItem> Attempts { get; set; } = new List<AttemptReviewItem>();
    }
}
