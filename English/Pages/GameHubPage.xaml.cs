using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Views;
using English.Models;
using English.Popups;

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

    private async void OnTikTokLiveClicked(object sender, EventArgs e)
    {
        var popup = new TikTokLiveSettingsPopup();
        var result = await this.ShowPopupAsync(popup);
        if (result is TikTokLiveConfig config)
        {
            await SafeNavigateAsync(() => new TikTokLiveArenaPage(config));
        }
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
        await Toast.Make($"رصيدك الحالي: {coins} كوينز ذهبي 💰 (تكسبها عند الفوز في التحديات والمسابقات)").Show();
    }
}