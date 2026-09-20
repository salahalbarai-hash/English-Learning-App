namespace English.Services
{
    public static class TenWords
    {
        // In-memory cache to avoid re-reading and deserializing on every call
        private static List<WordModel>? _cachedWords;

        private static async Task<List<WordModel>> LoadWordsAsync()
        {
            if (_cachedWords != null)
                return _cachedWords;

            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("words.json");
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                _cachedWords = JsonSerializer.Deserialize<List<WordModel>>(json) ?? new List<WordModel>();
                return _cachedWords;
            }
            catch
            {
                return new List<WordModel>();
            }
        }

        /// <summary>
        /// Clears the in-memory cache so next call re-reads from disk.
        /// </summary>
        public static void InvalidateCache() => _cachedWords = null;

        public static async Task<List<WordModel>> GetAllWordsAsync()
        {
            return await LoadWordsAsync();
        }

        public static async Task<List<WordModel>> GetMemorizedWordsAsync()
        {
            try
            {
                var allWords = await LoadWordsAsync();

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

        public static async Task<List<WordModel>> AllAsync()
        {
            try
            {
                var allWords = await LoadWordsAsync();

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

        // Synchronous wrappers kept for backward compatibility with non-async callers
        // These are now safe because they use the cached data after first async load
        public static List<WordModel> GetAllWords()
        {
            if (_cachedWords != null)
                return _cachedWords;

            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("words.json").GetAwaiter().GetResult();
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                _cachedWords = JsonSerializer.Deserialize<List<WordModel>>(json) ?? new List<WordModel>();
                return _cachedWords;
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
                var allWords = GetAllWords();

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
                var allWords = GetAllWords();

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