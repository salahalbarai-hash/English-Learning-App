using CommunityToolkit.Maui.Alerts;
using English.Models;
using English.Services;

namespace English.Pages;

public partial class GameHubPage : ContentPage
{
    private bool _isNavigating = false;

    public GameHubPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isNavigating = false;
        UpdateHeaderStats();
        _ = Task.Run(async () =>
        {
            if (await Service.SyncUserStatsWithServerAsync())
            {
                MainThread.BeginInvokeOnMainThread(UpdateHeaderStats);
            }
        });
    }

    private void UpdateHeaderStats()
    {
        long challenges = Preferences.Get("FriendsChallengeCount", 0L);
        long coins = Preferences.Get("Coins", 0L);

        ChallengeCountLabel.Text = challenges.ToString();
        CoinsCountLabel.Text = coins.ToString();
    }

    private async Task SafeNavigateAsync(Func<Page> pageFactory)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        try
        {
            var page = pageFactory();
            await Navigation.PushModalAsync(page);
        }
        catch (Exception ex)
        {
            await Toast.Make(ex.Message).Show();
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private async void OnWordMatrixClicked(object sender, EventArgs e)
    {
        var memorizedWords = await TenWords.GetMemorizedWordsAsync();
        if (memorizedWords == null || memorizedWords.Count == 0)
        {
            await Toast.Make("لم تقم بحفظ أي كلمات بعد 📚! أتمم اختبار الكلمات العشر لتتمكن من اللعب بكلماتك المحفوظة هنا.").Show();
            return;
        }

        if (memorizedWords.Count < 3)
        {
            await Toast.Make("يجب أن تحفظ 3 كلمات على الأقل لتتمكن من خوض هذا التحدي 📚").Show();
            return;
        }

        var config = GameSessionConfig.FromMemorizedWords(memorizedWords, GameDifficulty.Medium);
        await SafeNavigateAsync(() => new WordMatrixPage(config));
    }

    private async void OnSkywardBirdClicked(object sender, EventArgs e)
    {
        var memorizedWords = await TenWords.GetMemorizedWordsAsync();
        if (memorizedWords == null || memorizedWords.Count == 0)
        {
            await Toast.Make("لم تقم بحفظ أي كلمات بعد 📚! أتمم اختبار الكلمات العشر لتتمكن من اللعب بكلماتك المحفوظة هنا.").Show();
            return;
        }

        await SafeNavigateAsync(() => new SkywardBirdPage());
    }

    private async void OnSpellReactorClicked(object sender, EventArgs e)
    {
        var memorizedWords = await TenWords.GetMemorizedWordsAsync();
        if (memorizedWords == null || memorizedWords.Count == 0)
        {
            await Toast.Make("لم تقم بحفظ أي كلمات بعد 📚! أتمم اختبار الكلمات العشر لتتمكن من اللعب بكلماتك المحفوظة هنا.").Show();
            return;
        }

        var config = GameSessionConfig.FromMemorizedWords(memorizedWords, GameDifficulty.Medium);
        await SafeNavigateAsync(() => new SpellReactorPage(config));
    }

    private async void OnSmartInspectorClicked(object sender, EventArgs e)
    {
        await SafeNavigateAsync(() => new SmartInspectorPage());
    }

    private async void OnChoiceChallengeClicked(object sender, EventArgs e)
    {
        await SafeNavigateAsync(() => new ChoiceChallengePage());
    }

    private async void OnWritingChallengeClicked(object sender, EventArgs e)
    {
        await SafeNavigateAsync(() => new WritingChallengePage());
    }

    private async void OnChallengeFriendTapped(object sender, EventArgs e)
    {
        long count = Preferences.Get("FriendsChallengeCount", 0L);
        await Toast.Make($"لديك {count} محاولات لتحدي الأصدقاء ⚔️ (تكسب محاولات جديدة عند إتمام وحفظ الكلمات)").Show();
    }

    private async void OnCoinsBadgeTapped(object sender, EventArgs e)
    {
        long coins = Preferences.Get("Coins", 0L);
        await Toast.Make($"رصيدك الحالي: {coins} كوينز ذهبي 💰 (خاصة بمبارزات تحدي الأصدقاء ⚔️)").Show();
    }
}
