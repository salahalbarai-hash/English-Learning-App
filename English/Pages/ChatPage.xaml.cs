using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Alerts;
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

    public ObservableCollection<ChatBubbleModel> Messages { get; set; } = new();
    private AppShell? _shell;
    private string _currentUserName = "";

    public System.Windows.Input.ICommand LongPressCommand { get; private set; }
    public System.Windows.Input.ICommand TapCommand { get; private set; }

    private IDisposable? _receiveSubscription;
    private IDisposable? _deliveredSubscription;
    private IDisposable? _readSubscription;

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
                {
                    item.OnTappedAction = OnMessageTappedCommand;
                    item.OnLongPressedAction = OnMessageLongPressedCommand;
                    item.IsSelectionMode = IsSelectionModeActive;
                }
            }
        };

        MessagesList.ItemsSource = Messages;
        _shell = Shell.Current as AppShell;
        _currentUserName = Preferences.Get("UserName", "");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        this.AnimatePageInAsync();

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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
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

    private void OnBackClicked(object sender, EventArgs e)
    {
        Shell.Current.GoToAsync("..");
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

            // 2. إذا كان SignalR مفصولاً، أو الصديق غير متصل الآن، نجلب "آخر ظهور" من السيرفر
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

    private async Task LoadServerChatHistory()
    {
        if (_shell != null && !string.IsNullOrEmpty(FriendName))
        {
            var serverMessages = await _shell.GetChatHistoryAsync(FriendName);
            if (serverMessages != null && serverMessages.Count > 0)
            {
                // جلب القائمة السوداء للرسائل المحذوفة محلياً (حذف لدي)
                string blacklistJson = Preferences.Get("DeletedForMe_List", "[]");
                var blacklist = JsonSerializer.Deserialize<List<int>>(blacklistJson) ?? new List<int>();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // لتفادي خلل الـ CollectionView في MAUI (Ghost items) الذي يمنع التحديد السليم
                    // نقوم بفصل الـ ItemsSource مؤقتاً قبل مسح وإضافة العناصر
                    MessagesList.ItemsSource = null;
                    Messages.Clear();

                    foreach (var sm in serverMessages)
                    {
                        // تخطي الرسائل التي تم حذفها محلياً
                        if (blacklist.Contains(sm.Id)) continue;

                        bool isMine = sm.Sender.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase);

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
                    
                    // إعادة ربط القائمة
                    MessagesList.ItemsSource = Messages;
                    ScrollToBottom();
                    SaveMessagesOffline();
                });
            }
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
                SaveMessagesOffline();
            }
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

        // جلب النسخة الحقيقية الموجودة في القائمة حالياً (لتفادي مشكلة تواجد نسخ قديمة في الذاكرة)
        var realMsg = Messages.FirstOrDefault(m => m.Id == msg.Id) ?? msg;

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
            var realMsg = Messages.FirstOrDefault(m => m.Id == msg.Id) ?? msg;
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

        // التحقق مما إذا كان مسموحاً الحذف للجميع (كل الرسائل المحددة يجب أن تكون IsMine)
        bool canDeleteForEveryone = selectedMessages.All(m => m.IsMine);

        // إظهار نافذة التأكيد المخصصة
        var popup = new English.Popups.DeleteConfirmPopup(canDeleteForEveryone);
        var result = await Shell.Current.ShowPopupAsync(popup);
        
        string action = result as string ?? "";

        if (action == "DeleteForMe")
        {
            string blacklistJson = Preferences.Get("DeletedForMe_List", "[]");
            var blacklist = JsonSerializer.Deserialize<List<int>>(blacklistJson) ?? new List<int>();

            foreach (var msg in selectedMessages)
            {
                if (!blacklist.Contains(msg.Id))
                {
                    blacklist.Add(msg.Id);
                }
                Messages.Remove(msg);
            }

            Preferences.Set("DeletedForMe_List", JsonSerializer.Serialize(blacklist));
            SaveMessagesOffline();
            CloseSelectionMode();
        }
        else if (action == "DeleteForEveryone")
        {
            string blacklistJson = Preferences.Get("DeletedForMe_List", "[]");
            var blacklist = JsonSerializer.Deserialize<List<int>>(blacklistJson) ?? new List<int>();
            bool blacklistChanged = false;

            foreach (var msg in selectedMessages)
            {
                Messages.Remove(msg);
                if (_shell?.GameHub != null)
                {
                    bool success = await _shell.GameHub.DeleteMessageAsync(msg.Id);
                    if (!success)
                    {
                        // إذا رفض السيرفر الحذف (مثلاً الرسالة قديمة جداً)، نقوم بحذفها محلياً على الأقل
                        if (!blacklist.Contains(msg.Id))
                        {
                            blacklist.Add(msg.Id);
                            blacklistChanged = true;
                        }
                    }
                }
            }

            if (blacklistChanged)
            {
                Preferences.Set("DeletedForMe_List", JsonSerializer.Serialize(blacklist));
            }

            SaveMessagesOffline();
            CloseSelectionMode();
        }
        else
        {
            // Cancel
            CloseSelectionMode();
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

    private void LoadOfflineMessages()
    {
        string filePath = Path.Combine(FileSystem.AppDataDirectory, $"chat_{FriendName}.json");
        if (!File.Exists(filePath)) return;

        try
        {
            var json = File.ReadAllText(filePath);
            if (!string.IsNullOrEmpty(json))
            {
                var savedMsgs = JsonSerializer.Deserialize<List<ChatBubbleModel>>(json);
                if (savedMsgs != null && Messages.Count == 0)
                {
                    Messages.Clear();
                    foreach (var msg in savedMsgs)
                    {
                        // يجب تعيين الـ Actions يدوياً لأن JsonIgnore يمنع حفظها
                        msg.OnTappedAction = OnMessageTappedCommand;
                        msg.OnLongPressedAction = OnMessageLongPressedCommand;
                        msg.IsSelectionMode = IsSelectionModeActive;
                        Messages.Add(msg);
                    }
                    ScrollToBottom();
                }
            }
        }
        catch { }
    }

    private void SaveMessagesOffline()
    {
        try
        {
            string filePath = Path.Combine(FileSystem.AppDataDirectory, $"chat_{FriendName}.json");
            string json = JsonSerializer.Serialize(Messages);
            File.WriteAllText(filePath, json);
        }
        catch { }
    }
}