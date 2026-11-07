namespace English.Pages;

public partial class MemorySecretsPage : ContentPage
{
    public MemorySecretsPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void OnToggleStrategy1(object sender, EventArgs e) => ToggleCard(StrategyDetails1, Chevron1);
    private void OnToggleStrategy2(object sender, EventArgs e) => ToggleCard(StrategyDetails2, Chevron2);
    private void OnToggleStrategy3(object sender, EventArgs e) => ToggleCard(StrategyDetails3, Chevron3);
    private void OnToggleStrategy4(object sender, EventArgs e) => ToggleCard(StrategyDetails4, Chevron4);
    private void OnToggleStrategy5(object sender, EventArgs e) => ToggleCard(StrategyDetails5, Chevron5);
    private void OnToggleStrategy6(object sender, EventArgs e) => ToggleCard(StrategyDetails6, Chevron6);

    private async void ToggleCard(VerticalStackLayout details, Label chevron)
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
        bool willExpand = !details.IsVisible;

        if (willExpand)
        {
            details.Opacity = 0;
            details.IsVisible = true;
            _ = details.FadeTo(1, 200, Easing.CubicOut);
            await chevron.RotateTo(180, 200, Easing.CubicOut);
        }
        else
        {
            await details.FadeTo(0, 150, Easing.CubicIn);
            details.IsVisible = false;
            await chevron.RotateTo(0, 200, Easing.CubicOut);
        }
    }

    private void OnExpandAllClicked(object sender, EventArgs e)
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
        SetAllCards(true);
    }

    private void OnCollapseAllClicked(object sender, EventArgs e)
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
        SetAllCards(false);
    }

    private void SetAllCards(bool expand)
    {
        var cards = new (VerticalStackLayout details, Label chevron)[]
        {
            (StrategyDetails1, Chevron1),
            (StrategyDetails2, Chevron2),
            (StrategyDetails3, Chevron3),
            (StrategyDetails4, Chevron4),
            (StrategyDetails5, Chevron5),
            (StrategyDetails6, Chevron6),
        };

        foreach (var (details, chevron) in cards)
        {
            details.IsVisible = expand;
            details.Opacity = expand ? 1 : 0;
            chevron.Rotation = expand ? 180 : 0;
        }
    }
}


