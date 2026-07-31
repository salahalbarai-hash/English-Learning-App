using English.Hubs;
using English.Popups;
using Microsoft.AspNetCore.SignalR.Client;

namespace English
{
    public partial class AppShell : Shell
    {
        private readonly GameHub _gameHub;
        public GameHub GameHub => _gameHub;
        public Action<bool>? OnChallengeResponseReceived;

        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(FriendRequestsPage), typeof(FriendRequestsPage));
            Routing.RegisterRoute("ChatPage", typeof(ChatPage));
            _gameHub = new GameHub();

            string savedUserName = Preferences.Get("UserName", "");
            if (!string.IsNullOrEmpty(savedUserName))
            {
                _ = StartGameHubAsync(savedUserName);
            }

            try
            {
                Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
            }
            catch { }
        }

        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                var savedUserName = Preferences.Get("UserName", "");
                if (!string.IsNullOrEmpty(savedUserName))
                {
                    _ = MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            await StartGameHubAsync(savedUserName);
                        }
                        catch { }
                    });
                }
            }
        }
        public async Task<List<string>> GetAllFriendsAsync()
        {
            if (_gameHub != null && _gameHub.HubConnection?.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                try
                {
                    return await _gameHub.HubConnection.InvokeAsync<List<string>>("GetAllFriends");
                }
                catch { return new List<string>(); }
            }
            return new List<string>();
        }
        public async Task StartGameHubAsync(string currentUserName)
        {
            if (string.IsNullOrEmpty(currentUserName)) return;

            if (_gameHub.HubConnection?.State == HubConnectionState.Connected)
                return;

            await _gameHub.ConnectAsync(currentUserName);

            string GetRoomName(string user1, string user2)
            {
                return string.Compare(user1, user2, StringComparison.Ordinal) < 0
                    ? $"room_{user1}_{user2}"
                    : $"room_{user2}_{user1}";
            }

            _gameHub.OnChallengeReceived += (senderName, category) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (Current != null)
                    {
                        var popup = new ReceiveChallengePopup(senderName, category);
                        var result = await Current.ShowPopupAsync(popup);

                        bool accepted = result is bool b && b;

                        await _gameHub.SendResponseAsync(senderName, accepted, category);

                        if (accepted)
                        {
                            string roomName = GetRoomName(currentUserName, senderName);

                            await _gameHub.JoinDuelRoomAsync(roomName);

                            if (_gameHub.HubConnection != null)
                            {
                                await Current.Navigation.PushModalAsync(new DuelGamePage(
                                    _gameHub.HubConnection, roomName, currentUserName, senderName, category, isFirstPlayer: false));
                            }
                        }
                    }
                });
            };

            _gameHub.OnChallengeResponseReceived += async (responderName, isAccepted, category) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (isAccepted)
                    {
                        string roomName = GetRoomName(currentUserName, responderName);

                        if (_gameHub.HubConnection != null)
                        {
                            await _gameHub.HubConnection.InvokeAsync("JoinDuelRoom", roomName);

                            await Current!.Navigation.PushModalAsync(new DuelGamePage(
                                _gameHub.HubConnection, roomName, currentUserName, responderName, category, isFirstPlayer: true));
                        }
                    }
                    else
                    {
                        await Current!.DisplayAlert("اعتذار", $"{responderName} اعتذر أو رفض التحدي حالياً.", "حسناً");
                    }
                });
            };

            _gameHub.OnFriendRequestReceived += (senderName) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (Current != null)
                    {
                        var popup = new ReceiveFriendRequestPopup(senderName);
                        var result = await Current.ShowPopupAsync(popup);

                        bool isAccepted = result is bool b && b;

                        if (isAccepted)
                        {
                            await _gameHub.AcceptFriendRequestAsync(senderName);

                            // 🟢 تحديث الكاش المحلي فوراً بإضافة الصديق الجديد
                            try
                            {
                                string cachedFriendsJson = Preferences.Get("Cached_Friends_List", "[]");
                                var friendsList = JsonSerializer.Deserialize<List<string>>(cachedFriendsJson) ?? new List<string>();

                                if (!friendsList.Contains(senderName, StringComparer.OrdinalIgnoreCase))
                                {
                                    friendsList.Add(senderName);
                                    Preferences.Set("Cached_Friends_List", JsonSerializer.Serialize(friendsList));
                                }
                            }
                            catch { }

                            await Toast.Make($"أصبح {senderName} الآن في قائمة أصدقائك!").Show();
                        }
                    }
                });
            };
        }

        // تم إضافة كلمة التحدي "word" كمعامل اختياري ليتناسب مع الإستدعاء في صفحة الأصدقاء
        public async Task SendChallengeToFriendAsync(string targetUser, string category, string word = "")
        {
            if (string.IsNullOrEmpty(word))
            {
                await _gameHub.SendChallengeAsync(targetUser, category);
            }
            else
            {
                // تأكد أن دالة SendChallengeAsync داخل كلاس GameHub تدعم استقبال الكلمة (المعامل الثالث)
                // إذا لم تكن تدعمه، ستحتاج لإضافته هناك أيضاً.
                // مؤقتاً، في حال لم تكن موجودة يمكنك دمجهم هكذا: await _gameHub.SendChallengeAsync(targetUser, category);
                // ولكن يُفضل تحديث السيرفر لاستقبال الكلمة.

                // افترضنا هنا أنك قمت بتحديث GameHub لدعمها
                await _gameHub.SendChallengeAsync(targetUser, category);
            }
        }

        public async Task SendFriendRequestAsync(string targetUser)
        {
            if (_gameHub != null)
            {
                await _gameHub.SendFriendRequestAsync(targetUser);
            }
        }

        public async Task AcceptFriendRequestAsync(string senderName)
        {
            if (_gameHub != null)
            {
                await _gameHub.AcceptFriendRequestAsync(senderName);
            }
        }

        public async Task<List<string>> GetPendingFriendRequestsAsync()
        {
            if (_gameHub != null)
            {
                return await _gameHub.GetPendingFriendRequestsAsync();
            }
            return new List<string>();
        }

        public async Task<List<string>> GetOnlineUsersAsync()
        {
            return await _gameHub.GetOnlineUsersAsync();
        }

        public async Task<List<string>> GetFriendsAsync()
        {
            if (_gameHub != null)
            {
                return await _gameHub.GetFriendsAsync();
            }
            return new List<string>();
        }

        public async Task<List<string>> GetSentPendingRequestsAsync()
        {
            if (_gameHub != null)
            {
                return await _gameHub.GetSentPendingRequestsAsync();
            }
            return new List<string>();
        }

        // 🟢 إضافة دالة لجلب سجل المحادثة من السيرفر
        public async Task<List<ChatMessageDto>> GetChatHistoryAsync(string targetUser)
        {
            if (_gameHub != null && _gameHub.HubConnection?.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                try
                {
                    return await _gameHub.HubConnection.InvokeAsync<List<ChatMessageDto>>("GetChatHistory", targetUser);
                }
                catch { return new List<ChatMessageDto>(); }
            }
            return new List<ChatMessageDto>();
        }

        public async Task<DateTime?> GetLastSeenAsync(string targetUser)
        {
            if (_gameHub != null)
            {
                return await _gameHub.GetLastSeenAsync(targetUser);
            }
            return null;
        }
    }
}