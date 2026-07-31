using System.Text.Json;
using English.Models;

namespace English.Services
{
    public static class TenWords
    {
        public static List<WordModel> GetMemorizedWords()
        {
            var stream = FileSystem.OpenAppPackageFileAsync("words.json").Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var allWords = JsonSerializer.Deserialize<List<WordModel>>(json);

            if (allWords == null || allWords.Count == 0)
                return [];

            int skipCount = Preferences.Get("MemorizedWords", 0);
            return [.. allWords.Take(skipCount)];
        }

        public static List<WordModel> All()
        {
            // قراءة الملف
            var stream = FileSystem.OpenAppPackageFileAsync("words.json").Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var allWords = JsonSerializer.Deserialize<List<WordModel>>(json);

            if (allWords == null || allWords.Count == 0)
                return [];

            int groupSize = 10;

            // خطوة أمان: إذا تجاوز رقم التخطي إجمالي عدد الكلمات الموجودة في الملف، 
            // نرجعه إلى الصفر ليبدأ من جديد لتجنب أي أخطاء (Crash)
            int skipCount = Preferences.Get("MemorizedWords", 0);
            if (skipCount >= allWords.Count)
            {
                skipCount = 0;
                Preferences.Set("MemorizedWords", 0); // تصفير القيمة في الذاكرة أيضاً
            }

            // يتخطى الكلمات حسب قيمة YER ويجلب 10 كلمات بعدها
            return [.. allWords.Skip(skipCount).Take(groupSize)];
        }
    }
}