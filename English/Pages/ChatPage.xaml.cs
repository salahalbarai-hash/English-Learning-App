using English.Helpers;
using English.Models;
using English.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace English.Pages;

[QueryProperty(nameof(FriendName), "FriendName")]
public partial class ChatPage : ContentPage
{
    public static string? CurrentActiveFriend { get; private set; }

    private DateTime _lastLongPressTime = DateTime.MinValue;
    private string _friendName = "";
    public string FriendName
    {
        get => _friendName;
        set
        {
            _friendName = value;
            UpdateHeader();
        }
    }

    public ObservableCollection<ChatBubbleModel> Messages { get; set; } = [];
    private AppShell? _shell;
    private string _currentUserName = "";

    public ICommand LongPressCommand { get; private set; }
    public ICommand TapCommand { get; private set; }

    // متغيرات التحميل المتدرج المحلي
    private const int PageSize = 50;
    private bool _isLoadingMore = false;
    private List<ChatBubbleModel>? _allOfflineMessages;
    private int _offlineLoadedCount = 0;

    // 🟢 تحكم فوري بالرجوع بدون أي تأخير زمني (0ms) مع معالجة النقر المزدوج بأمان
    public static bool IsNavigatingBack { get; private set; } = false;
    private static Task? _backNavigationTask = null;
    private static bool _secondTapQueued = false;
    private static readonly object _navLock = new();

