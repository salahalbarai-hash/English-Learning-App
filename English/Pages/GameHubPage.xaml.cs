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

        // الانتقال المباشر لشاشة تحدي الخيارات
        await Navigation.PushModalAsync(new ChoiceChallengePage());
    }
}