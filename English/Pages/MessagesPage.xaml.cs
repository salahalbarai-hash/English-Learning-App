using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using English.Helpers;
using English.Models;
using English.Services;

namespace English.Pages;

public partial class MessagesPage : ContentPage
{
    private readonly List<MessagesFriendItem> _allFriends = [];
    private readonly ObservableCollection<MessagesFriendItem> _displayedFriends = [];
    private bool _isNavigating = false;
    private bool _isNavigatingBack = false;
    private bool _isFirstAppearance = true;
    private DateTime _pageAppearedTime = DateTime.MinValue;
    private string _currentFilter = string.Empty;
    private AppShell? _shell;

    public MessagesPage()
    {
        InitializeComponent();
        FriendsList.ItemsSource = _displayedFriends;
        _shell = Shell.Current as AppShell;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _pageAppearedTime = DateTime.UtcNow;
        _isNavigating = false;
        _isNavigatingBack = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _pageAppearedTime = DateTime.UtcNow;
        _isNavigating = false;
        _isNavigatingBack = false;

        // إزالة التحديد عند العودة للصفحة
        FriendsList.SelectedItem = null;

        // ربط الأحداث اللحظية
        AttachEvents();

        // 1. تحميل الأصدقاء مع ملخص الرسائل والوقت فوراً من الكاش المحلي (0ms)
        var loadTask = LoadFriendsWithSummariesAsync();

        // 2. تشغيل حركة ظهور الصفحة ككتلة واحدة في أول فتح فقط، لكي تكون العودة لاحقاً من الدردشة خفيفة وفورية كالنسيم
        if (_isFirstAppearance)
        {
            _isFirstAppearance = false;
            await Task.WhenAll(loadTask, this.AnimatePageInAsync(PageAnimationOptions.ContainerOnly));
        }
        else
        {
            await loadTask;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DetachEvents();
    }

    private void AttachEvents()
    {
        DetachEvents();

        ChatStorageService.OnGlobalMessageReceived += OnGlobalMessageReceived;
        ChatStorageService.OnChatSummaryChanged += OnChatSummaryChanged;

        if (_shell?.GameHub != null)
        {
            _shell.GameHub.OnUserConnected += OnFriendConnected;
            _shell.GameHub.OnUserDisconnected += OnFriendDisconnected;
        }
    }

    private void DetachEvents()
    {
        ChatStorageService.OnGlobalMessageReceived -= OnGlobalMessageReceived;
        ChatStorageService.OnChatSummaryChanged -= OnChatSummaryChanged;

        if (_shell?.GameHub != null)
        {
            _shell.GameHub.OnUserConnected -= OnFriendConnected;
            _shell.GameHub.OnUserDisconnected -= OnFriendDisconnected;
        }
    }

    private void OnGlobalMessageReceived(string sender, int messageId, string content)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var item = _allFriends.FirstOrDefault(f => f.Name.Equals(sender, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.LastMessage = content;
                item.IsLastMessageMine = false;
                item.LastMessageTime = DateTime.Now;
                item.UnreadCount++;

                SortAndRefreshList(_currentFilter);
            }
            else
            {
                _ = LoadFriendsWithSummariesAsync();
            }
        });
    }

    private void OnChatSummaryChanged(string friendName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var item = _allFriends.FirstOrDefault(f => f.Name.Equals(friendName, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                var summary = ChatStorageService.GetChatSummary(friendName);
                item.UpdateFromSummary(summary);
                SortAndRefreshList(_currentFilter);
            }
        });
    }

    private void OnFriendConnected(string userName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var item = _allFriends.FirstOrDefault(f => f.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.IsOnline = true;
                SortAndRefreshList(_currentFilter);
            }
        });
    }

    private void OnFriendDisconnected(string userName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var item = _allFriends.FirstOrDefault(f => f.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.IsOnline = false;
                SortAndRefreshList(_currentFilter);
            }
        });
    }

    private async Task LoadFriendsWithSummariesAsync()
    {
        List<string> friendsList = [];
        List<string> onlineUsers = [];

        // 🟢 1. القراءة الفورية المباشرة من الكاش المحلي (0ms)
        var cachedJson = Preferences.Get("Cached_Friends_List", "");
        if (!string.IsNullOrEmpty(cachedJson))
        {
            try
            {
                friendsList = JsonSerializer.Deserialize<List<string>>(cachedJson) ?? [];
            }
            catch { }
        }

        // 🟢 1. بناء القائمة فوراً من الكاش المحلي وملخصات الرسائل في 0ms
        _allFriends.Clear();
        foreach (var name in friendsList)
        {
            var summary = ChatStorageService.GetChatSummary(name);

            var item = new MessagesFriendItem
            {
                Name = name,
                IsOnline = false
            };
            item.UpdateFromSummary(summary);
            _allFriends.Add(item);
        }

        SortAndRefreshList(_currentFilter);

        // 🟢 2. جلب وتحديث حالة المتصلين في الخلفية بهدوء دون حجز واجهة المستخدم
        _ = Task.Run(async () =>
        {
            if (_shell != null)
            {
                try
                {
                    var fetchedOnline = await _shell.GetOnlineUsersAsync();
                    var onlineSet = new HashSet<string>(fetchedOnline ?? [], StringComparer.OrdinalIgnoreCase);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        foreach (var item in _allFriends)
                        {
                            item.IsOnline = onlineSet.Contains(item.Name);
                        }
                        SortAndRefreshList(_currentFilter);
                    });
                }
                catch { }
            }
        });

        await Task.CompletedTask;
    }

    private void SortAndRefreshList(string filter = "")
    {
        _currentFilter = filter;

        // 🟢 ترتيب ذكي كالواتساب والتيليغرام:
        // 1. المحادثات التي تحتوي على رسائل ترتب بحسب وقت آخر رسالة (الأحدث في القمة).
        // 2. المحادثات التي ليس بها رسائل بعد، يظهر الأصدقاء المتصلون أولاً ثم ترتيب أبجدي.
        var sorted = _allFriends
            .OrderByDescending(f => f.HasLastMessage)
            .ThenByDescending(f => f.LastMessageTime ?? DateTime.MinValue)
            .ThenByDescending(f => f.IsOnline)
            .ThenBy(f => f.Name)
            .ToList();

        var filtered = string.IsNullOrWhiteSpace(filter)
            ? sorted
            : sorted.Where(f => f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                (f.LastMessage != null && f.LastMessage.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToList();

        // تحديث ObservableCollection دون إعادة بناء الواجهة بالكامل
        _displayedFriends.Clear();
        foreach (var item in filtered)
        {
            _displayedFriends.Add(item);
        }

        EmptyLabel.IsVisible = _displayedFriends.Count == 0;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = (e.NewTextValue ?? string.Empty).Trim();
        SortAndRefreshList(text);
    }

    private async void OnFriendSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_isNavigating || (DateTime.UtcNow - _pageAppearedTime).TotalMilliseconds < 300) return;

        if (e.CurrentSelection.FirstOrDefault() is MessagesFriendItem item)
        {
            _isNavigating = true;
            try
            {
                // الانتقال للدردشة
                await Shell.Current.GoToAsync($"ChatPage?FriendName={item.Name}");
            }
            finally
            {
                _isNavigating = false;
                if (sender is CollectionView cv)
                    cv.SelectedItem = null;
            }
        }
    }

    private bool _isSettingsNavigating = false;
    private async void OnSettingsClicked(object sender, TappedEventArgs e)
    {
        if (_isSettingsNavigating) return;
        _isSettingsNavigating = true;
        try
        {
            if (SettingsBtn is Border btn)
            {
                await btn.ScaleTo(0.9, 80);
                await btn.ScaleTo(1.0, 80);
            }
            await Shell.Current.GoToAsync(nameof(ChatSettingsPage));
        }
        catch { }
        finally
        {
            _isSettingsNavigating = false;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // إذا كان هناك رجوع سريع قيد التنفيذ من صفحة الدردشة، نجدول الذهاب المباشر إلى MorePage فوراً
        if (ChatPage.IsNavigatingBack)
        {
            ChatPage.QueueSecondBack();
            return true;
        }

        if (_isNavigatingBack) return true;
        _isNavigatingBack = true;

        Dispatcher.Dispatch(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync("//MorePage");
            }
            catch
            {
                try { await Shell.Current.GoToAsync(".."); } catch { }
            }
            finally
            {
                await Task.Delay(300);
                _isNavigatingBack = false;
            }
        });

        return true;
    }
}

