using Plugin.Maui.Audio;

public class SoundService
{
    public async Task PlayAsync(string fileName)
    {
        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            var player = AudioManager.Current.CreatePlayer(stream);

            player.PlaybackEnded += (s, e) =>
            {
                player.Dispose();
                stream.Dispose();
            };
            player.Play();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sound error: {ex.Message}");
        }
    }
}