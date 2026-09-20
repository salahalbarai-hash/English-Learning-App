using English.Models;
using English.Services;
using System.Collections.Generic;
using System.Linq;

#nullable enable
namespace English.ViewModels
{
    public class WordsVM
    {
        public List<WordModel> Titles { get; set; } = new List<WordModel>();

        public async Task LoadAsync()
        {
            // جلب الكلمات حسب المجموعة الحالية
            var words = await Words.TagAsync(GlobalVariables.CurrentGroup);

            Titles.AddRange(words.Select(title =>
            {
                // إذا كانت الكلمة مقفلة وليست Quiz، استبدالها بعلامة "?"
                if (title.EnglishWord != null && !title.EnglishWord.Contains("Quiz") && title.Locked)
                {
                    title.EnglishWord = "?";
                }

                return title;
            }));
        }
    }
}
