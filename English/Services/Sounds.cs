using Plugin.Maui.Audio;
using System;

namespace English.Models
{
    public static class Sounds
    {
        private static readonly string[] CorrectSounds = { "correct0.wav", "correct1.wav" };
        private static readonly string[] WrongSounds = { "wrong0.wav", "wrong1.wav" };
        private static readonly string[] GameOverSounds = { "game_over0.wav", "game_over1.wav" };

        private static readonly Random Random = new Random();

        public static string Correct()
        {
            return CorrectSounds[Random.Next(CorrectSounds.Length)];
        }

        public static string Wrong()
        {
            return WrongSounds[Random.Next(WrongSounds.Length)];
        }

        public static string GameOver()
        {
            return GameOverSounds[Random.Next(GameOverSounds.Length)];
        }

        public static string Win() => "win0.wav";
        public static async Task PlayAsync(string fileName)
        {
            try
            {
                var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                var player = AudioManager.Current.CreatePlayer(stream);

                player.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sound error: {ex.Message}");
            }
        }
    }
}