    public static void QueueSecondBack()
    {
        lock (_navLock)
        {
            if (IsNavigatingBack && _backNavigationTask != null && !_backNavigationTask.IsCompleted)
            {
                if (!_secondTapQueued)
                {
                    _secondTapQueued = true;
                    _ = _backNavigationTask.ContinueWith(async _ =>
                    {
                        await Task.Delay(40);
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            try { await Shell.Current.GoToAsync("//MorePage"); } catch { }
                            finally
                            {
                                lock (_navLock)
                                {
                                    IsNavigatingBack = false;
                                    _secondTapQueued = false;
                                    _backNavigationTask = null;
                                }
                            }
                        });
                    });
                }
            }
        }
    }

    private bool _isSelectionModeActive = false;
    public bool IsSelectionModeActive
    {
        get => _isSelectionModeActive;
        set
        {
            if (_isSelectionModeActive != value)
            {
                _isSelectionModeActive = value;
                OnPropertyChanged(nameof(IsSelectionModeActive));

                // تحديث حالة كل رسالة
                if (Messages != null)
                {
                    foreach (var msg in Messages)
                    {
                        msg.IsSelectionMode = value;
                    }
                }
            }
        }
    }

    public ChatPage()
    {
        InitializeComponent();

        LongPressCommand = new Command<ChatBubbleModel>(OnMessageLongPressedCommand);
        TapCommand = new Command<ChatBubbleModel>(OnMessageTappedCommand);

        Messages.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (ChatBubbleModel item in e.NewItems)
                    item.IsSelectionMode = IsSelectionModeActive;
            }
        };

        MessagesList.ItemsSource = Messages;
        _shell = Shell.Current as AppShell;
        _currentUserName = Preferences.Get("UserName", "");
        Shell.SetNavBarIsVisible(this, false);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        lock (_navLock)
        {
            IsNavigatingBack = false;
            _secondTapQueued = false;
            _backNavigationTask = null;
        }
        CurrentActiveFriend = FriendName;

        // 1. استرجاع الرسائل من الخدمة المركزية في الخلفية
        await LoadOfflineMessagesAsync();

        // 2. تشغيل حركة ظهور الصفحة ككتلة واحدة مستقرة
        var animationTask = this.AnimatePageInAsync(PageAnimationOptions.ContainerOnly);

        // 3. ربط مستمعات SignalR المركزية
        AttachHubEvents();

        // 4. التحقق من اتصال الصديق الحالي أو آخر ظهور
        await CheckFriendStatusAsync();

        // 5. تمييز الرسائل كمقروءة وإشعار الطرف الآخر
        await MarkCurrentChatAsReadAsync();

        await animationTask;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (CurrentActiveFriend == FriendName)
        {
            CurrentActiveFriend = null;
        }

        DetachHubEvents();
    }

    private void UpdateHeader()
    {
        FriendNameTitle.Text = FriendName;
        FriendInitialLabel.Text = string.IsNullOrEmpty(FriendName) ? "?" : FriendName[0].ToString().ToUpper();
    }

    private void AttachHubEvents()
    {
        if (_shell?.GameHub == null) return;

        DetachHubEvents();

        _shell.GameHub.OnUserConnected += OnFriendConnected;
        _shell.GameHub.OnUserDisconnected += OnFriendDisconnected;
        _shell.GameHub.OnDirectMessageReceived += OnDirectMessageReceived;
        _shell.GameHub.OnMessageDelivered += OnMessageDelivered;
        _shell.GameHub.OnMessagesReadBy += OnMessagesReadBy;
        _shell.GameHub.OnMessageDeleted += OnMessageDeleted;

        if (_shell.GameHub.HubConnection != null)
        {
            _shell.GameHub.HubConnection.Reconnected += OnHubReconnected;
            _shell.GameHub.HubConnection.Closed += OnHubClosed;
        }
    }

    private void DetachHubEvents()
    {
        if (_shell?.GameHub == null) return;

        _shell.GameHub.OnUserConnected -= OnFriendConnected;
        _shell.GameHub.OnUserDisconnected -= OnFriendDisconnected;
        _shell.GameHub.OnDirectMessageReceived -= OnDirectMessageReceived;
        _shell.GameHub.OnMessageDelivered -= OnMessageDelivered;
        _shell.GameHub.OnMessagesReadBy -= OnMessagesReadBy;
        _shell.GameHub.OnMessageDeleted -= OnMessageDeleted;

        if (_shell.GameHub.HubConnection != null)
        {
            _shell.GameHub.HubConnection.Reconnected -= OnHubReconnected;
            _shell.GameHub.HubConnection.Closed -= OnHubClosed;
        }
    }

    private async Task LoadOfflineMessagesAsync()
    {
        try
        {
            _allOfflineMessages = await ChatStorageService.LoadMessagesAsync(FriendName);

            if (_allOfflineMessages.Count > 0 && Messages.Count == 0)
            {
                int startIndex = Math.Max(0, _allOfflineMessages.Count - PageSize);
                var recentMessages = _allOfflineMessages.Skip(startIndex).ToList();
                _offlineLoadedCount = recentMessages.Count;

                MessagesList.ItemsSource = null;
                Messages.Clear();
                foreach (var msg in recentMessages)
                {
                    Messages.Add(msg);
                }
                MessagesList.ItemsSource = Messages;

                LoadMoreButton.IsVisible = _allOfflineMessages.Count > PageSize;
                ScrollToBottom(animate: false);
            }
            else
            {
                LoadMoreButton.IsVisible = false;
            }
        }
        catch { }
    }

    private async Task MarkCurrentChatAsReadAsync()
    {
        bool hasUnread = Messages.Any(m => !m.IsMine && m.Status != MessageStatus.Read) ||
                         (_allOfflineMessages?.Any(m => !m.IsMine && m.Status != MessageStatus.Read) ?? false);

        if (hasUnread)
        {
            foreach (var m in Messages)
            {
                if (!m.IsMine && m.Status != MessageStatus.Read)
                {
                    m.Status = MessageStatus.Read;
                }
            }

            if (_allOfflineMessages != null)
            {
                foreach (var m in _allOfflineMessages)
                {
                    if (!m.IsMine && m.Status != MessageStatus.Read)
                    {
                        m.Status = MessageStatus.Read;
                    }
                }
            }

            _ = ChatStorageService.MarkMessagesAsReadAsync(FriendName);

            if (_shell?.GameHub != null)
            {
                await _shell.GameHub.MarkMessagesAsReadAsync(FriendName);
            }
        }
    }

    private void OnDirectMessageReceived(int messageId, string sender, string message)
    {
        if (!sender.Equals(FriendName, StringComparison.OrdinalIgnoreCase))
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // منع التكرار
            if (messageId > 0 && Messages.Any(m => m.Id == messageId))
                return;

            var newMsg = new ChatBubbleModel
            {
                Id = messageId,
                Content = message,
                Timestamp = DateTime.Now,
                IsMine = false,
                Status = MessageStatus.Read
            };

            Messages.Add(newMsg);
            _allOfflineMessages?.Add(newMsg);
            ScrollToBottom(animate: true);

            // حفظ الرسالة في الخلفية
            _ = SaveMessagesOfflineAsync();

            // إشعار الخادم فوراً بأن الرسالة قُرئت لأن المستخدم يشاهدها الآن
            if (_shell?.GameHub != null)
            {
                await _shell.GameHub.MarkMessagesAsReadAsync(sender);
            }
        });
    }

    private void OnMessageDelivered(int messageId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // 1. البحث عن الرسالة بالـ Id إذا كان مطابقاً
            var msg = Messages.FirstOrDefault(m => m.Id == messageId);

            // 2. إذا لم يطابق (لأن الرسالة أُنشئت محلياً بـ Id=0)، نربطها بأقدم رسالة مرسلة تنتظر تأكيد الاستلام
            if (msg == null)
            {
                msg = Messages.FirstOrDefault(m => m.IsMine && (m.Id <= 0 || m.Status < MessageStatus.Delivered));
            }

            if (msg != null)
            {
                msg.Id = messageId;
                msg.Status = MessageStatus.Delivered; // صحين رمادي
                _ = SaveMessagesOfflineAsync();
            }
        });
    }

    private void OnMessagesReadBy(string friendName)
    {
        if (!friendName.Equals(FriendName, StringComparison.OrdinalIgnoreCase))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            bool isChanged = false;
            for (int i = 0; i < Messages.Count; i++)
            {
                var msg = Messages[i];
                if (msg.IsMine && msg.Status != MessageStatus.Read)
                {
                    msg.Status = MessageStatus.Read; // صحين أزرق
                    isChanged = true;
                }
            }

            if (isChanged)
            {
                _ = SaveMessagesOfflineAsync();
            }
        });
    }

    private void OnMessageDeleted(int msgId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var msg = Messages.FirstOrDefault(m => m.Id == msgId);
            if (msg != null)
            {
                Messages.Remove(msg);
            }
            _allOfflineMessages?.RemoveAll(m => m.Id == msgId);
            _ = SaveMessagesOfflineAsync();
        });
    }

    private void OnFriendConnected(string userName)
    {
        if (userName.Equals(FriendName, StringComparison.OrdinalIgnoreCase))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FriendStatusLabel.Text = "متصل الآن";
                FriendStatusLabel.TextColor = Color.FromArgb("#10B981");
                OnlineStatusIndicator.IsVisible = true;
                OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#10B981");
            });
        }
    }

    private void OnFriendDisconnected(string userName)
    {
        if (userName.Equals(FriendName, StringComparison.OrdinalIgnoreCase))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await UpdateLastSeenUIAsync();
            });
        }
    }

    private async Task CheckFriendStatusAsync()
    {
        try
        {
            if (_shell?.GameHub?.HubConnection?.State == HubConnectionState.Connected)
            {
                var onlineUsers = await _shell.GetOnlineUsersAsync();
                if (onlineUsers != null && onlineUsers.Contains(FriendName, StringComparer.OrdinalIgnoreCase))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        FriendStatusLabel.Text = "متصل الآن";
                        FriendStatusLabel.TextColor = Color.FromArgb("#10B981");
                        OnlineStatusIndicator.IsVisible = true;
                        OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#10B981");
                    });
                    return;
                }
            }
            await UpdateLastSeenUIAsync();
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnlineStatusIndicator.IsVisible = false;
                FriendStatusLabel.TextColor = Color.FromArgb("#64748B");
                FriendStatusLabel.Text = "غير متصل";
            });
        }
    }

    private async Task UpdateLastSeenUIAsync()
    {
        try
        {
            var lastSeen = _shell != null ? await _shell.GetLastSeenAsync(FriendName) : null;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnlineStatusIndicator.IsVisible = false;
                FriendStatusLabel.TextColor = Color.FromArgb("#94A3B8");

                if (lastSeen.HasValue)
                {
                    var time = lastSeen.Value.ToLocalTime();
                    var today = DateTime.Today;
                    var culture = new System.Globalization.CultureInfo("ar-SA");
                    string timeStr = time.ToString("h:mm tt", culture);

                    if (time.Date == today)
                    {
                        FriendStatusLabel.Text = $"آخر ظهور اليوم الساعة {timeStr}";
                    }
                    else if (time.Date == today.AddDays(-1))
                    {
                        FriendStatusLabel.Text = $"آخر ظهور أمس الساعة {timeStr}";
                    }
                    else if ((today - time.Date).TotalDays < 7 && time.Date < today)
                    {
                        string dayName = time.ToString("dddd", culture);
                        FriendStatusLabel.Text = $"آخر ظهور {dayName} الساعة {timeStr}";
                    }
                    else
                    {
                        string datePart = time.ToString("dd/MM/yyyy", culture);
                        FriendStatusLabel.Text = $"آخر ظهور {datePart} في {timeStr}";
                    }
                }
                else
                {
                    FriendStatusLabel.Text = "غير متصل";
                }
            });
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnlineStatusIndicator.IsVisible = false;
                FriendStatusLabel.TextColor = Color.FromArgb("#94A3B8");
                FriendStatusLabel.Text = "غير متصل";
            });
        }
    }

    private void OnLoadMoreClicked(object sender, EventArgs e)
    {
        if (_isLoadingMore) return;
        _isLoadingMore = true;
        LoadMoreButton.Text = "جارِ التحميل...";

        try
        {
            if (_allOfflineMessages != null && _offlineLoadedCount < _allOfflineMessages.Count)
            {
                int totalMessages = _allOfflineMessages.Count;
                int alreadyShown = _offlineLoadedCount;
                int remaining = totalMessages - alreadyShown;

                if (remaining <= 0)
                {
                    LoadMoreButton.IsVisible = false;
                    return;
                }

                int toLoad = Math.Min(PageSize, remaining);
                int startIndex = totalMessages - alreadyShown - toLoad;
                if (startIndex < 0) startIndex = 0;

                var olderMessages = _allOfflineMessages.Skip(startIndex).Take(toLoad).ToList();

                for (int i = olderMessages.Count - 1; i >= 0; i--)
                {
                    var msg = olderMessages[i];
                    Messages.Insert(0, msg);
                }

                _offlineLoadedCount += toLoad;
                LoadMoreButton.IsVisible = _offlineLoadedCount < totalMessages;
            }
            else
            {
                LoadMoreButton.IsVisible = false;
            }
        }
        finally
        {
            _isLoadingMore = false;
            LoadMoreButton.Text = "⬆️ تحميل رسائل أقدم";
        }
    }

    private async Task OnHubReconnected(string? connectionId)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await CheckFriendStatusAsync();
            await SendPendingMessagesAsync();
        });
    }

    private Task OnHubClosed(Exception? error)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            FriendStatusLabel.Text = "";
        });
        return Task.CompletedTask;
    }

    private void OnMessageLongPressedCommand(ChatBubbleModel msg)
    {
        if (msg == null) return;

        var realMsg = Messages.Contains(msg) ? msg : (Messages.FirstOrDefault(m => m.Id == msg.Id) ?? msg);
        _lastLongPressTime = DateTime.Now;

        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }

        if (!IsSelectionModeActive)
        {
            realMsg.IsSelected = true;
            IsSelectionModeActive = true;
        }
        else
        {
            realMsg.IsSelected = !realMsg.IsSelected;
        }

        UpdateSelectionUI();
    }

    private void OnMessageTappedCommand(ChatBubbleModel msg)
    {
        if (msg == null) return;

        if ((DateTime.Now - _lastLongPressTime).TotalMilliseconds < 500)
            return;

        if (IsSelectionModeActive)
        {
            var realMsg = Messages.Contains(msg) ? msg : (Messages.FirstOrDefault(m => m.Id == msg.Id) ?? msg);
            realMsg.IsSelected = !realMsg.IsSelected;
            UpdateSelectionUI();
        }
    }

    private void UpdateSelectionUI()
    {
        var selectedCount = Messages.Count(m => m.IsSelected);

        if (selectedCount == 0)
        {
            CloseSelectionMode();
        }
        else
        {
            NormalHeader.IsVisible = false;
            SelectionHeader.IsVisible = true;
            SelectionCountLabel.Text = $"{selectedCount} محدد";
        }
    }

    private void CloseSelectionMode()
    {
        IsSelectionModeActive = false;
        NormalHeader.IsVisible = true;
        SelectionHeader.IsVisible = false;

        foreach (var m in Messages)
        {
            m.IsSelected = false;
        }
    }

    private void OnCancelSelectionClicked(object sender, EventArgs e)
    {
        CloseSelectionMode();
    }

    private async void OnBulkDeleteClicked(object sender, EventArgs e)
    {
        var selectedMessages = Messages.Where(m => m.IsSelected).ToList();
        if (selectedMessages.Count == 0) return;

        var popup = new English.Popups.DeleteConfirmPopup();
        var result = await Shell.Current.ShowPopupAsync(popup);
        string action = result as string ?? "";

        CloseSelectionMode();

        if (action == "Delete")
        {
            var idsToDelete = new List<int>();

            foreach (var msg in selectedMessages)
            {
                if (msg.Id > 0)
                {
                    idsToDelete.Add(msg.Id);

                    // إبلاغ الطرف الآخر إذا كانت الرسالة من إنشائي
                    if (msg.IsMine && _shell?.GameHub != null)
                    {
                        _ = _shell.GameHub.DeleteMessageAsync(msg.Id, FriendName);
                    }
                }

                Messages.Remove(msg);
                _allOfflineMessages?.RemoveAll(m => m.Id == msg.Id || 
                    (m.Content == msg.Content && m.Timestamp == msg.Timestamp && m.IsMine == msg.IsMine));
            }

            if (idsToDelete.Count > 0)
            {
                ChatStorageService.AddToBlacklist(idsToDelete);
            }

            _ = SaveMessagesOfflineAsync();
        }
    }

    private async Task SendPendingMessagesAsync()
    {
        if (_shell?.GameHub?.HubConnection?.State != HubConnectionState.Connected) return;

        bool hasChanges = false;

        for (int i = 0; i < Messages.Count; i++)
        {
            var msg = Messages[i];
            if (msg.IsMine && msg.Status == MessageStatus.Pending)
            {
                try
                {
                    bool sent = await _shell.GameHub.SendDirectMessageAsync(FriendName, msg.Content);
                    if (sent)
                    {
                        msg.Status = MessageStatus.Sent;
                        hasChanges = true;
                    }
                }
                catch { }
            }
        }

        if (hasChanges)
        {
            _ = SaveMessagesOfflineAsync();
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        MessageEntry.Text = string.Empty;

        // تأثير نقر لمسي سلس للزر
        if (sender is VisualElement btn)
        {
            _ = btn.ScaleTo(0.85, 70).ContinueWith(_ => btn.ScaleTo(1.0, 70));
        }
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

        var newMsg = new ChatBubbleModel
        {
            Id = 0, // معرف مؤقت حتى يعود MessageDelivered بالمعرف الفعلي من السيرفر
            Content = text,
            Timestamp = DateTime.Now,
            IsMine = true,
            Status = MessageStatus.Pending
        };

        Messages.Add(newMsg);
        _allOfflineMessages?.Add(newMsg);
        ScrollToBottom(animate: true);
        _ = SaveMessagesOfflineAsync();

        try
        {
            if (_shell?.GameHub?.HubConnection?.State == HubConnectionState.Connected)
            {
                bool sent = await _shell.GameHub.SendDirectMessageAsync(FriendName, text);
                if (sent)
                {
                    if (newMsg.Status == MessageStatus.Pending)
                    {
                        newMsg.Status = MessageStatus.Sent;
                        _ = SaveMessagesOfflineAsync();
                    }
                }
            }
        }
        catch { }
    }

    private void ScrollToBottom(bool animate = false)
    {
        if (Messages.Count == 0) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(50);
            var last = Messages.LastOrDefault();
            if (last != null)
            {
                MessagesList.ScrollTo(last, position: ScrollToPosition.End, animate: animate);
            }
        });
    }

    private async Task SaveMessagesOfflineAsync()
    {
        var listToSave = _allOfflineMessages ?? Messages.ToList();
        await ChatStorageService.SaveMessagesAsync(FriendName, listToSave);
    }

    protected override bool OnBackButtonPressed()
    {
        // 1. إذا كان وضع التحديد مفعلاً، إلغاء التحديد أولاً
        if (IsSelectionModeActive)
        {
            CloseSelectionMode();
            return true;
        }

        lock (_navLock)
        {
            // 2. إذا كان الرجوع الأول قيد التنفيذ وجاءت نقرة ثانية سريعة:
            // لا نلغي الانتقال ولا نسمح بتعارض Shell، بل نجدول الانتقال المباشر لـ MorePage فور انتهاء الرجوع
            if (IsNavigatingBack && _backNavigationTask != null && !_backNavigationTask.IsCompleted)
            {
                QueueSecondBack();
                return true;
            }

            if (IsNavigatingBack) return true;
            IsNavigatingBack = true;
            _secondTapQueued = false;

            // 3. النقرة الأولى: تبدأ فوراً في نفس اللحظة (0ms) بدون أي تأخير أو انتظار!
            _backNavigationTask = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await Shell.Current.GoToAsync("..");
                }
                catch
                {
                    try { await Shell.Current.GoToAsync("//MorePage"); } catch { }
                }
                finally
                {
                    await Task.Delay(250);
                    lock (_navLock)
                    {
                        if (!_secondTapQueued)
                        {
                            IsNavigatingBack = false;
                            _backNavigationTask = null;
                        }
                    }
                }
            });

            return true;
        }
    }
}