public partial class MessagesFriendItem : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    [NotifyPropertyChangedFor(nameof(StatusTextColor))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusIcon))]
    [NotifyPropertyChangedFor(nameof(LastMessagePreview))]
    private bool isOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastMessage))]
    [NotifyPropertyChangedFor(nameof(LastMessagePreview))]
    private string? lastMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastMessagePreview))]
    private bool isLastMessageMine;

    [ObservableProperty]
    private MessageStatus lastMessageStatus = MessageStatus.Pending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastMessageTimeString))]
    private DateTime? lastMessageTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    [NotifyPropertyChangedFor(nameof(IsArrowVisible))]
    [NotifyPropertyChangedFor(nameof(UnreadCountText))]
    [NotifyPropertyChangedFor(nameof(LastMessageColor))]
    [NotifyPropertyChangedFor(nameof(LastMessageFontAttributes))]
    private int unreadCount;

    public string Initials => string.IsNullOrWhiteSpace(Name)
        ? "?"
        : string.Join("", Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0].ToString())).ToUpper();

    public string StatusIcon => IsOnline ? "🟢" : "🔴";
    public string StatusText => IsOnline ? "متصل الآن" : "غير متصل";

    public Color StatusColor => IsOnline ? Color.FromArgb("#10B981") : Color.FromArgb("#64748B");
    public Color StatusTextColor => IsOnline ? Color.FromArgb("#10B981") : Color.FromArgb("#64748B");

    public bool HasLastMessage => !string.IsNullOrEmpty(LastMessage);
    public bool HasUnread => UnreadCount > 0;
    public bool IsArrowVisible => UnreadCount <= 0;
    public string UnreadCountText => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

    public string LastMessagePreview
    {
        get
        {
            if (!HasLastMessage) return StatusText;
            return IsLastMessageMine ? $"أنت: {LastMessage}" : (LastMessage ?? string.Empty);
        }
    }

    public Color LastMessageColor => HasUnread ? Color.FromArgb("#F8FAFC") : Color.FromArgb("#94A3B8");
    public FontAttributes LastMessageFontAttributes => HasUnread ? FontAttributes.Bold : FontAttributes.None;

    public string LastMessageTimeString
    {
        get
        {
            if (!LastMessageTime.HasValue) return string.Empty;

            var time = LastMessageTime.Value.ToLocalTime();
            var culture = new System.Globalization.CultureInfo("ar-SA");
            var today = DateTime.Today;

            if (time.Date == today)
            {
                return time.ToString("hh:mm tt", culture);
            }
            if (time.Date == today.AddDays(-1))
            {
                return "أمس";
            }
            if ((today - time.Date).TotalDays < 7)
            {
                return time.ToString("dddd", culture);
            }
            return time.ToString("dd/MM", culture);
        }
    }

    public void UpdateFromSummary(ChatSummary summary)
    {
        LastMessage = summary.LastMessage;
        IsLastMessageMine = summary.IsMine;
        LastMessageStatus = summary.Status;
        LastMessageTime = summary.LastMessageTime;
        UnreadCount = summary.UnreadCount;
    }
}