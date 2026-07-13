using English.Models;
using Microsoft.Maui.Storage;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace English.Services
{
    public static class Words
    {
        private static readonly string FilePath = Path.Combine(FileSystem.AppDataDirectory, "LockFile.json");

        public static List<WordModel> Tag(string tag)
        {
            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath, "{}");
            }

            var lockData = JObject.Parse(File.ReadAllText(FilePath));

            var words = new List<WordModel>
            {
                // ===== Group 1: Daily Actions 1 (كلمات 1) =====
                new() { Tag = "Group 1", ArabicWord = "يصلي", EnglishWord = "Pray", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 1", ArabicWord = "يساعد", EnglishWord = "Help", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 1", ArabicWord = "مطبخ", EnglishWord = "Kitchen", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 1", ArabicWord = "يزور", EnglishWord = "Visit", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 1", ArabicWord = "جدة", EnglishWord = "Grandmother", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 1", ArabicWord = "يتمرن", EnglishWord = "Exercise", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 1", ArabicWord = "يستحم", EnglishWord = "Take a shower", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 1", ArabicWord = "ينظف", EnglishWord = "Clean", CurrentLanguage = "en", Locked = false, Padding = 70 },

                // ===== Group 2: Daily Actions 2 (كلمات 2) =====
                new() { Tag = "Group 2", ArabicWord = "غرفة", EnglishWord = "Room", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 2", ArabicWord = "يشاهد", EnglishWord = "Watch", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 2", ArabicWord = "فيلم", EnglishWord = "Movie", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 2", ArabicWord = "يشرب", EnglishWord = "Drink", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 2", ArabicWord = "قهوة", EnglishWord = "Coffee", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 2", ArabicWord = "يمشي", EnglishWord = "Walk", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 2", ArabicWord = "والد / اب", EnglishWord = "Father", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 2", ArabicWord = "منزل", EnglishWord = "Home", CurrentLanguage = "en", Locked = false, Padding = 70 },

                // ===== Group 3: Routine Sentences 1 (جمل 1) =====
                new() { Tag = "Group 3", ArabicWord = "انا اصلي", EnglishWord = "I pray", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 3", ArabicWord = "اساعد امي في المطبخ", EnglishWord = "I help my mother in the kitchen", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 3", ArabicWord = "انا ازور جدتي", EnglishWord = "I visit my grandmother", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 3", ArabicWord = "انا اتمرن", EnglishWord = "I exercise", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 3", ArabicWord = "انا استحم", EnglishWord = "I take a shower", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 3", ArabicWord = "انا انظف غرفتي", EnglishWord = "I clean my room", CurrentLanguage = "en", Locked = false, Padding = 70 },

                // ===== Group 4: Routine Sentences 2 (جمل 2) =====
                new() { Tag = "Group 4", ArabicWord = "انا اشاهد فيلماً", EnglishWord = "I watch a movie", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 4", ArabicWord = "انا اشرب القهوة", EnglishWord = "I drink coffee", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 4", ArabicWord = "ذهبت لامشي مع وال" +"دي", EnglishWord = "I go for a walk with my father", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 4", ArabicWord = "انا احب منزلي", EnglishWord = "I love my home", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 4", ArabicWord = "يوم حافل", EnglishWord = "A busy day", CurrentLanguage = "en", Locked = false, Padding = 70 },
                new() { Tag = "Group 4", ArabicWord = "وقت الراحة", EnglishWord = "Rest time", CurrentLanguage = "en", Locked = false, Padding = 70 },
            };

            // إضافة كويزات المجموعات بشكل ديناميكي
            for (int i = 1; i <= 4; i++)
            {
                string groupTag = $"Group {i}";
                words.Add(new WordModel { Tag = groupTag, ArabicWord = "Quiz Options", EnglishWord = "Quiz Options", CurrentLanguage = "en", Locked = lockData.Value<bool?>($"{groupTag}.Quiz Options") ?? false, Padding = 100 });
                words.Add(new WordModel { Tag = groupTag, ArabicWord = "Quiz Writing", EnglishWord = "Quiz Writing", CurrentLanguage = "en", Locked = lockData.Value<bool?>($"{groupTag}.Quiz Writing") ?? false, Padding = 100 });
                words.Add(new WordModel { Tag = groupTag, ArabicWord = "Quiz Listening", EnglishWord = "Quiz Listening", CurrentLanguage = "en", Locked = lockData.Value<bool?>($"{groupTag}.Quiz Listening") ?? false, Padding = 100 });
            }

            return words.Where(w => w.Tag == tag).ToList();
        }

        public static bool AllValuesAreFalse()
        {
            if (!File.Exists(FilePath)) return true;
            var lockData = JObject.Parse(File.ReadAllText(FilePath));
            return lockData.Properties().Where(p => p.Value.Type == JTokenType.Boolean).All(p => !p.Value.Value<bool>());
        }
    }
}