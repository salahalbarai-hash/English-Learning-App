using English.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace English.Services
{
    public static class Questions
    {
        public static List<QuestionModel> GetQuestions(bool includeArabic = true)
        {
            // 5 اسئلة لكل مجموعة (English to Arabic)
            List<QuestionModel> questions =
            [
                // Group 1
                new() { Tag = "Group 1", Title = "Pray", Options = ["يصلي", "يمشي", "يزور"], CorrectAnswer = "يصلي", Language = "en" },
                new() { Tag = "Group 1", Title = "Help", Options = ["يساعد", "ينظف", "يطبخ"], CorrectAnswer = "يساعد", Language = "en" },
                new() { Tag = "Group 1", Title = "Visit", Options = ["يزور", "يسافر", "يرحل"], CorrectAnswer = "يزور", Language = "en" },
                new() { Tag = "Group 1", Title = "Grandmother", Options = ["جدة", "ام", "عمة"], CorrectAnswer = "جدة", Language = "en" },
                new() { Tag = "Group 1", Title = "Kitchen", Options = ["مطبخ", "غرفة", "منزل"], CorrectAnswer = "مطبخ", Language = "en" },
              
                // Group 2
                new() { Tag = "Group 2", Title = "Drink", Options = ["يشرب", "ياكل", "يغسل"], CorrectAnswer = "يشرب", Language = "en" },
                new() { Tag = "Group 2", Title = "Coffee", Options = ["قهوة", "شاي", "عصير"], CorrectAnswer = "قهوة", Language = "en" },
                new() { Tag = "Group 2", Title = "Watch", Options = ["يشاهد", "يسمع", "يقرا"], CorrectAnswer = "يشاهد", Language = "en" },
                new() { Tag = "Group 2", Title = "Walk", Options = ["يمشي", "يجري", "يقفز"], CorrectAnswer = "يمشي", Language = "en" },
                new() { Tag = "Group 2", Title = "Father", Options = ["والد", "اخ", "خال"], CorrectAnswer = "والد", Language = "en" },
              
                // Group 3
                new() { Tag = "Group 3", Title = "I visit my grandmother", Options = ["انا ازور جدتي", "انا ازور امي", "انا في المنزل"], CorrectAnswer = "انا ازور جدتي", Language = "en" },
                new() { Tag = "Group 3", Title = "I take a shower", Options = ["انا استحم", "انا انظف", "انا اشرب"], CorrectAnswer = "انا استحم", Language = "en" },
                new() { Tag = "Group 3", Title = "I exercise", Options = ["انا اتمرن", "انا العب", "انا اجري"], CorrectAnswer = "انا اتمرن", Language = "en" },
                new() { Tag = "Group 3", Title = "I clean my room", Options = ["انا انظف غرفتي", "انا ارتب سريري", "انا في غرفتي"], CorrectAnswer = "انا انظف غرفتي", Language = "en" },
                new() { Tag = "Group 3", Title = "I help my mother", Options = ["اساعد امي", "انا مع امي", "امي تطبخ"], CorrectAnswer = "اساعد امي", Language = "en" },
              
                // Group 4
                new() { Tag = "Group 4", Title = "I drink coffee", Options = ["انا اشرب القهوة", "انا احب القهوة", "انا اصنع القهوة"], CorrectAnswer = "انا اشرب القهوة", Language = "en" },
                new() { Tag = "Group 4", Title = "I watch a movie", Options = ["انا اشاهد فيلماً", "انا اشاهد التلفاز", "انا في السينما"], CorrectAnswer = "انا اشاهد فيلماً", Language = "en" },
                new() { Tag = "Group 4", Title = "I love my home", Options = ["انا احب منزلي", "انا في منزلي", "هذا منزلي"], CorrectAnswer = "انا احب منزلي", Language = "en" },
                new() { Tag = "Group 4", Title = "A busy day", Options = ["يوم حافل", "يوم طويل", "يوم جميل"], CorrectAnswer = "يوم حافل", Language = "en" },
                new() { Tag = "Group 4", Title = "Rest time", Options = ["وقت الراحة", "وقت النوم", "وقت الفراغ"], CorrectAnswer = "وقت الراحة", Language = "en" }
            ];

            if (includeArabic)
            {
                questions.AddRange([
                    // Group 1
                    new() { Tag = "Group 1", Title = "يصلي", Options = ["Pray", "Eat", "Run"], CorrectAnswer = "Pray", Language = "ar" },
                    new() { Tag = "Group 1", Title = "يساعد", Options = ["Help", "Work", "Talk"], CorrectAnswer = "Help", Language = "ar" },
                    new() { Tag = "Group 1", Title = "يزور", Options = ["Visit", "Go", "Stay"], CorrectAnswer = "Visit", Language = "ar" },
                    new() { Tag = "Group 1", Title = "يستعد", Options = ["Prepare", "Finish", "Start"], CorrectAnswer = "Prepare", Language = "ar" },
                    new() { Tag = "Group 1", Title = "يتمرن", Options = ["Exercise", "Play", "Sleep"], CorrectAnswer = "Exercise", Language = "ar" },
                  
                    // Group 2
                    new() { Tag = "Group 2", Title = "يشرب", Options = ["Drink", "Bake", "Wash"], CorrectAnswer = "Drink", Language = "ar" },
                    new() { Tag = "Group 2", Title = "يمشي", Options = ["Walk", "Sit", "Jump"], CorrectAnswer = "Walk", Language = "ar" },
                    new() { Tag = "Group 2", Title = "يشاهد", Options = ["Watch", "Listen", "Read"], CorrectAnswer = "Watch", Language = "ar" },
                    new() { Tag = "Group 2", Title = "ينظف", Options = ["Clean", "Fix", "Paint"], CorrectAnswer = "Clean", Language = "ar" },
                    new() { Tag = "Group 2", Title = "منزل", Options = ["Home", "Office", "School"], CorrectAnswer = "Home", Language = "ar" },
                  
                    // Group 3
                    new() { Tag = "Group 3", Title = "انا اصلي", Options = ["I pray", "I fast", "I go"], CorrectAnswer = "I pray", Language = "ar" },
                    new() { Tag = "Group 3", Title = "انا اتمرن", Options = ["I exercise", "I eat", "I walk"], CorrectAnswer = "I exercise", Language = "ar" },
                    new() { Tag = "Group 3", Title = "انا انظف غرفتي", Options = ["I clean my room", "I like my room", "I stay in my room"], CorrectAnswer = "I clean my room", Language = "ar" },
                    new() { Tag = "Group 3", Title = "انا استحم", Options = ["I take a shower", "I wash my face", "I am tired"], CorrectAnswer = "I take a shower", Language = "ar" },
                    new() { Tag = "Group 3", Title = "اساعد امي في المطبخ", Options = ["I help my mother in the kitchen", "My mother is in the kitchen", "I love the kitchen"], CorrectAnswer = "I help my mother in the kitchen", Language = "ar" },
                  
                    // Group 4
                    new() { Tag = "Group 4", Title = "انا اشاهد فيلماً", Options = ["I watch a movie", "I hear a movie", "I like the movie"], CorrectAnswer = "I watch a movie", Language = "ar" },
                    new() { Tag = "Group 4", Title = "ذهبت لامشي مع والدي", Options = ["I go for a walk with my father", "I walk with my friend", "My father is walking"], CorrectAnswer = "I go for a walk with my father", Language = "ar" },
                    new() { Tag = "Group 4", Title = "انا اشرب القهوة", Options = ["I drink coffee", "I make coffee", "I want coffee"], CorrectAnswer = "I drink coffee", Language = "ar" },
                    new() { Tag = "Group 4", Title = "انا احب منزلي", Options = ["I love my home", "I leave my home", "This is my home"], CorrectAnswer = "I love my home", Language = "ar" },
                    new() { Tag = "Group 4", Title = "يوم حافل", Options = ["A busy day", "A good day", "A bad day"], CorrectAnswer = "A busy day", Language = "ar" }
                ]);
            }

            return FilterAndShuffle(questions);
        }

        private static List<QuestionModel> FilterAndShuffle(List<QuestionModel> source)
        {
            var filtered = GlobalVariables.CurrentGroup == "Final Exam"
                ? source.OrderBy(_ => Guid.NewGuid()).Take(10)
                : source.Where(q => q.Tag == GlobalVariables.CurrentGroup);

            return [.. filtered
                   .OrderBy(_ => Guid.NewGuid())
                   .Select(q => new QuestionModel
                   {
                       Tag = q.Tag,
                       Title = q.Title,
                       Language = q.Language,
                       CorrectAnswer = q.CorrectAnswer,
                       Options = [.. q.Options.OrderBy(_ => Guid.NewGuid())]
                   })];
        }
    }
}