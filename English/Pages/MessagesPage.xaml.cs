namespace English.Pages;

public class MessagesFriendItem
{
    public string Name { get; set; } = string.Empty;
    public string Initials => string.IsNullOrEmpty(Name)
        ? "?"
        : string.Join("", Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0].ToString())).ToUpper();
    public string StatusIcon { get; set; } = "🔴";
    public string StatusText => StatusIcon == "🟢" ? "متصل" : "غير متصل";

    // 🟢 لون نقطة الاتصال: أخضر للمتصل وأحمر للغير متصل
    public Color StatusColor => StatusIcon == "🟢" ? Color.FromArgb("#22C55E") : Color.FromArgb("#EF4444");
}

public partial class MessagesPage : ContentPage
{
    private List<MessagesFriendItem> _allFriends = new();

    public MessagesPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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
                // 1. محاولة جلب البيانات من السيرفر (SignalR)
                var fetchedFriends = await appShell.GetAllFriendsAsync();
                var fetchedOnline = await appShell.GetOnlineUsersAsync();

                if (fetchedFriends != null) friendsList = fetchedFriends;
                if (fetchedOnline != null) onlineUsers = fetchedOnline;
            }

            // 2. إذا كانت القائمة فارغة (قد يكون الاتصال ضعيفاً)، جرب جلبها من API
            if (friendsList.Count == 0 && !string.IsNullOrEmpty(userName))
            {
                var arr = await Services.Service.GetFriendsAsync(userName);
                if (arr != null) friendsList = arr.ToList();
            }

            // 3. إذا نجحنا في جلب البيانات من الإنترنت، نقوم بتحديث الذاكرة المحلية (الكاش)
            if (friendsList.Count > 0)
            {
                Preferences.Set("OfflineFriendsCache", string.Join(",", friendsList));
            }
        }
        catch
        {
            // نتجاهل أي خطأ في الاتصال بالإنترنت هنا لننتقل للخطوة التالية (الوضع غير المتصل)
        }

        // 🟢 4. ميزة الواتساب: إذا لم نتمكن من جلب البيانات (المستخدم Offline)، نجلبها من الذاكرة المحلية
        if (friendsList.Count == 0)
        {
            var cachedFriends = Preferences.Get("OfflineFriendsCache", "");
            if (!string.IsNullOrEmpty(cachedFriends))
            {
                friendsList = cachedFriends.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        // 5. تحويل البيانات إلى النموذج وتحديد من المتصل
        _allFriends = friendsList.Select(f =>
        {
            bool isOnline = onlineUsers.Contains(f, StringComparer.OrdinalIgnoreCase);
            return new MessagesFriendItem
            {
                Name = f,
                StatusIcon = isOnline ? "🟢" : "🔴"
            };
        }).ToList();

        // 6. ترتيب القائمة (المتصلين أولاً، ثم أبجدياً)
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
            // إزالة التحديد فوراً 
            if (sender is CollectionView cv)
                cv.SelectedItem = null;

            // الانتقال للدردشة
            await Shell.Current.GoToAsync($"ChatPage?FriendName={item.Name}");
        }
    }
}