namespace English.Pages;

public partial class FriendRequestsPage : ContentPage
{
    private List<string> _requests = new();

    public FriendRequestsPage()
    {
        InitializeComponent();
        Shell.SetNavBarIsVisible(this, false);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRequestsAsync();
    }

    private async Task LoadRequestsAsync()
    {
        if (Shell.Current is AppShell appShell)
        {
            _requests = await appShell.GetPendingFriendRequestsAsync();
            RequestsCollectionView.ItemsSource = _requests;
        }
    }

    private async void OnAcceptRequestClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string senderName)
        {
            if (Shell.Current is AppShell appShell)
            {
                await appShell.AcceptFriendRequestAsync(senderName);

                _requests.Remove(senderName);
                RequestsCollectionView.ItemsSource = null;
                RequestsCollectionView.ItemsSource = _requests;
            }

            await Toast.Make($"أصبح {senderName} من أصدقائك الآن").Show();
        }
    }
}