using CommunityToolkit.Maui.Views;

namespace English.Popups;

public enum SelectedGameMode
{
    SinglePlayer,
    FriendChallenge
}

public partial class GameModeSelectionPopup : Popup
{
    public GameModeSelectionPopup(string gameTitle)
    {
        InitializeComponent();
        GameTitleLabel.Text = $"اختر وضع اللعب - {gameTitle}";
    }

    private void OnSinglePlayerTapped(object sender, EventArgs e)
    {
        Close(SelectedGameMode.SinglePlayer);
    }

    private void OnFriendChallengeTapped(object sender, EventArgs e)
    {
        Close(SelectedGameMode.FriendChallenge);
    }

    private void OnCloseClicked(object sender, EventArgs e)
    {
        Close(null);
    }
}