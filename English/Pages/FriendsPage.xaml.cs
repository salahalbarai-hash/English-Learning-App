using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Views;
using English.Popups;

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

    // مفاتيح حفظ البيانات محلياً
    private const string CachedFriendsKey = "Cached_Friends_List";
    private const string CachedSentRequestsKey = "Cached_SentRequests_List";

    public FriendsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Shell.Current is AppShell appShell && appShell.GameHub != null)
        {
            appShell.GameHub.OnUserConnected += OnUserConnectedHandler;
            appShell.GameHub.OnUserDisconnected += OnUserDisconnectedHandler;
        }

        // الاستماع لتغيرات الشبكة لتحديث البيانات فور عودة الإنترنت
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
        // 1️⃣ أولاً: التحميل الفوري للبيانات المخبأة محلياً (Offline-First)
        LoadDataFromLocalCache();
        BuildUIItems();

        // 2️⃣ ثانياً: إذا كان هناك إنترنت، يتم تحديث البيانات في الخلفية من السيرفر
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            await FetchDataFromNetworkAsync();
        }
    }

    /// <summary>
    /// قراءة البيانات المخزنة محلياً لضمان السرعة وعرض الأصدقاء بدون إنترنت
    /// </summary>
    private void LoadDataFromLocalCache()
    {
        try
        {
            string cachedFriendsJson = Preferences.Get(CachedFriendsKey, string.Empty);
            if (!string.IsNullOrEmpty(cachedFriendsJson))
            {
                _friends = JsonSerializer.Deserialize<List<string>>(cachedFriendsJson) ?? new List<string>();
            }

            string cachedRequestsJson = Preferences.Get(CachedSentRequestsKey, string.Empty);
            if (!string.IsNullOrEmpty(cachedRequestsJson))
            {
                _sentRequests = JsonSerializer.Deserialize<List<string>>(cachedRequestsJson) ?? new List<string>();
            }
        }
        catch
        {
            _friends = new List<string>();
            _sentRequests = new List<string>();
        }
    }

    /// <summary>
    /// جلب البيانات من السيرفر وتحديث الكاش المحلي
    /// </summary>
    private async Task FetchDataFromNetworkAsync()
    {
        if (Shell.Current is AppShell appShell)
        {
            try
            {
                var fetchedOnline = await appShell.GetOnlineUsersAsync();
                var fetchedFriends = await appShell.GetAllFriendsAsync();
                var fetchedRequests = await appShell.GetSentPendingRequestsAsync();

                _onlineUsers = fetchedOnline ?? new List<string>();

                // تحديث الأصدقاء وطلبات الصداقة فقط في حال إرجاع قائمة سليمة من السيرفر
                if (fetchedFriends != null && fetchedFriends.Count > 0)
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
                // في حال حدوث أي خطأ في الاتصال، يتم الاكتفاء بالبيانات المحلية المحملة سابقاً
            }
        }
    }

    /// <summary>
    /// إعادة بناء عناصر القوائم وتحديث الواجهة
    /// </summary>
    private void BuildUIItems()
    {
        var currentUser = Preferences.Get("UserName", "");

        var availableOnlineUsers = _onlineUsers
            .Where(user => !string.Equals(user, currentUser, StringComparison.OrdinalIgnoreCase)
                        && !_friends.Contains(user, StringComparer.OrdinalIgnoreCase)
                        && !_sentRequests.Contains(user, StringComparer.OrdinalIgnoreCase))
            .ToList();

        _onlineUserItems = availableOnlineUsers.Select(user => new OnlineUserItem
        {
            Name = user,
            ButtonText = "إضافة ➕",
            IsButtonEnabled = true,
            ButtonBgColor = Color.FromArgb("#6366F1")
        }).ToList();

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
                    RefreshUI();
                }
            }
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

            _onlineUserItems.RemoveAll(u => u.Name.Equals(targetUser, StringComparison.OrdinalIgnoreCase));

            // إضافة المستخدم لطلبات الصداقة المرسلة وحفظها محلياً
            if (!_sentRequests.Contains(targetUser, StringComparer.OrdinalIgnoreCase))
            {
                _sentRequests.Add(targetUser);
                Preferences.Set(CachedSentRequestsKey, JsonSerializer.Serialize(_sentRequests));
            }

            RefreshUI();

            await Toast.Make($"تم إرسال طلب الصداقة إلى {targetUser}").Show();
        }
    }

    private async void OnChallengeClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string friendName)
        {
            // التحقق أولاً من وجود اتصال عند محاولة إرسال التحدي
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await Toast.Make("لا يوجد اتصال بالإنترنت لإرسال التحدي!").Show();
                return;
            }

            int memorizedCount = Preferences.Get("MemorizedWords", 0);

            if (memorizedCount == 0)
            {
                await Toast.Make("يجب عليك حفظ بعض الكلمات أولاً لتتمكن من إرسال تحدي!").Show();
                return;
            }

            List<string> allowedCategories = new();
            JsonDocument doc;
            string json;

            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("words.json");
                using var reader = new StreamReader(stream);
                json = await reader.ReadToEndAsync();
                doc = JsonDocument.Parse(json);

                var memorizedWords = doc.RootElement.EnumerateArray().Take(memorizedCount);

                allowedCategories = memorizedWords
                    .Where(x => x.TryGetProperty("Category", out var c))
                    .Select(x => x.GetProperty("Category").GetString())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct()
                    .ToList()!;
            }
            catch
            {
                await Toast.Make("تعذر قراءة ملف الكلمات").Show();
                return;
            }

            if (allowedCategories.Count == 0)
            {
                await Toast.Make("لا توجد فئات متاحة ضمن الكلمات التي حفظتها.").Show();
                return;
            }

            var popup = new CategorySelectPopup(allowedCategories, memorizedCount);
            var result = await this.ShowPopupAsync(popup);

            if (result is not CategorySelectPopup.CategorySelectionResult sel || string.IsNullOrWhiteSpace(sel.Category))
                return;

            string category = sel.Category;
            string selectedWordEnglish = sel.English;

            if (string.IsNullOrEmpty(category) || category == "إلغاء")
                return;

            if (string.IsNullOrEmpty(selectedWordEnglish))
            {
                var items = doc.RootElement.EnumerateArray()
                             .Take(memorizedCount)
                             .Where(x => x.TryGetProperty("Category", out var c) && c.GetString() == category)
                             .ToArray();

                if (items.Length == 0)
                {
                    await Toast.Make($"لا توجد كلمات محفوظة في الفئة {category}").Show();
                    return;
                }

                var rnd = new Random();
                var chosen = items[rnd.Next(items.Length)];
                selectedWordEnglish = chosen.GetProperty("EnglishWord").GetString() ?? string.Empty;
            }

            if (Shell.Current is AppShell appShell)
            {
                var waitingPopup = new Popups.WaitingChallengePopup(friendName);

                Action<string, bool, string> onChallengeResponded = (responder, isAccepted, cat) =>
                {
                    if (responder == friendName)
                    {
                        MainThread.BeginInvokeOnMainThread(() => waitingPopup.Close(isAccepted));
                    }
                };

                appShell.GameHub.OnChallengeResponseReceived += onChallengeResponded;

                await appShell.SendChallengeToFriendAsync(friendName, category, selectedWordEnglish);
                await Toast.Make($"تم إرسال التحدي إلى {friendName} في قسم {category}").Show();

                var waitResult = await this.ShowPopupAsync(waitingPopup);

                appShell.GameHub.OnChallengeResponseReceived -= onChallengeResponded;

                if (waitResult is bool isAcceptedResult)
                {
                    if (!isAcceptedResult)
                        await Toast.Make($"{friendName} رفض التحدي أو هو مشغول حالياً.").Show();
                }
                else if (waitResult is string status && status == "Cancel")
                {
                    await appShell.GameHub.CancelChallengeAsync(friendName);
                }
            }
        }
    }
}