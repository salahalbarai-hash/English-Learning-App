namespace English.Pages;

[QueryProperty(nameof(FriendName), "FriendName")]
public partial class ChatPage : ContentPage
{
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

    public ObservableCollection<ChatBubbleModel> Messages { get; set; } = new();
    private AppShell? _shell;
    private string _currentUserName = "";

    public ChatPage()
    {
        InitializeComponent();
        MessagesList.ItemsSource = Messages;
        _shell = Shell.Current as AppShell;
        _currentUserName = Preferences.Get("UserName", "");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. استرجاع الرسائل من التخزين المحلي فوراً (Offline)
        LoadOfflineMessages();

        // 2. ربط أحداث الاتصال والانفصال اللحظية
        if (_shell?.GameHub != null)
        {
            _shell.GameHub.OnUserConnected += OnFriendConnected;
            _shell.GameHub.OnUserDisconnected += OnFriendDisconnected;
        }

        // 3. التحقق من اتصال الصديق الحالي أو آخر ظهور
        await CheckFriendStatus();

        // 4. جلب السجل الحقيقي من السيرفر ومزامنة حالات القراءة والاستلام
        await LoadServerChatHistory();

        // 5. تفعيل مستمعات SignalR للرسائل والحالات
        SetupSignalRListeners();
    }

