using English.ViewModels;
namespace English.Models
{
    public class ExamStageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? MultiChoiceTemplate { get; set; }
        public DataTemplate? WritingTemplate { get; set; }
        public DataTemplate? ListeningTemplate { get; set; }

        public DataTemplate? DefaultTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            var fallback = DefaultTemplate ?? new DataTemplate();

            if (item == null) return fallback;

            if (item is not ExamStage stage) return fallback;

            return stage switch
            {
                ExamStage.MultiChoice => MultiChoiceTemplate ?? fallback,
                ExamStage.Writing => WritingTemplate ?? fallback,
                ExamStage.Listening => ListeningTemplate ?? fallback,
                _ => fallback
            };
        }
    }
}