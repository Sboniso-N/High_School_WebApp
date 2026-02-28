using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class AssessmentGradingViewModel
    {
        public int AttemptID { get; set; }
        public int AssessmentID { get; set; }
        public string AssessmentName { get; set; }
        public string StudentName { get; set; }
        public List<GradingQuestionResponse> GradingQuestions { get; set; }

        public AssessmentGradingViewModel()
        {
            GradingQuestions = new List<GradingQuestionResponse>();
        }
    }

    public class GradingQuestionResponse
    {
        public int QuestionID { get; set; }
        public string QuestionType { get; set; }
        public string QuestionText { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public string StudentAnswer { get; set; }
        // This property will be bound to the checkbox.
        public bool IsCorrect { get; set; }
        // ScoreAwarded will be computed automatically.
        public decimal ScoreAwarded { get; set; }
    }

}
