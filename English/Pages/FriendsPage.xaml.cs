using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Maui.Alerts;

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
    private List<OnlineUserItem> _onlineUserItems = new();
    private List<FriendItem> _friendItems = new();

    private List<string> _onlineUsers = new();
    private List<string> _friends = new();
    private List<string> _sentRequests = new();

    private string _currentTab = "Online";

    public FriendsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 🟢 الاشتراك في أحداث الاتصال/الانقطاع من GameHub
        if (Shell.Current is AppShell appShell && appShell.GameHub != null)
        {
            appShell.GameHub.OnUserConnected += OnUserConnectedHandler;
            appShell.GameHub.OnUserDisconnected += OnUserDisconnectedHandler;
        }

        await LoadDataAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // 🟢 إلغاء الاشتراك لتجنب استهلاك الذاكرة عند مغادرة الصفحة
        if (Shell.Current is AppShell appShell && appShell.GameHub != null)
        {
            appShell.GameHub.OnUserConnected -= OnUserConnectedHandler;
            appShell.GameHub.OnUserDisconnected -= OnUserDisconnectedHandler;
        }
    }

    private async Task LoadDataAsync()
    {
        if (Shell.Current is AppShell appShell)
        {
            _onlineUsers = await appShell.GetOnlineUsersAsync();
            _friends = await appShell.GetFriendsAsync();
            _sentRequests = await appShell.GetSentPendingRequestsAsync();

            // 1. تجهيز قائمة المتصلين المتاح إضافتهم
            var availableOnlineUsers = _onlineUsers
                .Where(user => !_friends.Contains(user, StringComparer.OrdinalIgnoreCase)
                            && !_sentRequests.Contains(user, StringComparer.OrdinalIgnoreCase))
                .ToList();

            _onlineUserItems = availableOnlineUsers.Select(user => new OnlineUserItem
            {
                Name = user,
                ButtonText = "إضافة ➕",
                IsButtonEnabled = true,
                ButtonBgColor = Color.FromArgb("#6366F1")
            }).ToList();

            // 2. تجهيز قائمة الأصدقاء مع تحديد حالة الاتصال وقيمة IsOnline
            _friendItems = _friends.Select(friend => {
                bool isOnline = _onlineUsers.Contains(friend, StringComparer.OrdinalIgnoreCase);
                return new FriendItem
                {
                    Name = friend,
                    StatusIcon = isOnline ? "🟢" : "🔴",
                    IsOnline = isOnline
                };
            }).ToList();

            RefreshUI();
        }
    }

    // 🟢 معالج حدث عودة أو دخول مستخدم متصل
    private void OnUserConnectedHandler(string userName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_onlineUsers.Contains(userName, StringComparer.OrdinalIgnoreCase))
                _onlineUsers.Add(userName);

            // إذا كان المستخدم صديقاً، نحدث حالته فوراً ليكون متصلاً 🟢
            var friend = _friendItems.FirstOrDefault(f => f.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (friend != null)
            {
                friend.StatusIcon = "🟢";
                friend.IsOnline = true;
            }
            else if (!_friends.Contains(userName, StringComparer.OrdinalIgnoreCase) &&
                     !_sentRequests.Contains(userName, StringComparer.OrdinalIgnoreCase))
            {
                // إذا لم يكن صديقاً، نضيفه لقائمة المتصلين المتاح إضافتهم
                if (!_onlineUserItems.Any(u => u.Name.Equals(userName, StringComparison.OrdinalIgnoreCase)))
                {
                    _onlineUserItems.Add(new OnlineUserItem
                    {
                        Name = userName,
                        ButtonText = "إضافة ➕",
                        IsButtonEnabled = true,
                        ButtonBgColor = Color.FromArgb("#6366F1")
                    });
                    RefreshUI();
                }
            }
        });
    }

    // 🟢 معالج حدث انقطاع أو خروج مستخدم
    private void OnUserDisconnectedHandler(string userName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _onlineUsers.RemoveAll(u => u.Equals(userName, StringComparison.OrdinalIgnoreCase));

            // إذا كان المستخدم صديقاً، نغير حالته إلى غير متصل 🔴
            var friend = _friendItems.FirstOrDefault(f => f.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (friend != null)
            {
                friend.StatusIcon = "🔴";
                friend.IsOnline = false;
            }

            // إزالته من قائمة المتصلين المتاحين للإضافة
            _onlineUserItems.RemoveAll(u => u.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));
            RefreshUI();
        });
    }

    private void RefreshUI()
    {
        OnlineUsersList.ItemsSource = null;
        OnlineUsersList.ItemsSource = _onlineUserItems;

        FriendsList.ItemsSource = null;
        FriendsList.ItemsSource = _friendItems;

        OnSearchTextChanged(SearchBox, new TextChangedEventArgs(string.Empty, SearchBox.Text));
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
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string keyword = e.NewTextValue?.Trim() ?? string.Empty;

        if (_currentTab == "Online")
        {
            OnlineUsersList.ItemsSource = string.IsNullOrEmpty(keyword) ?
                _onlineUserItems : _onlineUserItems.Where(u => u.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else if (_currentTab == "Friends")
        {
            FriendsList.ItemsSource = string.IsNullOrEmpty(keyword) ?
                _friendItems : _friendItems.Where(u => u.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    private async void OnAddFriendClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string targetUser)
        {
            if (Shell.Current is AppShell appShell)
            {
                await appShell.SendFriendRequestAsync(targetUser);
            }

            _onlineUserItems.RemoveAll(u => u.Name.Equals(targetUser, StringComparison.OrdinalIgnoreCase));
            RefreshUI();

            await Toast.Make($"تم إرسال طلب الصداقة إلى {targetUser}").Show();
        }
    }

    private async void OnChallengeClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string friendName)
        {
            string category = await DisplayActionSheet("اختر قسم التحدي:", "إلغاء", null, "قواعد", "مفردات", "استماع");

            if (!string.IsNullOrEmpty(category) && category != "إلغاء")
            {
                if (Shell.Current is AppShell appShell)
                {
                    await appShell.SendChallengeToFriendAsync(friendName, category);
                }

                await Toast.Make($"تم إرسال التحدي إلى {friendName} في قسم {category}").Show();
            }
        }
    }
}