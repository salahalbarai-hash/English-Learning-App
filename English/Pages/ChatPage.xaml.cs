using English.Helpers;
namespace English.Pages;

[QueryProperty(nameof(FriendName), "FriendName")]
public partial class ChatPage : ContentPage
{
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

    private IDisposable? _receiveSubscription;
    private IDisposable? _deliveredSubscription;
    private IDisposable? _readSubscription;

    // 🟢 متغيرات التحميل المتدرج المحلي (Local Pagination)
    private const int PageSize = 50;
    private bool _isLoadingMore = false;
    private bool _isNavigatingBack = false;
    private CancellationTokenSource? _backCts;
    private List<ChatBubbleModel>? _allOfflineMessages; // كل الرسائل المحلية
    private int _offlineLoadedCount = 0;

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
        _isNavigatingBack = false;

        // 1. استرجاع آخر 50 رسالة من التخزين المحلي فوراً قبل بدء الحركة
        // لضمان ظهور الرسائل داخل القائمة منذ اللحظة الأولى دون قفزة بصرية
        LoadOfflineMessages();

        // 2. بدء حركة ظهور الصفحة فوراً دون تعطيل بقية العمليات (بدون await هنا)
        // ملاحظة: في صفحات الدردشة يُفضل ContainerOnly لتظهر الصفحة ككتلة واحدة متماسكة،
        // أو اتركها فارغة ()this.AnimatePageInAsync لتحريك (الشريط العلوي ثم الرسائل ثم حقل الإدخال) بالتتابع
        var animationTask = this.AnimatePageInAsync(PageAnimationOptions.ContainerOnly);

        // 3. ربط أحداث الاتصال والانفصال اللحظية
        if (_shell?.GameHub != null)
        {
            // إزالة الاشتراك القديم أولاً لمنع تكرار الاستدعاء عند الخروج والعودة للصفحة
            _shell.GameHub.OnUserConnected -= OnFriendConnected;
            _shell.GameHub.OnUserConnected += OnFriendConnected;

            _shell.GameHub.OnUserDisconnected -= OnFriendDisconnected;
            _shell.GameHub.OnUserDisconnected += OnFriendDisconnected;
        }

        // 4. تفعيل مستمعات SignalR للرسائل والحالات
        SetupSignalRListeners();

        // 5. التحقق من اتصال الصديق الحالي أو آخر ظهور عبر الشبكة
        await CheckFriendStatus();

        // 6. انتظار اكتمال الحركة (إن لم تكن قد اكتملت بالفعل أثناء الاتصال بالشبكة)
        await animationTask;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _backCts?.Cancel();
        _backCts?.Dispose();
        _backCts = null;

        _receiveSubscription?.Dispose();
        _deliveredSubscription?.Dispose();
        _readSubscription?.Dispose();

