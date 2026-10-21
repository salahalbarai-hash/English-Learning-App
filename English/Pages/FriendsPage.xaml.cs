using CommunityToolkit.Maui.Alerts;
using English.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace English.Pages;

public class OnlineUserItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _buttonText = "إضافة ➕";
    private bool _isButtonEnabled = true;
    private Color _buttonBgColor = Color.FromArgb("#6366F1");

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string ButtonText
    {
        get => _buttonText;
        set { _buttonText = value; OnPropertyChanged(); }
    }

    public bool IsButtonEnabled
    {
        get => _isButtonEnabled;
        set { _isButtonEnabled = value; OnPropertyChanged(); }
    }

    public Color ButtonBgColor
    {
        get => _buttonBgColor;
        set { _buttonBgColor = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class FriendItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _statusIcon = "🔴";
    private bool _isOnline = false;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string StatusIcon
    {
        get => _statusIcon;
        set { _statusIcon = value; OnPropertyChanged(); }
    }

    public bool IsOnline
    {
        get => _isOnline;
        set { _isOnline = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public partial class FriendsPage : ContentPage
{
    // القوائم المعروضة المربوطة بالواجهة
    private readonly ObservableCollection<OnlineUserItem> _displayedOnlineUsers = [];
    private readonly ObservableCollection<FriendItem> _displayedFriends = [];

    // القوائم الأصلية الكاملة
    private readonly List<OnlineUserItem> _onlineUserItems = [];
    private readonly List<FriendItem> _friendItems = [];

    private List<string> _onlineUsers = [];
    private List<string> _friends = [];
    private List<string> _sentRequests = [];

    private string _currentTab = "Online";

    // مفاتيح حفظ البيانات محلياً
    private const string CachedFriendsKey = "Cached_Friends_List";
    private const string CachedSentRequestsKey = "Cached_SentRequests_List";

    public FriendsPage()
    {
        InitializeComponent();

        // ربط القوائم لمرة واحدة لمنع وميض وتفكك الربط عند البحث والتحديث
        OnlineUsersList.ItemsSource = _displayedOnlineUsers;
        FriendsList.ItemsSource = _displayedFriends;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ = this.AnimatePageInAsync();

        if (Shell.Current is AppShell appShell && appShell.GameHub != null)
        {
            appShell.GameHub.OnUserConnected += OnUserConnectedHandler;
            appShell.GameHub.OnUserDisconnected += OnUserDisconnectedHandler;
        }

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        await LoadDataAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (Shell.Current is AppShell appShell && appShell.GameHub != null)
        {
            appShell.GameHub.OnUserConnected -= OnUserConnectedHandler;
            appShell.GameHub.OnUserDisconnected -= OnUserDisconnectedHandler;
        }

        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await FetchDataFromNetworkAsync();
            });
        }
    }

    private async Task LoadDataAsync()
    {
        // 1. التحميل الفوري من الذاكرة المحلية (Offline-First)
        LoadDataFromLocalCache();
        BuildUIItems();

        // 2. التحديث بالخلفية عبر الإنترنت
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            await FetchDataFromNetworkAsync();
        }
    }

    private void LoadDataFromLocalCache()
    {
        try
        {
            string cachedFriendsJson = Preferences.Get(CachedFriendsKey, string.Empty);
            if (!string.IsNullOrEmpty(cachedFriendsJson))
            {
                _friends = JsonSerializer.Deserialize<List<string>>(cachedFriendsJson) ?? [];
            }

            string cachedRequestsJson = Preferences.Get(CachedSentRequestsKey, string.Empty);
            if (!string.IsNullOrEmpty(cachedRequestsJson))
            {
                _sentRequests = JsonSerializer.Deserialize<List<string>>(cachedRequestsJson) ?? [];
            }
        }
        catch
        {
            _friends = [];
            _sentRequests = [];
        }
    }

    private async Task FetchDataFromNetworkAsync()
    {
        if (Shell.Current is AppShell appShell)
        {
            try
            {
                var fetchedOnline = await appShell.GetOnlineUsersAsync();
                var fetchedFriends = await appShell.GetFriendsAsync();
                var fetchedRequests = await appShell.GetSentPendingRequestsAsync();
                var incomingRequests = await appShell.GetPendingFriendRequestsAsync();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (incomingRequests != null && incomingRequests.Count > 0)
                    {
                        BtnPendingRequests.IsVisible = true;
                        LblPendingCount.Text = $"طلبات الصداقة الواردة ({incomingRequests.Count}) 📩";
                    }
                    else
                    {
                        BtnPendingRequests.IsVisible = false;
                    }
                });

                _onlineUsers = fetchedOnline ?? [];

                // تحديث الأصدقاء حتى وإن كانت القائمة فارغة لحل ثغرة تجميد الكاش
                if (fetchedFriends != null)
                {
                    _friends = fetchedFriends;
                    Preferences.Set(CachedFriendsKey, JsonSerializer.Serialize(_friends));
                }

                if (fetchedRequests != null)
                {
                    _sentRequests = fetchedRequests;
                    Preferences.Set(CachedSentRequestsKey, JsonSerializer.Serialize(_sentRequests));
                }

                BuildUIItems();
            }
            catch
            {
                // في حال انقطاع الشبكة يتم الاعتماد على البيانات السابقة
            }
        }
    }

    private void BuildUIItems()
    {
        var currentUser = Preferences.Get("UserName", "");

        var availableOnlineUsers = _onlineUsers
            .Where(user => !string.Equals(user, currentUser, StringComparison.OrdinalIgnoreCase)
                        && !_friends.Contains(user, StringComparer.OrdinalIgnoreCase)
                        && !_sentRequests.Contains(user, StringComparer.OrdinalIgnoreCase))
            .ToList();

        _onlineUserItems.Clear();
        foreach (var user in availableOnlineUsers)
        {
            _onlineUserItems.Add(new OnlineUserItem
            {
                Name = user,
                ButtonText = "إضافة ➕",
                IsButtonEnabled = true,
                ButtonBgColor = Color.FromArgb("#6366F1")
            });
        }

        _friendItems.Clear();
        foreach (var friend in _friends)
        {
            bool isOnline = _onlineUsers.Contains(friend, StringComparer.OrdinalIgnoreCase);
            _friendItems.Add(new FriendItem
            {
                Name = friend,
                StatusIcon = isOnline ? "🟢" : "🔴",
                IsOnline = isOnline
            });
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string keyword = SearchBox?.Text?.Trim() ?? string.Empty;

        // تصفية المتصلين
        var filteredOnline = string.IsNullOrEmpty(keyword)
            ? _onlineUserItems
            : _onlineUserItems.Where(u => u.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        SyncCollection(_displayedOnlineUsers, filteredOnline);

        // تصفية الأصدقاء
        var filteredFriends = string.IsNullOrEmpty(keyword)
            ? _friendItems
            : _friendItems.Where(f => f.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        SyncCollection(_displayedFriends, filteredFriends);
    }

    private static void SyncCollection<T>(ObservableCollection<T> target, IList<T> source)
    {
        if (target.SequenceEqual(source))
            return;

        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private void OnUserConnectedHandler(string userName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_onlineUsers.Contains(userName, StringComparer.OrdinalIgnoreCase))
                _onlineUsers.Add(userName);

            var friend = _friendItems.FirstOrDefault(f => f.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (friend != null)
            {
                friend.StatusIcon = "🟢";
                friend.IsOnline = true;
            }
            else if (!_friends.Contains(userName, StringComparer.OrdinalIgnoreCase) &&
                     !_sentRequests.Contains(userName, StringComparer.OrdinalIgnoreCase))
            {
                if (!_onlineUserItems.Any(u => u.Name.Equals(userName, StringComparison.OrdinalIgnoreCase)))
                {
                    _onlineUserItems.Add(new OnlineUserItem
                    {
                        Name = userName,
                        ButtonText = "إضافة ➕",
                        IsButtonEnabled = true,
                        ButtonBgColor = Color.FromArgb("#6366F1")
                    });
                }
            }

            ApplyFilter();
        });
    }

    private void OnUserDisconnectedHandler(string userName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _onlineUsers.RemoveAll(u => u.Equals(userName, StringComparison.OrdinalIgnoreCase));

            var friend = _friendItems.FirstOrDefault(f => f.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (friend != null)
            {
                friend.StatusIcon = "🔴";
                friend.IsOnline = false;
            }

            _onlineUserItems.RemoveAll(u => u.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));

            ApplyFilter();
        });
    }

    private async void OnPageRefreshing(object sender, EventArgs e)
    {
        try
        {
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                await FetchDataFromNetworkAsync();
            }
            else
            {
                await Toast.Make("لا يوجد اتصال بالإنترنت 📶").Show();
            }
        }
        finally
        {
            PageRefreshView.IsRefreshing = false;
        }
    }

    private async void OnOpenRequestsPageClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(FriendRequestsPage));
    }

    private void OnTabClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string tabName)
        {
            _currentTab = tabName;

            TabOnline.BackgroundColor = Color.FromArgb("#1E293B");
            TabFriends.BackgroundColor = Color.FromArgb("#1E293B");

            OnlineUsersList.IsVisible = false;
            FriendsList.IsVisible = false;

            button.BackgroundColor = Color.FromArgb("#6366F1");

            if (tabName == "Online") OnlineUsersList.IsVisible = true;
            else if (tabName == "Friends") FriendsList.IsVisible = true;

            SearchBox.Text = string.Empty;
            ApplyFilter();
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private async void OnAddFriendClicked(object sender, EventArgs e)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await Toast.Make("لا يوجد اتصال بالإنترنت لإرسال طلب الصداقة ⚠️").Show();
            return;
        }

        if (sender is Button button && button.CommandParameter is string targetUser)
        {
            var currentUser = Preferences.Get("UserName", "");
            if (string.Equals(currentUser, targetUser, StringComparison.OrdinalIgnoreCase))
            {
                await Toast.Make("لا يمكنك إرسال طلب صداقة لنفسك").Show();
                return;
            }

            if (Shell.Current is AppShell appShell)
            {
                await appShell.SendFriendRequestAsync(targetUser);
            }

            // إضافة المستخدم لطلبات الصداقة المرسلة وحفظها محلياً
            if (!_sentRequests.Contains(targetUser, StringComparer.OrdinalIgnoreCase))
            {
                _sentRequests.Add(targetUser);
                Preferences.Set(CachedSentRequestsKey, JsonSerializer.Serialize(_sentRequests));
            }

            _onlineUserItems.RemoveAll(u => u.Name.Equals(targetUser, StringComparison.OrdinalIgnoreCase));

            ApplyFilter();

            await Toast.Make($"تم إرسال طلب الصداقة إلى {targetUser} 📩").Show();
        }
    }
}