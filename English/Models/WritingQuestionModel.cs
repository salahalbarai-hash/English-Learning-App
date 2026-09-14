namespace English.Models
{
    public class WritingQuestionModel
    {
        public required string Tag { get; set; }

        public required string Title { get; set; }

        public required string CorrectAnswer { get; set; }

        public string? SelectedAnswer { get; set; }
    }
}
