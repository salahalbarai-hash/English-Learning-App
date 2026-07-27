namespace English.Popups;

public partial class ReceiveFriendRequestPopup : Popup
{
    public ReceiveFriendRequestPopup(string senderName)
    {
        InitializeComponent();

        // تنسيق النص ليكون واضحاً ومرحباً
        MessageLabel.Text = $"المستخدم {senderName} يود إضافتك إلى قائمة أصدقائه، هل تقبل؟";
    }

    private void OnAcceptClicked(object sender, EventArgs e)
    {
        // إغلاق النافذة وإرجاع true (مقبول)
        Close(true);
    }

    private void OnDeclineClicked(object sender, EventArgs e)
    {
        // إغلاق النافذة وإرجاع false (مرفوض)
        Close(false);
    }
}