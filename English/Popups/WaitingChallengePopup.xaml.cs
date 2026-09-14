using CommunityToolkit.Maui.Views;

namespace English.Popups;

public partial class WaitingChallengePopup : Popup
{
    public WaitingChallengePopup(string friendName)
    {
        InitializeComponent();
        FriendNameLabel.Text = $"الصديق: {friendName}";
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        // إغلاق النافذة وإرجاع قيمة "Cancel" لكي يعرف التطبيق أننا ألغينا الطلب
        Close("Cancel");
    }

    // هذه الدالة سنستدعيها من الخارج عندما يأتي رد من السيرفر بالقبول أو الرفض
    public void CloseWithResult(bool isAccepted)
    {
        Close(isAccepted ? "Accepted" : "Rejected");
    }
}