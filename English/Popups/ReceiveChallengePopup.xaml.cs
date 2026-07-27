using CommunityToolkit.Maui.Views;

namespace English.Popups;

public partial class ReceiveChallengePopup : Popup
{
    public ReceiveChallengePopup(string senderName, string category)
    {
        InitializeComponent();
        MessageLabel.Text = $"تلقيت طلب تحدٍ من الصديق ({senderName}) في قسم ({category})!";
    }

    private void OnDeclineClicked(object sender, EventArgs e)
    {
        Close(false); // إرجاع false يعني رفض التحدي
    }

    private void OnAcceptClicked(object sender, EventArgs e)
    {
        Close(true); // إرجاع true يعني قبول التحدي
    }
}