    // إزالة الأحداث عند الخروج من المحادثة لتجنب تكرار الاستماع واستهلاك الذاكرة
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_shell?.GameHub != null)
        {
            _shell.GameHub.OnUserConnected -= OnFriendConnected;
            _shell.GameHub.OnUserDisconnected -= OnFriendDisconnected;
        }
    }

    private void UpdateHeader()
    {
        FriendNameTitle.Text = FriendName;
        FriendInitialLabel.Text = string.IsNullOrEmpty(FriendName) ? "?" : FriendName[0].ToString().ToUpper();
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        Shell.Current.GoToAsync("..");
    }

    // 🟢 عندما يدخل الصديق تتحدث الحالة فوراً أمامي
    private void OnFriendConnected(string userName)
    {
        if (userName.Equals(FriendName, StringComparison.OrdinalIgnoreCase))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FriendStatusLabel.Text = "متصل الآن";
                OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#22C55E");
            });
        }
    }

    // 🟢 عندما يخرج الصديق يتم جلب وقت خروجه فوراً
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
        // إذا كنت غير متصل بالإنترنت، نجعل النص فارغاً
        if (_shell?.GameHub?.HubConnection == null || _shell.GameHub.HubConnection.State != HubConnectionState.Connected)
        {
            FriendStatusLabel.Text = "";
            MainThread.BeginInvokeOnMainThread(() => OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#EF4444"));
            return;
        }

        try
        {
            var onlineUsers = await _shell?.GetOnlineUsersAsync()!;
            if (onlineUsers.Contains(FriendName, StringComparer.OrdinalIgnoreCase))
            {
                FriendStatusLabel.Text = "متصل الآن";
                MainThread.BeginInvokeOnMainThread(() => OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#22C55E"));
            }
            else
            {
                await UpdateLastSeenUI();
            }
        }
        catch
        {
            FriendStatusLabel.Text = "";
            MainThread.BeginInvokeOnMainThread(() => OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#EF4444"));
        }
    }

    // 🟢 دالة جلب التاريخ وتنسيقه ليظهر بشكل احترافي (يوم/شهر/سنة + 12 ساعة ص/م)
    private async Task UpdateLastSeenUI()
    {
        try
        {
            var lastSeen = await _shell?.GetLastSeenAsync(FriendName);
            MainThread.BeginInvokeOnMainThread(() => OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#EF4444"));

            if (lastSeen.HasValue)
            {
                var time = lastSeen.Value;
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
        }
        catch
        {
            FriendStatusLabel.Text = "غير متصل";
            MainThread.BeginInvokeOnMainThread(() => OnlineStatusIndicator.BackgroundColor = Color.FromArgb("#EF4444"));
        }
    }

    // 🟢 جلب السجل من السيرفر وتحديث الحالات (قرأها / استلمها)
    private async Task LoadServerChatHistory()
    {
        if (_shell != null && !string.IsNullOrEmpty(FriendName))
        {
            var serverMessages = await _shell.GetChatHistoryAsync(FriendName);
            if (serverMessages != null && serverMessages.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Messages.Clear();
                    foreach (var sm in serverMessages)
                    {
                        bool isMine = sm.Sender.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase);

                        // تحديد حالة الرسالة بناءً على قاعدة البيانات
                        MessageStatus status = MessageStatus.Sent;
                        if (sm.IsRead) status = MessageStatus.Read;
                        else if (sm.IsDelivered) status = MessageStatus.Delivered;

                        Messages.Add(new ChatBubbleModel
                        {
                            Id = sm.Id,
                            Content = sm.Content,
                            Timestamp = sm.Timestamp,
                            IsMine = isMine,
                            Status = status
                        });
                    }
                    ScrollToBottom();
                    SaveMessagesOffline();
                });
            }
        }
    }

    private void SetupSignalRListeners()
    {
        if (_shell?.GameHub?.HubConnection != null)
        {
            // 🟢 عند عودة الإنترنت
            _shell.GameHub.HubConnection.Reconnected += async (connectionId) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await CheckFriendStatus();
                    await SendPendingMessages(); // إرسال الرسائل المعلقة تلقائياً
                });
            };

            // 🟢 عند انقطاع الإنترنت
            _shell.GameHub.HubConnection.Closed += async (error) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FriendStatusLabel.Text = ""; // مسح الحالة
                });
            };

            // 🟢 استقبال رسالة جديدة
            _shell.GameHub.HubConnection.On<int, string, string>("ReceiveDirectMessage", async (messageId, sender, message) =>
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

            _shell.GameHub.HubConnection.On<int>("MessageDelivered", (messageId) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var msg = Messages.FirstOrDefault(m => m.Id == messageId)
                              ?? Messages.FirstOrDefault(m => m.IsMine && m.Id == 0 && m.Status < MessageStatus.Delivered);

                    if (msg != null)
                    {
                        var index = Messages.IndexOf(msg);

                        // 🟢 إنشاء نسخة جديدة كلياً لتجبار الواجهة على التحديث اللحظي
                        Messages[index] = new ChatBubbleModel
                        {
                            Id = messageId,
                            Content = msg.Content,
                            Timestamp = msg.Timestamp,
                            IsMine = msg.IsMine,
                            Status = MessageStatus.Delivered // صحين رمادي
                        };

                        SaveMessagesOffline();
                    }
                });
            });

            // 🟢 تحديث الرسائل إلى مقروءة (✓✓ أزرق) بدوووون تخريب الشاشة
            _shell.GameHub.HubConnection.On<string>("MessagesReadBy", (friendName) =>
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
                                // 🟢 استبدال الكائن بنسخة جديدة حالتها Read ليتلون بالأزرق فوراً
                                Messages[i] = new ChatBubbleModel
                                {
                                    Id = msg.Id,
                                    Content = msg.Content,
                                    Timestamp = msg.Timestamp,
                                    IsMine = msg.IsMine,
                                    Status = MessageStatus.Read // صحين أزرق
                                };
                                isChanged = true;
                            }
                        }
                        if (isChanged) SaveMessagesOffline();
                    });
                }
            });
        }
    }

    // 🟢 دالة إرسال الرسائل المعلقة عند عودة الإنترنت
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
                    Messages[i] = msg; // تحديث الواجهة بدون قفز
                    hasChanges = true;
                }
                catch
                {
                    // تبقى معلقة
                }
            }
        }

        if (hasChanges) SaveMessagesOffline();
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        MessageEntry.Text = string.Empty; // تفريغ النص أولاً

        var newMsg = new ChatBubbleModel
        {
            Content = text,
            Timestamp = DateTime.Now,
            IsMine = true,
            Status = MessageStatus.Pending
        };

        Messages.Add(newMsg);

        // استدعاء التمرير بعد إضافة الرسالة
        ScrollToBottom();
        SaveMessagesOffline();

        // محاولة الإرسال للسيرفر
        try
        {
            if (_shell?.GameHub?.HubConnection?.State == HubConnectionState.Connected)
            {
                await _shell.GameHub.HubConnection.InvokeAsync("SendDirectMessage", FriendName, text);

                var index = Messages.IndexOf(newMsg);
                if (index >= 0)
                {
                    newMsg.Status = MessageStatus.Sent;
                    Messages[index] = newMsg; // تحديث ناعم في مكانه بدلاً من مسح القائمة
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
                // إعطاء مهلة صغيرة جداً لواجهة المستخدم لإعادة رسم نفسها بعد ظهور الكيبورد
                await Task.Delay(100);

                var lastMessage = Messages.Last();

                // استخدام animate: false يجعل التمرير فوري ودقيق ولا يتعارض مع حركة الكيبورد
                MessagesList.ScrollTo(lastMessage, position: ScrollToPosition.End, animate: false);
            });
        }
    }

    private void LoadOfflineMessages()
    {
        string cacheKey = $"ChatHistory_{FriendName}";
        var json = Preferences.Get(cacheKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var savedMsgs = JsonSerializer.Deserialize<ObservableCollection<ChatBubbleModel>>(json);
                if (savedMsgs != null && Messages.Count == 0)
                {
                    Messages.Clear();
                    foreach (var msg in savedMsgs) Messages.Add(msg);
                    ScrollToBottom();
                }
            }
            catch { }
        }
    }

    private void SaveMessagesOffline()
    {
        string cacheKey = $"ChatHistory_{FriendName}";
        string json = JsonSerializer.Serialize(Messages);
        Preferences.Set(cacheKey, json);
    }
}