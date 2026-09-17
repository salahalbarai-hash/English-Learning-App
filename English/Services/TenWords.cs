namespace English.Services
{
    public static class TenWords
    {
        public static List<WordModel> GetAllWords()
        {
            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("words.json").GetAwaiter().GetResult();
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                return JsonSerializer.Deserialize<List<WordModel>>(json) ?? new List<WordModel>();
            }
            catch
            {
                return new List<WordModel>();
            }
        }

        public static List<WordModel> GetMemorizedWords()
        {
            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("words.json").GetAwaiter().GetResult();
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                var allWords = JsonSerializer.Deserialize<List<WordModel>>(json) ?? new List<WordModel>();

                if (allWords.Count == 0)
                    return [];

                int memorizedCount = Preferences.Get("MemorizedWords", 0);
                if (memorizedCount <= 0)
                    return [];

                return allWords.Take(memorizedCount).ToList();
            }
            catch
            {
                return new List<WordModel>();
            }
        }

        public static List<WordModel> All()
        {
            try
            {
                // قراءة الملف
                using var stream = FileSystem.OpenAppPackageFileAsync("words.json").GetAwaiter().GetResult();
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                var allWords = JsonSerializer.Deserialize<List<WordModel>>(json) ?? [];

                if (allWords.Count == 0)
                    return [];

                int groupSize = 10;

                int skipCount = Preferences.Get("MemorizedWords", 0);
                if (skipCount >= allWords.Count)
                {
                    skipCount = Math.Max(0, allWords.Count - groupSize);
                }

                return [.. allWords.Skip(skipCount).Take(groupSize)];
            }
            catch
            {
                return [];
            }
        }
    }
}