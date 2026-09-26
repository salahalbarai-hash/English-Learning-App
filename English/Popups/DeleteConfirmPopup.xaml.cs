using CommunityToolkit.Maui.Views;

namespace English.Popups;

public partial class DeleteConfirmPopup : Popup
{
    public DeleteConfirmPopup()
    {
        InitializeComponent();
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        Close("Delete");
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close("Cancel");
    }
}
