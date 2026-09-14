using System.Text.Json;
using CommunityToolkit.Maui.Alerts;
using YoutubeExplode;

namespace English.Pages;

public partial class WordVideosPage : ContentPage
{
    int memorizedWords;

    public WordVideosPage()
    {
        InitializeComponent();

        memorizedWords = Preferences.Get("MemorizedWords", 0);
        WordsInfoLabel.Text = $"لديك {memorizedWords} كلمة محفوظة، سنستخدمها لتثبيت لغتك";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    private async void FindLearningVideo_Clicked(object sender, EventArgs e)
    {
        await SearchVideoAsync();
    }

    private async void AnotherVideo_Clicked(object sender, EventArgs e)
    {
        await SearchVideoAsync();
    }

    private async Task<SearchVideoAsyncResult?> GetWordsForAI()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("words.json");
            var words = await JsonSerializer.DeserializeAsync<List<WordItem>>(stream);

            if (words == null || words.Count == 0)
                return null;

            var list = words
                .Take(memorizedWords)
                .Select(x => x.EnglishWord)
                .ToList();

            return new SearchVideoAsyncResult
            {
                Words = string.Join(", ", list)
            };
        }
        catch (Exception ex)
        {
            await Toast.Make(ex.Message).Show();
            return null;
        }
    }

    private async Task SearchVideoAsync()
    {
        if (LoadingOverlay.IsVisible)
            return;

        try
        {
            LoadingOverlay.IsVisible = true;
            SearchButton.IsEnabled = false;

            var data = await GetWordsForAI();

            if (data == null)
            {
                await Toast.Make("لم يتم العثور على الكلمات").Show();
                return;
            }

            string prompt = $"""
أنا أطور تطبيقًا لتعليم اللغة الإنجليزية للمبتدئين.

هذه أول {memorizedWords} كلمة تعلمها الطالب:

{data.Words}

مهم جدًا:

أنت لا تملك الحق في اختراع أو تخمين أي رابط YouTube.

إذا لم تتمكن من الوصول إلى YouTube والتحقق من وجود الفيديو فعليًا، فأعد هذه الكلمة فقط:

NOT_FOUND

ولا تنشئ أي رابط من ذاكرتك.

إذا كنت قادرًا على التحقق من وجود الفيديو، فأعد رابط فيديو YouTube حقيقي فقط يحقق الشروط التالية:

- قصة قصيرة أو محادثة يومية.
- مناسب للمستوى A1.
- ليس فيديو لحفظ الكلمات.
- مدة الفيديو بين دقيقتين و10 دقائق.
- يحتوي على أكبر عدد ممكن من الكلمات السابقة.
- الفيديو متاح وغير محذوف.
- أعد الرابط الكامل فقط بهذا الشكل:
https://www.youtube.com/watch?v=VIDEO_ID

لا تضف أي شرح أو أي نص آخر.
""";

            string videoUrl = await Service.AskAIAsync(prompt);

            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                await Toast.Make("تعذر جلب رابط الفيديو").Show();
                return;
            }
            var streamUrl = await Get360StreamUrl(videoUrl);

            if (!string.IsNullOrEmpty(streamUrl))
            {
                VideoPlayer.Source = streamUrl;
                VideoPlayer.Play();
            }
            else
            {
                await Toast.Make("تعذر استخراج رابط تشغيل الفيديو").Show();
            }
        }
        catch (Exception ex)
        {
            await Toast.Make(ex.Message).Show();
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
            SearchButton.IsEnabled = true;
        }
    }

    private async Task<string?> Get360StreamUrl(string videoUrl)
    {
        try
        {
            var youtube = new YoutubeClient();

            var video = await youtube.Videos.GetAsync(videoUrl);

            var manifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);

            var stream = manifest
                .GetMuxedStreams()
                .Where(x => x.VideoQuality.MaxHeight <= 360)
                .OrderByDescending(x => x.VideoQuality.MaxHeight)
                .FirstOrDefault()
                ?? manifest.GetMuxedStreams()
                    .OrderByDescending(x => x.VideoQuality.MaxHeight)
                    .FirstOrDefault();

            return stream?.Url;
        }
        catch(Exception ex)
        {
            string errorMessage = ex.Message;
            return null;
        }
    }
}

public class SearchVideoAsyncResult
{
    public string Words { get; set; } = "";
}