        if (_shell?.GameHub != null)
        {
            _shell.GameHub.OnUserConnected -= OnFriendConnected;
            _shell.GameHub.OnUserDisconnected -= OnFriendDisconnected;
            _shell.GameHub.OnMessageDeleted -= OnMessageDeletedHandler;

            if (_shell.GameHub.HubConnection != null)
            {
                _shell.GameHub.HubConnection.Reconnected -= OnHubReconnected;
                _shell.GameHub.HubConnection.Closed -= OnHubClosed;
            }
        }
    }

    private void UpdateHeader()
    {
        FriendNameTitle.Text = FriendName;
        FriendInitialLabel.Text = string.IsNullOrEmpty(FriendName) ? "?" : FriendName[0].ToString().ToUpper();
    }

    protected override bool OnBackButtonPressed()
    {
        // 1. إذا كان وضع التحديد مفعلاً، إلغاء التحديد أولاً
        if (IsSelectionModeActive)
        {
            CloseSelectionMode();
            return true;
        }

        // إذا كان الانتقال جارياً بالفعل، تجاهل أي نقرات إضافية
        if (_isNavigatingBack) return true;

        // 2. إذا كان مؤقت الضغطة الأولى يعمل وجاءت ضغطة ثانية سريعة -> رجوع مزدوج مباشر إلى شاشة المزيد
        if (_backCts != null)
        {
            _backCts.Cancel();
            _backCts.Dispose();
            _backCts = null;
            _isNavigatingBack = true;

            Dispatcher.Dispatch(async () =>
            {
                try
                {
                    // انتقال مباشر أحادي إلى شاشة المزيد دون المرور بمرحلتين متتاليتين لتفادي خلل أندرويد #31935
                    await Shell.Current.GoToAsync("//MorePage");
                }
                catch
                {
                    try { await Shell.Current.GoToAsync("../.."); } catch { }
                }
                finally
                {
                    await Task.Delay(400);
                    _isNavigatingBack = false;
                }
            });

            return true;
        }

        // 3. الضغطة الأولى: نافذة 220ms للتفريق بين الرجوع لخطوة واحدة والرجوع المزدوج
        _backCts = new CancellationTokenSource();
        var cts = _backCts;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(220, cts.Token);

                // إذا انتهت المهلة دون إلغاء، فهذا رجوع عادي لخطوة واحدة (شاشة الرسائل)
                Dispatcher.Dispatch(async () =>
                {
                    if (_backCts == cts)
                    {
                        _backCts?.Dispose();
                        _backCts = null;
                    }

                    if (_isNavigatingBack) return;
                    _isNavigatingBack = true;

                    try
                    {
                        await Shell.Current.GoToAsync("..");
                    }
                    catch { }
                    finally
                    {
                        await Task.Delay(350);
                        _isNavigatingBack = false;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // تم الإلغاء لأن الضغطة الثانية وصلت وتم تنفيذ الانتقال المباشر
            }
        });

        return true;
    }

    private void OnFriendConnected(string userName)
    {
        if (userName.Equals(FriendName, StringComparison.OrdinalIgnoreCase))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FriendStatusLabel.Text = "متصل الآن";
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
                await UpdateLastSeenUI();
            });
        }
    }

    private async Task CheckFriendStatus()
    {
        try
        {
            // 1. نتحقق مما إذا كان SignalR متصلاً لنجلب حالة الأونلاين اللحظية
            if (_shell?.GameHub?.HubConnection?.State == HubConnectionState.Connected)
            {
                var onlineUsers = await _shell.GetOnlineUsersAsync();
                if (onlineUsers != null && onlineUsers.Contains(FriendName, StringComparer.OrdinalIgnoreCase))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        FriendStatusLabel.Text = "متصل الآن";
                        OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#10B981"); // أخضر
                    });
                    return; // نتوقف هنا لأننا وجدناه متصلاً
                }
            }
            await UpdateLastSeenUI();
        }
        catch
        {
            // 3. في حالة فشل كل شيء (انقطاع الإنترنت الفعلي عن الجهاز)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FriendStatusLabel.Text = "غير متصل (لا يوجد انترنت)";
                OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#EF4444"); // أحمر
            });
        }
    }

    private async Task UpdateLastSeenUI()
    {
        try
        {
            var lastSeen = _shell != null ? await _shell.GetLastSeenAsync(FriendName) : null;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#EF4444");

                if (lastSeen.HasValue)
                {
                    var time = lastSeen.Value.ToLocalTime();
                    var today = DateTime.Today;
                    var culture = new System.Globalization.CultureInfo("ar-SA");
                    string timeStr = time.ToString("hh:mm tt", culture);
                    string datePart;

                    if (time.Date == today)
                        datePart = $"اليوم الساعة {timeStr}";
                    else if (time.Date == today.AddDays(-1))
                        datePart = $"أمس الساعة {timeStr}";
                    else
                        datePart = time.ToString("dd/MM/yyyy hh:mm tt", culture);

                    FriendStatusLabel.Text = $"آخر ظهور: {datePart}";
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
                FriendStatusLabel.Text = "غير متصل";
                OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#EF4444");
            });
        }
    }

    // 🟢 زر "تحميل المزيد" - يجلب رسائل أقدم من الملف المحلي
    private void OnLoadMoreClicked(object sender, EventArgs e)
    {
        if (_isLoadingMore) return;
        _isLoadingMore = true;
        LoadMoreButton.Text = "جارِ التحميل...";

        try
        {
            if (_allOfflineMessages != null && _offlineLoadedCount < _allOfflineMessages.Count)
            {
                // تحميل المزيد من الرسائل المحلية
                LoadMoreOfflineMessages();
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadMoreButton.IsVisible = false;
                });
            }
        }
        finally
        {
            _isLoadingMore = false;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadMoreButton.Text = "⬆️ تحميل رسائل أقدم";
            });
        }
    }

    private async Task OnHubReconnected(string? connectionId)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await CheckFriendStatus();
            await SendPendingMessages();
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

    private void OnMessageDeletedHandler(int msgId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var msg = Messages.FirstOrDefault(m => m.Id == msgId);
            if (msg != null)
            {
                Messages.Remove(msg);
            }
            _allOfflineMessages?.RemoveAll(m => m.Id == msgId);
            SaveMessagesOffline();
        });
    }

    private void SetupSignalRListeners()
    {
        if (_shell?.GameHub?.HubConnection != null)
        {
            _shell.GameHub.HubConnection.Reconnected -= OnHubReconnected;
            _shell.GameHub.HubConnection.Reconnected += OnHubReconnected;

            _shell.GameHub.HubConnection.Closed -= OnHubClosed;
            _shell.GameHub.HubConnection.Closed += OnHubClosed;

            _receiveSubscription?.Dispose();
            _receiveSubscription = _shell.GameHub.HubConnection.On<int, string, string>("ReceiveDirectMessage", async (messageId, sender, message) =>
            {
                if (sender.Equals(FriendName, StringComparison.OrdinalIgnoreCase))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var newMsg = new ChatBubbleModel
                        {
                            Id = messageId,
                            Content = message,
                            Timestamp = DateTime.Now,
                            IsMine = false,
                            Status = MessageStatus.Read
                        };
                        Messages.Add(newMsg);
                        ScrollToBottom();
                        SaveMessagesOffline();
                    });

                    try
                    {
                        if (_shell?.GameHub?.HubConnection?.State == HubConnectionState.Connected)
                        {
                            await _shell.GameHub.HubConnection.InvokeAsync("MarkMessagesAsRead", sender);
                        }
                    }
                    catch { }
                }
            });

            _deliveredSubscription?.Dispose();
            _deliveredSubscription = _shell.GameHub.HubConnection.On<int>("MessageDelivered", (messageId) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var msg = Messages.FirstOrDefault(m => m.Id == messageId)
                              ?? Messages.FirstOrDefault(m => m.IsMine && m.Id == 0 && m.Status < MessageStatus.Delivered);

                    if (msg != null)
                    {
                        var index = Messages.IndexOf(msg);

                        msg.Status = MessageStatus.Delivered;
                        SaveMessagesOffline();
                    }
                });
            });

            _readSubscription?.Dispose();
            _readSubscription = _shell.GameHub.HubConnection.On<string>("MessagesReadBy", (friendName) =>
            {
                if (friendName.Equals(FriendName, StringComparison.OrdinalIgnoreCase))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        bool isChanged = false;
                        for (int i = 0; i < Messages.Count; i++)
                        {
                            var msg = Messages[i];
                            if (msg.IsMine && msg.Status != MessageStatus.Read)
                            {
                                msg.Status = MessageStatus.Read;
                                isChanged = true;
                            }
                        }
                        if (isChanged) SaveMessagesOffline();
                    });
                }
            });

            _shell.GameHub.OnMessageDeleted -= OnMessageDeletedHandler;
            _shell.GameHub.OnMessageDeleted += OnMessageDeletedHandler;
        }
    }

    private void OnMessageLongPressedCommand(ChatBubbleModel msg)
    {
        if (msg == null)
            return;

        // جلب النسخة الحقيقية (نستخدم نفس الكائن إذا كان موجوداً لتفادي تشابه الـ Id=0، أو نبحث بالـ Id في حال كان الكائن قديماً)
        var realMsg = Messages.Contains(msg) ? msg : (Messages.FirstOrDefault(m => m.Id == msg.Id) ?? msg);

        // هذا الضغط كان LongPress، نسجل الوقت لنتجاهل أي Tap يأتي مباشرة بعده
        _lastLongPressTime = DateTime.Now;

        if (!IsSelectionModeActive)
        {
            // تحديد الرسالة أولاً قبل تفعيل وضع التحديد
            // هذا يضمن أن selectedCount > 0 عند استدعاء UpdateSelectionUI
            realMsg.IsSelected = true;

            // الدخول في وضع التحديد
            IsSelectionModeActive = true;
        }
        else
        {
            // في وضع التحديد:
            // الضغط المطول على رسالة يحددها أو يلغي تحديدها
            realMsg.IsSelected = !realMsg.IsSelected;
        }

        UpdateSelectionUI();
    }

    private void OnMessageTappedCommand(ChatBubbleModel msg)
    {
        if (msg == null)
            return;

        // نتجاهل الحدث إذا كان جزءاً من ضغطة مطولة للتو (خلال نصف ثانية)
        if ((DateTime.Now - _lastLongPressTime).TotalMilliseconds < 500)
        {
            return;
        }

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
            // إغلاق وضع التحديد
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

        // إظهار نافذة التأكيد المخصصة
        var popup = new English.Popups.DeleteConfirmPopup();
        var result = await Shell.Current.ShowPopupAsync(popup);

        string action = result as string ?? "";

        // 🟢 إغلاق وضع التحديد فوراً بمجرد تأكيد الحذف لمنع المستخدم من التفاعل مع رسائل أخرى أثناء الحذف
        CloseSelectionMode();

        if (action == "Delete")
        {
            string blacklistJson = Preferences.Get("DeletedForMe_List", "[]");
            var blacklist = JsonSerializer.Deserialize<List<int>>(blacklistJson) ?? new List<int>();

            foreach (var msg in selectedMessages)
            {
                if (msg.Id > 0 && !blacklist.Contains(msg.Id))
                {
                    blacklist.Add(msg.Id);
                }
                Messages.Remove(msg);
                _allOfflineMessages?.RemoveAll(m => m.Id == msg.Id || (m.Content == msg.Content && m.Timestamp == msg.Timestamp && m.IsMine == msg.IsMine));
            }

            Preferences.Set("DeletedForMe_List", JsonSerializer.Serialize(blacklist));
            SaveMessagesOffline();
        }
    }

    private async Task SendPendingMessages()
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
                    await _shell.GameHub.HubConnection.InvokeAsync("SendDirectMessage", FriendName, msg.Content);
                    msg.Status = MessageStatus.Sent;
                    hasChanges = true;
                }
                catch
                {
                }
            }
        }

        if (hasChanges) SaveMessagesOffline();
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        MessageEntry.Text = string.Empty;

        var newMsg = new ChatBubbleModel
        {
            Id = Environment.TickCount & int.MaxValue,
            Content = text,
            Timestamp = DateTime.Now,
            IsMine = true,
            Status = MessageStatus.Pending
        };

        Messages.Add(newMsg);

        ScrollToBottom();
        SaveMessagesOffline();

        try
        {
            if (_shell?.GameHub?.HubConnection?.State == HubConnectionState.Connected)
            {
                await _shell.GameHub.HubConnection.InvokeAsync("SendDirectMessage", FriendName, text);

                var index = Messages.IndexOf(newMsg);
                if (index >= 0)
                {
                    newMsg.Status = MessageStatus.Sent;
                    SaveMessagesOffline();
                }
            }
        }
        catch { }
    }

    private void ScrollToBottom()
    {
        if (Messages.Count > 0)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100);
                var lastMessage = Messages.LastOrDefault();
                if (lastMessage != null)
                {
                    MessagesList.ScrollTo(lastMessage, position: ScrollToPosition.End, animate: false);
                }
            });
        }
    }

    // 🟢 تحميل آخر 50 رسالة فقط من الملف المحلي
    private void LoadOfflineMessages()
    {
        string filePath = Path.Combine(FileSystem.AppDataDirectory, $"chat_{FriendName}.json");
        if (!File.Exists(filePath))
        {
            _allOfflineMessages = new List<ChatBubbleModel>();
            LoadMoreButton.IsVisible = false;
            return;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            if (!string.IsNullOrEmpty(json))
            {
                // استبعاد الرسائل المحذوفة محلياً
                string blacklistJson = Preferences.Get("DeletedForMe_List", "[]");
                var blacklist = JsonSerializer.Deserialize<List<int>>(blacklistJson) ?? new List<int>();

                var allMsgs = JsonSerializer.Deserialize<List<ChatBubbleModel>>(json) ?? new List<ChatBubbleModel>();
                _allOfflineMessages = allMsgs.Where(m => !blacklist.Contains(m.Id)).ToList();

                if (_allOfflineMessages != null && _allOfflineMessages.Count > 0 && Messages.Count == 0)
                {
                    // 🟢 نأخذ فقط آخر PageSize رسالة للعرض السريع
                    int startIndex = Math.Max(0, _allOfflineMessages.Count - PageSize);
                    var recentMessages = _allOfflineMessages.Skip(startIndex).ToList();
                    _offlineLoadedCount = recentMessages.Count;

                    // 🟢 Batch Insert: فصل وربط مرة واحدة
                    MessagesList.ItemsSource = null;
                    Messages.Clear();
                    foreach (var msg in recentMessages)
                    {
                        Messages.Add(msg);
                    }
                    MessagesList.ItemsSource = Messages;

                    // إظهار زر "تحميل المزيد" إذا كانت هناك رسائل أقدم
                    LoadMoreButton.IsVisible = _allOfflineMessages.Count > PageSize;
                    ScrollToBottom();
                }
                else
                {
                    LoadMoreButton.IsVisible = false;
                }
            }
        }
        catch { }
    }

    // 🟢 تحميل دفعة إضافية من الرسائل المحلية القديمة
    private void LoadMoreOfflineMessages()
    {
        if (_allOfflineMessages == null) return;

        int totalMessages = _allOfflineMessages.Count;
        int alreadyShown = _offlineLoadedCount;
        int remaining = totalMessages - alreadyShown;

        if (remaining <= 0)
        {
            MainThread.BeginInvokeOnMainThread(() => LoadMoreButton.IsVisible = false);
            return;
        }

        int toLoad = Math.Min(PageSize, remaining);
        int startIndex = totalMessages - alreadyShown - toLoad;
        if (startIndex < 0) startIndex = 0;

        var olderMessages = _allOfflineMessages.Skip(startIndex).Take(toLoad).ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            for (int i = olderMessages.Count - 1; i >= 0; i--)
            {
                var msg = olderMessages[i];
                Messages.Insert(0, msg);
            }

            _offlineLoadedCount += toLoad;
            LoadMoreButton.IsVisible = _offlineLoadedCount < totalMessages;
        });
    }

    private void SaveMessagesOffline()
    {
        try
        {
            if (_allOfflineMessages == null)
            {
                _allOfflineMessages = new List<ChatBubbleModel>();
            }

            // مزامنة العناصر الحالية مع القائمة الكاملة
            foreach (var m in Messages)
            {
                var existing = _allOfflineMessages.FirstOrDefault(x => (x.Id != 0 && x.Id == m.Id) || (x.Content == m.Content && x.Timestamp == m.Timestamp && x.IsMine == m.IsMine));
                if (existing != null)
                {
                    existing.Status = m.Status;
                    existing.Id = m.Id;
                }
                else
                {
                    _allOfflineMessages.Add(m);
                }
            }

            // استبعاد الرسائل المحذوفة
            string blacklistJson = Preferences.Get("DeletedForMe_List", "[]");
            var blacklist = JsonSerializer.Deserialize<List<int>>(blacklistJson) ?? new List<int>();
            _allOfflineMessages.RemoveAll(x => blacklist.Contains(x.Id));

            string filePath = Path.Combine(FileSystem.AppDataDirectory, $"chat_{FriendName}.json");
            string json = JsonSerializer.Serialize(_allOfflineMessages);
            File.WriteAllText(filePath, json);
        }
        catch { }
    }
}