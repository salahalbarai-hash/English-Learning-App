namespace English.Models
{
    public class QuestionModel
    {
        // المجموعة التي تنتمي إليها الكلمة او السؤال
        public required string Tag { get; set; }

        // نص السؤال او الكلمة العربية/الإنجليزية
        public required string Title { get; set; }

        // خيارات الإجابة
        public required string[] Options { get; set; }

        // الإجابة الصحيحة
        public required string CorrectAnswer { get; set; }

        // الإجابة المختارة من قبل المستخدم (يمكن ان تكون فارغة)
        public string? SelectedAnswer { get; set; }

        // لغة السؤال "ar" او "en"
        public required string Language { get; set; }
    }
}
