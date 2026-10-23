using CommunityToolkit.Maui.Views;

namespace English.Popups;

public partial class WaitingChallengePopup : Popup
{
    private string _friendName = "";
    public bool IsCancelled { get; private set; } = false;

    public WaitingChallengePopup(string friendName, bool startInPreparingState = false)
    {
        InitializeComponent();
        _friendName = friendName;

        if (startInPreparingState)
        {
            SetPreparingState();
        }
        else
        {
            SetWaitingForFriendState(friendName);
        }
    }

    public void SetPreparingState()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IconLabel.Text = "⚡";
            TitleLabel.Text = "جارٍ التحقق وتجهيز التحدي...";
            SubtitleLabel.Text = "نتحقق من جاهزية الصديق ونُعد الأسئلة...";
            SubtitleLabel.IsVisible = true;
            FriendNameLabel.Text = $"الصديق: {_friendName}";
            FriendNameLabel.IsVisible = true;
        });
    }

    public void SetWaitingForFriendState(string? friendName = null)
    {
        if (!string.IsNullOrEmpty(friendName)) _friendName = friendName;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            IconLabel.Text = "⏳";
            TitleLabel.Text = "بانتظار قبول التحدي...";
            SubtitleLabel.Text = $"تم إرسال الدعوة إلى {_friendName}";
            SubtitleLabel.IsVisible = true;
            FriendNameLabel.Text = $"الصديق: {_friendName}";
            FriendNameLabel.IsVisible = false;
        });
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        IsCancelled = true;
        Close("Cancel");
    }

    public void CloseWithResult(bool isAccepted)
    {
        Close(isAccepted ? "Accepted" : "Rejected");
    }

    public void CloseWithError(string errorMessage)
    {
        Close($"Error:{errorMessage}");
    }
}