using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class AddQuestionViewModel
    {
        public int AssessmentID { get; set; }
        public string AssessmentName { get; set; }

        [Required(ErrorMessage = "Question Type is required.")]
        [Display(Name = "Question Type")]
        public string QuestionType { get; set; } // "MCQ" or "Text"

        [Required(ErrorMessage = "Question Text is required.")]
        [Display(Name = "Question Text")]
        public string QuestionText { get; set; }

        // Only relevant if QuestionType = "MCQ"
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public string AnswerKey { get; set; }

        // Show or hide MCQ fields in the view
        public bool ShowMcqFields { get; set; }

        // List of existing questions for display
        public List<ExistingQuestionItem> ExistingQuestions { get; set; }

        public AddQuestionViewModel()
        {
            ExistingQuestions = new List<ExistingQuestionItem>();
        }
    }

    public class ExistingQuestionItem
    {
        public int QuestionID { get; set; }
        public string QuestionType { get; set; }
        public string QuestionText { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public string AnswerKey { get; set; }
    }
}
