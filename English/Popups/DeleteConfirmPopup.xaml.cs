using CommunityToolkit.Maui.Views;

namespace English.Popups;

public partial class DeleteConfirmPopup : Popup
{
    public DeleteConfirmPopup(bool canDeleteForEveryone)
    {
        InitializeComponent();
        
        // إخفاء زر "حذف لدى الجميع" إذا لم يكن مسموحاً (مثلاً رسالة ليست لك)
        DeleteForEveryoneBtn.IsVisible = canDeleteForEveryone;
    }

    private void OnDeleteForEveryoneClicked(object sender, EventArgs e)
    {
        Close("DeleteForEveryone");
    }

    private void OnDeleteForMeClicked(object sender, EventArgs e)
    {
        Close("DeleteForMe");
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close("Cancel");
    }
}
