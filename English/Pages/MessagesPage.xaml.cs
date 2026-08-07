namespace English.Pages;

public class MessagesFriendItem
{
    public string Name { get; set; } = string.Empty;
    public string Initials => string.IsNullOrEmpty(Name)
        ? "?"
        : string.Join("", Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0].ToString())).ToUpper();
    public string StatusIcon { get; set; } = "🔴";
    public string StatusText => StatusIcon == "🟢" ? "متصل" : "غير متصل";

    // 🟢 لون نقطة الاتصال: أخضر زاهي للمتصل وأحمر هادئ للغير متصل
    public Color StatusColor => StatusIcon == "🟢" ? Color.FromArgb("#10B981") : Color.FromArgb("#F43F5E");
}

public partial class MessagesPage : ContentPage
{
    private List<MessagesFriendItem> _allFriends = new();

    public MessagesPage()
    {
        InitializeComponent();

        // إجبار الشاشة على الوضع النهاري دائماً للحفاظ على التصميم الزاهي
        if (Application.Current != null)
        {
            Application.Current.UserAppTheme = AppTheme.Light;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // إزالة التحديد عند العودة للصفحة
        FriendsList.SelectedItem = null;

        await LoadFriendsAsync();
    }

    private async Task LoadFriendsAsync()
    {
        List<string> friendsList = new();
        List<string> onlineUsers = new();
        var userName = Preferences.Get("UserName", "");

        try
        {
            if (Shell.Current is AppShell appShell)
            {
                var fetchedFriends = await appShell.GetAllFriendsAsync();
                var fetchedOnline = await appShell.GetOnlineUsersAsync();

                if (fetchedFriends != null) friendsList = fetchedFriends;
                if (fetchedOnline != null) onlineUsers = fetchedOnline;
            }

            if (friendsList.Count == 0 && !string.IsNullOrEmpty(userName))
            {
                var arr = await Services.Service.GetFriendsAsync(userName);
                if (arr != null) friendsList = arr.ToList();
            }

            if (friendsList.Count > 0)
            {
                Preferences.Set("OfflineFriendsCache", string.Join(",", friendsList));
            }
        }
        catch
        {
            // الوضع غير المتصل
        }

        if (friendsList.Count == 0)
        {
            var cachedFriends = Preferences.Get("OfflineFriendsCache", "");
            if (!string.IsNullOrEmpty(cachedFriends))
            {
                friendsList = cachedFriends.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        _allFriends = friendsList.Select(f =>
        {
            bool isOnline = onlineUsers.Contains(f, StringComparer.OrdinalIgnoreCase);
            return new MessagesFriendItem
            {
                Name = f,
                StatusIcon = isOnline ? "🟢" : "🔴"
            };
        }).ToList();

        _allFriends = _allFriends
            .OrderByDescending(f => f.StatusIcon == "🟢")
            .ThenBy(f => f.Name)
            .ToList();

        RefreshList();
    }

    private void RefreshList(string filter = "")
    {
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allFriends
            : _allFriends.Where(f => f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        FriendsList.ItemsSource = filtered;
        EmptyLabel.IsVisible = filtered.Count == 0;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = e.NewTextValue ?? string.Empty;
        RefreshList(text.Trim());
    }

    private async void OnFriendSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MessagesFriendItem item)
        {
            // الانتقال للدردشة أولاً
            await Shell.Current.GoToAsync($"ChatPage?FriendName={item.Name}");

            // إزالة التحديد بعد الانتقال لتجنب الوميض المزعج
            if (sender is CollectionView cv)
                cv.SelectedItem = null;
        }
    }
}