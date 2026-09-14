using CommunityToolkit.Maui.Views;

namespace English.Popups;

public partial class ReceiveChallengePopup : Popup
{
    public ReceiveChallengePopup(string senderName, string category, string? word = null, string? arabic = null)
    {
        InitializeComponent();
        if (!string.IsNullOrEmpty(word))
        {
            MessageLabel.Text = $"{senderName} أرسل تحديًا: ({category})\nالكلمة: {word} - {arabic}";
            WordLabel.Text = word;
            ArabicLabel.Text = arabic ?? string.Empty;
            WordPreviewContainer.IsVisible = true;
        }
        else
        {
            MessageLabel.Text = $"تلقيت طلب تحدٍ من الصديق ({senderName}) في قسم ({category})!";
            WordPreviewContainer.IsVisible = false;
        }
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