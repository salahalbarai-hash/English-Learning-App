namespace English.Models
{
    public enum GameLaunchMode
    {
        /// <summary>
        /// تثبيت الكلمات العشر الحالية المستعرضة في صفحة الكلمات
        /// </summary>
        TenWordsPractice,

        /// <summary>
        /// مراجعة وتحدي الكلمات المحفوظة للمستخدم
        /// </summary>
        MemorizedWordsReview
    }

    public enum GameDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    public class GameSessionConfig
    {
        public GameLaunchMode Mode { get; set; } = GameLaunchMode.TenWordsPractice;
        public GameDifficulty Difficulty { get; set; } = GameDifficulty.Medium;
        public List<WordModel> Words { get; set; } = new();
        public string SourceTitle { get; set; } = "الكلمات الحالية";

        public static GameSessionConfig FromTenWords(IEnumerable<WordModel> currentWords, GameDifficulty difficulty = GameDifficulty.Medium)
        {
            return new GameSessionConfig
            {
                Mode = GameLaunchMode.TenWordsPractice,
                Difficulty = difficulty,
                Words = currentWords?.Where(w => !w.Locked).ToList() ?? new List<WordModel>(),
                SourceTitle = "تثبيت الكلمات العشر ⚡"
            };
        }

        public static GameSessionConfig FromMemorizedWords(IEnumerable<WordModel> memorizedWords, GameDifficulty difficulty = GameDifficulty.Medium, int wordLimit = 15)
        {
            var wordList = memorizedWords?.Where(w => !string.IsNullOrWhiteSpace(w.EnglishWord) && !string.IsNullOrWhiteSpace(w.ArabicWord)).ToList() ?? new List<WordModel>();

            // خلط الكلمات المحفوظة عشوائياً واختيار عدد مناسب للجولة
            var shuffled = wordList.OrderBy(_ => Guid.NewGuid()).Take(Math.Min(wordLimit, wordList.Count)).ToList();

            return new GameSessionConfig
            {
                Mode = GameLaunchMode.MemorizedWordsReview,
                Difficulty = difficulty,
                Words = shuffled,
                SourceTitle = "الكلمات المحفوظة 🧠"
            };
        }
    }
}
