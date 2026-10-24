using CommunityToolkit.Maui.Views;

namespace English.Popups;

public class AutoReciteConfig
{
    public int WordRepeats { get; set; } = 2;
    public bool IsInfiniteLoop { get; set; } = true;
    public int GroupRounds { get; set; } = 2;
}

public partial class AutoReciteSettingsPopup : Popup
{
    private int _wordRepeats = 2;
    private bool _isInfinite = true;
    private int _groupRounds = 2;

    private static readonly SolidColorBrush ActiveStroke = new(Color.FromArgb("#38BDF8"));
    private static readonly SolidColorBrush InactiveStroke = new(Color.FromArgb("#334155"));
    private static readonly Color ActiveBg = Color.FromArgb("#14233D");
    private static readonly Color InactiveBg = Color.FromArgb("#1E293B");
    private static readonly Color ActiveText = Color.FromArgb("#38BDF8");
    private static readonly Color InactiveText = Color.FromArgb("#94A3B8");

    public AutoReciteSettingsPopup()
    {
        InitializeComponent();

        _wordRepeats = Preferences.Get("AutoRecite_WordRepeats", 2);
        if (_wordRepeats < 1) _wordRepeats = 2;
        _isInfinite = Preferences.Get("AutoRecite_Infinite", true);
        _groupRounds = Preferences.Get("AutoRecite_GroupRounds", 2);
        if (_groupRounds < 1) _groupRounds = 2;

        WordRepeatsLabel.Text = _wordRepeats.ToString();
        GroupRoundsLabel.Text = _groupRounds.ToString();

        UpdateLoopModeUi();
        UpdateWordChipsUi();
        UpdateRoundChipsUi();
    }

    private void UpdateLoopModeUi()
    {
        SetPillState(BtnInfiniteLoop, TxtInfiniteLoop, _isInfinite);
        SetPillState(BtnCustomLoop, TxtCustomLoop, !_isInfinite);
        CustomRoundsContainer.IsVisible = !_isInfinite;
        InfiniteHintLabel.IsVisible = _isInfinite;
    }

    private static void SetPillState(Border border, Label label, bool isActive)
    {
        border.Stroke = isActive ? ActiveStroke : InactiveStroke;
        border.BackgroundColor = isActive ? ActiveBg : InactiveBg;
        label.TextColor = isActive ? ActiveText : InactiveText;
    }

    private void UpdateWordChipsUi()
    {
        SetChipState(ChipW1, _wordRepeats == 1);
        SetChipState(ChipW2, _wordRepeats == 2);
        SetChipState(ChipW5, _wordRepeats == 5);
        SetChipState(ChipW10, _wordRepeats == 10);
        SetChipState(ChipW20, _wordRepeats == 20);
    }

    private void UpdateRoundChipsUi()
    {
        SetChipState(ChipR1, _groupRounds == 1);
        SetChipState(ChipR2, _groupRounds == 2);
        SetChipState(ChipR5, _groupRounds == 5);
        SetChipState(ChipR10, _groupRounds == 10);
    }

    private static void SetChipState(Border chip, bool isActive)
    {
        chip.Stroke = isActive ? ActiveStroke : InactiveStroke;
        chip.BackgroundColor = isActive ? ActiveBg : InactiveBg;
        if (chip.Content is Label lbl)
        {
            lbl.TextColor = isActive ? ActiveText : InactiveText;
        }
    }

    #region Word Repeats Handlers

    private void OnWordMinusTapped(object sender, TappedEventArgs e)
    {
        if (_wordRepeats > 1)
        {
            SetWordRepeats(_wordRepeats - 1);
        }
    }

    private void OnWordPlusTapped(object sender, TappedEventArgs e)
    {
        if (_wordRepeats < 100)
        {
            SetWordRepeats(_wordRepeats + 1);
        }
    }

    private async void OnWordLabelTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
            {
                var input = await page.DisplayPromptAsync("تكرار الكلمة", "أدخل عدد مرات تكرار الكلمة (1 - 100):", initialValue: _wordRepeats.ToString(), keyboard: Keyboard.Numeric);
                if (int.TryParse(input, out int val) && val >= 1)
                {
                    SetWordRepeats(Math.Clamp(val, 1, 100));
                }
            }
        }
        catch { }
    }

    private void OnChipW1Tapped(object sender, TappedEventArgs e) => SetWordRepeats(1);
    private void OnChipW2Tapped(object sender, TappedEventArgs e) => SetWordRepeats(2);
    private void OnChipW5Tapped(object sender, TappedEventArgs e) => SetWordRepeats(5);
    private void OnChipW10Tapped(object sender, TappedEventArgs e) => SetWordRepeats(10);
    private void OnChipW20Tapped(object sender, TappedEventArgs e) => SetWordRepeats(20);

    private void SetWordRepeats(int n)
    {
        _wordRepeats = Math.Clamp(n, 1, 100);
        WordRepeatsLabel.Text = _wordRepeats.ToString();
        UpdateWordChipsUi();
    }

    #endregion

    #region Group Rounds Handlers

    private void OnInfiniteLoopTapped(object sender, TappedEventArgs e)
    {
        _isInfinite = true;
        UpdateLoopModeUi();
    }

    private void OnCustomLoopTapped(object sender, TappedEventArgs e)
    {
        _isInfinite = false;
        UpdateLoopModeUi();
    }

    private void OnRoundMinusTapped(object sender, TappedEventArgs e)
    {
        if (_groupRounds > 1)
        {
            SetGroupRounds(_groupRounds - 1);
        }
    }

    private void OnRoundPlusTapped(object sender, TappedEventArgs e)
    {
        if (_groupRounds < 100)
        {
            SetGroupRounds(_groupRounds + 1);
        }
    }

    private async void OnRoundLabelTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
            {
                var input = await page.DisplayPromptAsync("عدد الدورات", "أدخل عدد دورات المجموعة (1 - 100):", initialValue: _groupRounds.ToString(), keyboard: Keyboard.Numeric);
                if (int.TryParse(input, out int val) && val >= 1)
                {
                    SetGroupRounds(Math.Clamp(val, 1, 100));
                }
            }
        }
        catch { }
    }

    private void OnChipR1Tapped(object sender, TappedEventArgs e) => SetGroupRounds(1);
    private void OnChipR2Tapped(object sender, TappedEventArgs e) => SetGroupRounds(2);
    private void OnChipR5Tapped(object sender, TappedEventArgs e) => SetGroupRounds(5);
    private void OnChipR10Tapped(object sender, TappedEventArgs e) => SetGroupRounds(10);

    private void SetGroupRounds(int n)
    {
        _groupRounds = Math.Clamp(n, 1, 100);
        GroupRoundsLabel.Text = _groupRounds.ToString();
        UpdateRoundChipsUi();
    }

    #endregion

    private void OnStartClicked(object sender, EventArgs e)
    {
        Preferences.Set("AutoRecite_WordRepeats", _wordRepeats);
        Preferences.Set("AutoRecite_Infinite", _isInfinite);
        Preferences.Set("AutoRecite_GroupRounds", _groupRounds);

        Close(new AutoReciteConfig
        {
            WordRepeats = _wordRepeats,
            IsInfiniteLoop = _isInfinite,
            GroupRounds = _groupRounds
        });
    }

    private void OnCloseClicked(object sender, EventArgs e)
    {
        Close(null);
    }
}
