namespace English.Pages;

public partial class GameHubPage : ContentPage
{
    private bool _isNavigating = false;

    public GameHubPage()
    {
        InitializeComponent();

        foreach (var child in CardsLayout.Children.OfType<VisualElement>())
        {
            child.Opacity = 0;
            child.TranslationY = 40;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        ChallengeCountLabel.Text = Preferences.Get("FriendsChallengeCount", 0L).ToString();
        _isNavigating = false;

        // حركة متتالية (Staggered Animation) لكروت الألعاب فقط (الهدير يبقى ثابتاً)
        await Task.Delay(100);
        int staggerDelay = 0;
        foreach (var child in CardsLayout.Children.OfType<VisualElement>())
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(staggerDelay);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    child.FadeTo(1, 500, Easing.SpringOut);
                    child.TranslateTo(0, 0, 500, Easing.SpringOut);
                });
            });
            staggerDelay += 80;
        }
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