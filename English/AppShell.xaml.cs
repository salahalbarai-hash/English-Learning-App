using CommunityToolkit.Maui.Alerts;
using English.Hubs;
using English.Popups;
using English.Pages;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Dispatching;

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
            _gameHub = new GameHub();

            // 🟢 الأهم هنا: فحص هل المستخدم مسجل دخول مسبقاً عند فتح التطبيق مباشرة؟
            string savedUserName = Preferences.Get("UserName", "");
            if (!string.IsNullOrEmpty(savedUserName))
            {
                _ = StartGameHubAsync(savedUserName);
            }

            // الاستماع لتغيرات اتصال الشبكة لإعادة المحاولة تلقائياً عند توفر الإنترنت
            try
            {
                Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
            }
            catch { }
        }

        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            // إذا أصبح لدينا اتصال إنترنت فعليًا
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                var savedUserName = Preferences.Get("UserName", "");
                if (!string.IsNullOrEmpty(savedUserName))
                {
                    // تشغيل الاتصال في الخلفية ولكن على خيط الواجهة عند الحاجة
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

        // دالة بدء تشغيل الاتصال وربط الأحداث بالاسم الصحيح للمستخدم
        public async Task StartGameHubAsync(string currentUserName)
        {
            if (string.IsNullOrEmpty(currentUserName)) return;

            // التأكد من عدم إعادة الاتصال إذا كان متصلاً مسبقاً
            if (_gameHub.HubConnection?.State == HubConnectionState.Connected)
                return;

            await _gameHub.ConnectAsync(currentUserName);

            // دالة مساعدة لتوليد اسم غرفة فريد وموحد وثابت بغض النظر عن من بدأ التحدي
            string GetRoomName(string user1, string user2)
            {
                return string.Compare(user1, user2, StringComparison.Ordinal) < 0
                    ? $"room_{user1}_{user2}"
                    : $"room_{user2}_{user1}";
            }

            // 1. الاستماع لطلبات التحدي الواردة (أنت المستقبل)
            _gameHub.OnChallengeReceived += (senderName, category) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (Current != null)
                    {
                        var popup = new ReceiveChallengePopup(senderName, category);
                        var result = await Current.ShowPopupAsync(popup);

                        bool accepted = result is bool b && b;

                        // إرسال الرد للطرف الآخر
                        await _gameHub.SendResponseAsync(senderName, accepted, category);

                        if (accepted)
                        {
                            string roomName = GetRoomName(currentUserName, senderName);

                            // الانضمام لغرفة التحدي عبر SignalR
                            await _gameHub.JoinDuelRoomAsync(roomName);

                            // فتح شاشة المبارزة (المستقبل ليس هو اللاعب الأول في بدء السؤال)
                            if (_gameHub.HubConnection != null)
                            {
                                await Current.Navigation.PushModalAsync(new DuelGamePage(
                                    _gameHub.HubConnection, roomName, currentUserName, senderName, category, isFirstPlayer: false));
                            }
                        }
                    }
                });
            };

            // 2. الاستماع لرد الصديق على التحدي (للمرسل)
            _gameHub.OnChallengeResponseReceived += async (responderName, isAccepted, category) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (isAccepted)
                    {
                        string roomName = GetRoomName(currentUserName, responderName);

                        // الانضمام لغرفة التحدي عبر SignalR
                        if (_gameHub.HubConnection != null)
                        {
                            await _gameHub.HubConnection.InvokeAsync("JoinDuelRoom", roomName);

                            // فتح شاشة المبارزة المباشرة (المرسل يبدأ كـ isFirstPlayer: true)
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

            // 3. الاستماع لطلبات الصداقة الواردة
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
                            await Toast.Make($"أصبح {senderName} الآن في قائمة أصدقائك!").Show();
                        }
                    }
                });
            };
        }

        public async Task SendChallengeToFriendAsync(string targetUser, string category)
        {
            await _gameHub.SendChallengeAsync(targetUser, category);
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
    }
}