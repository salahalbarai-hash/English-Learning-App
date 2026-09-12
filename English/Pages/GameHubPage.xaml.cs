using CommunityToolkit.Maui.Core;

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

        long challengeCount = Preferences.Get("FriendsChallengeCount", 0);
        ChallengeCountLabel.Text = challengeCount.ToString();

        _isNavigating = false;
    }

    private async void OnSmartInspectorTapped(object sender, EventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        // الانتقال المباشر لشاشة المفتش الذكي (تتضمن خيارات اللعب الفردي والتحدي بداخلها)
        await Navigation.PushModalAsync(new SmartInspectorPage());
    }

    private async void OnChoiceChallengeTapped(object sender, EventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        await Navigation.PushModalAsync(new ChoiceChallengePage());
    }

    private async void OnWritingChallengeTapped(object sender, EventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        // الانتقال المباشر لشاشة تحدي الكتابة
        await Navigation.PushModalAsync(new WritingChallengePage());
    }

    private async void OnChallengeFriendTapped(object sender, EventArgs e)
    {
        await Toast.Make($"لديك {ChallengeCountLabel.Text} محاولات تحدي صديق ⚔️").Show();
    }
}