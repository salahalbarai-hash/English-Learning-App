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
        private bool _isEventsSubscribed = false;

        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(FriendRequestsPage), typeof(FriendRequestsPage));
            Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
            Routing.RegisterRoute(nameof(SmartInspectorPage), typeof(SmartInspectorPage));
            Routing.RegisterRoute(nameof(ChoiceChallengePage), typeof(ChoiceChallengePage));
            Routing.RegisterRoute(nameof(WritingChallengePage), typeof(WritingChallengePage));
            Routing.RegisterRoute(nameof(WordVideosPage), typeof(WordVideosPage));
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
                            // Try to sync any pending coin updates when connection is restored
                            _ = Task.Run(async () => { try { await Service.TrySyncPendingCoinsAsync(); } catch { } });
                        }
                        catch { }
                    });
                }
            }
        }

        public async Task StartGameHubAsync(string currentUserName)
        {
            if (string.IsNullOrEmpty(currentUserName)) return;

            if (_gameHub.HubConnection?.State == HubConnectionState.Connected)
                return;

            await _gameHub.ConnectAsync(currentUserName);

            if (_isEventsSubscribed) return;
            _isEventsSubscribed = true;

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

                        // If current user accepted the challenge, deduct stake coins and FriendsChallengeCount locally and persist
                        if (accepted)
                        {
                            try
                            {
                                // determine stake by category (support Arabic and internal codes)
                                int stake = 0;
                                if (category != null && (category.Contains("خيارات") || category.Contains("Choice") || category.Contains("تحدي الخيارات"))) stake = 3;
                                else if (category != null && (category.Contains("كتابة") || category.Contains("Writing") || category.Contains("تحدي الكتابة"))) stake = 5;

                                if (stake > 0)
                                {
                                    long id = Convert.ToInt64(Preferences.Get("ID", "0"));

                                    // خصم الكوينز
                                    long coins = Preferences.Get("Coins", 0L) - stake;
                                    Preferences.Set("Coins", coins);

                                    // خصم عدد محاولات تحدي الأصدقاء
                                    long challengeCount = Preferences.Get("FriendsChallengeCount", 0L);
                                    if (challengeCount > 0) challengeCount -= 1;
                                    Preferences.Set("FriendsChallengeCount", challengeCount);

                                    if (await Service.HasActiveInternetAsync(5))
                                    {
                                        var res = await Service.UpdateCoins(new User { ID = id, Coins = coins });
                                        if (res != "1") Service.AddPendingCoinsUpdate(id, coins);

                                        await Service.UpdateFriendsChallengeCount(new User { ID = id, FriendsChallengeCount = challengeCount });
                                    }
                                    else
                                    {
                                        Service.AddPendingCoinsUpdate(id, coins);
                                    }
                                }
                            }
                            catch { }
                        }

                        await _gameHub.SendResponseAsync(senderName, accepted, category);

                        if (accepted)
                        {
                            string roomName = GetRoomName(currentUserName, senderName);

                            await _gameHub.JoinDuelRoomAsync(roomName);

                            if (_gameHub.HubConnection != null)
                            {
                                if (category == "تحدي الخيارات")
                                {
                                    await Current.Navigation.PushModalAsync(new ChoiceChallengePage());
                                }
                                else
                                {
                                    await Current.Navigation.PushModalAsync(new DuelGamePage(
                                        _gameHub.HubConnection, roomName, currentUserName, senderName, category, isFirstPlayer: false));
                                }
                            }
                        }
                    }
                });
            };

            _gameHub.OnChallengeWithWordReceived += (senderName, category, word) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (Current != null)
                    {
                        // --- تحديد قيمة الرهان حسب نوع التحدي ---
                        int stake = 0;
                        if (category == "ChoiceMulti") stake = 3;
                        else if (category == "WritingMulti") stake = 5;

                        // --- فحص رصيد المُستقبِل قبل عرض popup القبول ---
                        if (stake > 0)
                        {
                            long receiverCoins = Preferences.Get("Coins", 0L);
                            long receiverChallengeCount = Preferences.Get("FriendsChallengeCount", 0L);

                            if (receiverChallengeCount <= 0)
                            {
                                // رفض تلقائي: لا توجد محاولات كافية
                                await _gameHub.SendResponseAsync(senderName, false, category);
                                await Toast.Make("لا توجد لديك محاولات كافية لقبول هذا التحدي ⚠️").Show();
                                return;
                            }

                            if (receiverCoins < stake)
                            {
                                // رفض تلقائي: لا يوجد رصيد كافٍ
                                await _gameHub.SendResponseAsync(senderName, false, category);
                                await Toast.Make($"لا يوجد لديك عملات كافية لقبول هذا التحدي (مطلوب {stake} عملات) ⚠️").Show();
                                return;
                            }
                        }

                        string displayCategory = category switch
                        {
                            "ChoiceMulti" => "تحدي الخيارات",
                            "WritingMulti" => "تحدي الكتابة",
                            _ => category
                        };
                        var popup = new ReceiveChallengePopup(senderName, displayCategory);
                        var result = await Current.ShowPopupAsync(popup);

                        bool accepted = result is bool b && b;

                        // --- خصم الكوينز و FriendsChallengeCount عند القبول ---
                        if (accepted && stake > 0)
                        {
                            try
                            {
                                long id = Convert.ToInt64(Preferences.Get("ID", "0"));

                                // خصم الكوينز
                                long coins = Preferences.Get("Coins", 0L) - stake;
                                Preferences.Set("Coins", coins);

                                // خصم عدد محاولات تحدي الأصدقاء
                                long challengeCount = Preferences.Get("FriendsChallengeCount", 0L);
                                if (challengeCount > 0) challengeCount -= 1;
                                Preferences.Set("FriendsChallengeCount", challengeCount);

                                // حفظ التغييرات على السيرفر
                                if (await Service.HasActiveInternetAsync(5))
                                {
                                    var res = await Service.UpdateCoins(new User { ID = id, Coins = coins });
                                    if (res != "1") Service.AddPendingCoinsUpdate(id, coins);

                                    await Service.UpdateFriendsChallengeCount(new User { ID = id, FriendsChallengeCount = challengeCount });
                                }
                                else
                                {
                                    Service.AddPendingCoinsUpdate(id, coins);
                                }
                            }
                            catch { }
                        }

                        await _gameHub.SendResponseAsync(senderName, accepted, category);

                        if (accepted)
                        {
                            string roomName = GetRoomName(currentUserName, senderName);

                            await _gameHub.JoinDuelRoomAsync(roomName);

                            if (_gameHub.HubConnection != null)
                            {
                                if (category == "ChoiceMulti")
                                {
                                    await Current.Navigation.PushModalAsync(new ChoiceChallengePage(_gameHub.HubConnection, roomName, senderName, word));
                                }
                                else if (category == "WritingMulti")
                                {
                                    await Current.Navigation.PushModalAsync(new WritingChallengePage(_gameHub.HubConnection, roomName, senderName, word));
                                }
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

                            if (category == "ChoiceMulti" || category == "WritingMulti")
                            {
                                // The sender page will handle it internally
                            }
                            else if (category == "تحدي الخيارات")
                            {
                                await Current!.Navigation.PushModalAsync(new ChoiceChallengePage());
                            }
                            else
                            {
                                await Current!.Navigation.PushModalAsync(new DuelGamePage(
                                    _gameHub.HubConnection, roomName, currentUserName, responderName, category, isFirstPlayer: true));
                            }
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
                await _gameHub.SendChallengeAsync(targetUser, category, word);
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