using System.Text.Json;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using English.Models;
using English.Services;
using Microsoft.AspNetCore.SignalR.Client;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace English.Pages
{
    public partial class SkywardBirdPage : ContentPage
    {
        public class SkywardBirdPayload
        {
            public string SenderColor { get; set; } = "#38BDF8";
            public List<string> AvailableColors { get; set; } = new();
            public List<WordModel> Words { get; set; } = new();
        }

        private class WordCloud
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }
            public string EnglishWord { get; set; } = string.Empty;
            public bool IsTarget { get; set; }
            public bool IsActive { get; set; } = true;
        }

        private class SparkParticle
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float VelX { get; set; }
            public float VelY { get; set; }
            public float Alpha { get; set; } = 1f;
            public SKColor Color { get; set; }
            public float Radius { get; set; }
        }

        private class Star
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Size { get; set; }
            public float Alpha { get; set; }
        }

        // --- البيانات والكلمات ---
        private GameSessionConfig? _config;
        private List<WordModel> _gameWords = new();
        private List<WordModel> _memorizedWords = new();
        private WordModel? _currentTargetWord;
        private int _currentWordIndex = 0;

        // --- ألوان الطيور المخصصة والتباين الذكي ---
        private string _myBirdColorHex = "#38BDF8";
        private string _opponentBirdColorHex = "#F43F5E";

        // --- وضع التحدي الثنائي (Multiplayer Duel) والكوينز ---
        private bool _isMultiplayer = false;
        private HubConnection? _hubConnection;
        private string _roomName = string.Empty;
        private string _opponentName = string.Empty;
        private string _currentUserName = string.Empty;
        private int _opponentScore = 0;
        private int _opponentHearts = 3;
        private bool _hasOpponentFinished = false;
        private bool _hasLocalFinished = false;
        private bool _isLeaving = false;
        private const int DuelStake = 5; // رهان تحدي الصديق: 5 كوينز لكل لاعب، الفائز يحصد 10 كوينز
        private IDispatcherTimer? _syncTimer;

        // --- تزامن بدء التحدي الثنائي (Ready Handshake & Countdown) ---
        private bool _isLocalReady = false;
        private bool _isOpponentReady = false;
        private bool _isCountingDown = false;
        private bool _isWaitingForOpponentReady = false;
        private IDispatcherTimer? _readyHandshakeTimer;
        private IDispatcherTimer? _countdownTimer;
        private int _countdownSeconds = 3;

        // --- فيزياء الطائرين (Horizontal Clouds Drift + Vertical Bird Flap) ---
        // 1. الطائر المحلي (Player 1 - الأزرق 🕊️)
        private float _birdX = 180f;
        private float _birdY = 650f;
        private float _birdVelY = 0f;
        private const float Gravity = 0.45f;
        private const float FlapStrength = -9.2f;
        private float _wingPhase = 0f;

        // 2. طائر الصديق المنافس (Player 2 - الوردي 🦅)
        private float _opponentBirdX = 180f;
        private float _opponentBirdY = 650f;
        private float _opponentTargetBirdY = 650f;
        private float _opponentBirdVelY = 0f;
        private float _opponentWingPhase = 0f;

        // --- السحب والجزيئات والنجوم ---
        private readonly List<WordCloud> _clouds = new();
        private readonly List<SparkParticle> _particles = new();
        private readonly List<Star> _stars = new();

        // --- حالة اللعبة ---
        private bool _isPlaying = false;
        private bool _isGameOver = false;
        private int _hearts = 3;
        private int _combo = 1;
        private int _sessionScore = 0;
        private int _collectedCount = 0;
        private IDispatcherTimer? _gameLoopTimer;

        // --- فرَش SkiaSharp المعاد تدويرها لكلا الطائرين والمحيط ---
        private readonly SKPaint _birdBodyPaint = new() { Color = SKColor.Parse("#38BDF8"), Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _birdWingPaint = new() { Color = SKColor.Parse("#0284C7"), Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _birdEyePaint = new() { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _birdPupilPaint = new() { Color = SKColors.Black, Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _birdBeakPaint = new() { Color = SKColor.Parse("#F59E0B"), Style = SKPaintStyle.Fill, IsAntialias = true };

        private readonly SKPaint _opponentBodyPaint = new() { Color = SKColor.Parse("#F43F5E"), Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _opponentWingPaint = new() { Color = SKColor.Parse("#BE123C"), Style = SKPaintStyle.Fill, IsAntialias = true };

        private readonly SKPaint _cloudFillPaint = new() { Color = SKColor.Parse("#16233B"), Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _cloudBorderPaint = new() { Color = SKColor.Parse("#38BDF8"), Style = SKPaintStyle.Stroke, StrokeWidth = 3f, IsAntialias = true };
        private readonly SKPaint _cloudTextPaint = new()
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            TextSize = 28f,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        private readonly SKPaint _particlePaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _starPaint = new() { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };

        // ============================================================
        // 🏗️ المُنشئات (Constructors)
        // ============================================================

        public SkywardBirdPage()
        {
            InitializeComponent();
            _currentUserName = Preferences.Get("UserName", "أنا");
            LoadSetupData();
        }

        public SkywardBirdPage(GameSessionConfig config)
        {
            InitializeComponent();
            _config = config;
            _currentUserName = Preferences.Get("UserName", "أنا");

            if (_config != null && _config.Mode == GameLaunchMode.TenWordsPractice)
            {
                _isMultiplayer = false;
                _gameWords = _config.Words.Where(w => !string.IsNullOrWhiteSpace(w.EnglishWord) && !string.IsNullOrWhiteSpace(w.ArabicWord)).ToList();
                SetupView.IsVisible = false;
                GameView.IsVisible = true;
                UpdateGameModeUIVisibility();
            }
            else
            {
                LoadSetupData();
            }
        }

        public SkywardBirdPage(HubConnection hubConnection, string roomName, string opponentName, string payloadJson)
        {
            InitializeComponent();
            _currentUserName = Preferences.Get("UserName", "أنا");
            _hubConnection = hubConnection;
            _roomName = roomName;
            _opponentName = opponentName;
            _isMultiplayer = true;

            InitializeColors();

            SetupView.IsVisible = false;
            GameView.IsVisible = true;
            UpdateGameModeUIVisibility();

            MyNameLabel.Text = _currentUserName;
            OpponentNameLabel.Text = _opponentName;

            SetupMultiplayerCurrentConnection(payloadJson);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (GameView.IsVisible && !_isPlaying && !_isGameOver)
            {
                if (_isMultiplayer)
                {
                    if (_gameWords.Count > 0 && !_isLocalReady && !_isCountingDown)
                    {
                        PrepareMultiplayerSync();
                    }
                }
                else
                {
                    StartGame();
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopGame();
            StopSyncTimer();
            StopReadyHandshakeTimer();
        }

        // ============================================================
        // ⚙️ منطق إعداد التحدي النظيف (Clean Setup Logic)
        // ============================================================

        private void LoadSetupData()
        {
            _memorizedWords = TenWords.GetMemorizedWords();
            MaxWordsLabel.Text = _memorizedWords.Count.ToString();
            UpdateModeCardsUI();
            InitializeColors();
        }

        private void OnSinglePlayerTapped(object sender, TappedEventArgs e)
        {
            _isMultiplayer = false;
            UpdateModeCardsUI();
        }

        private void OnMultiplayerTapped(object sender, TappedEventArgs e)
        {
            _isMultiplayer = true;
            UpdateModeCardsUI();
        }

        private void UpdateModeCardsUI()
        {
            Color selectedBorder = Color.FromArgb("#6366F1");
            Color normalBorder = Color.FromArgb("#23344D");
            Color selectedBg = Color.FromArgb("#141838");
            Color normalBg = Color.FromArgb("#101726");

            SinglePlayerCard.BackgroundColor = !_isMultiplayer ? selectedBg : normalBg;
            SinglePlayerCard.Stroke = !_isMultiplayer ? selectedBorder : normalBorder;
            SinglePlayerText.TextColor = !_isMultiplayer ? Colors.White : Color.FromArgb("#64748B");

            MultiplayerCard.BackgroundColor = _isMultiplayer ? selectedBg : normalBg;
            MultiplayerCard.Stroke = _isMultiplayer ? selectedBorder : normalBorder;
            MultiplayerText.TextColor = _isMultiplayer ? Colors.White : Color.FromArgb("#64748B");
        }

        private void UpdateGameModeUIVisibility()
        {
            SinglePlayerHud.IsVisible = !_isMultiplayer;
            MultiplayerHud.IsVisible = _isMultiplayer;
        }

        // ============================================================
        // 🎨 إدارة ألوان الطيور والتخصيص البصري
        // ============================================================

        private void InitializeColors()
        {
            _myBirdColorHex = Preferences.Get("MyBirdColor", "#38BDF8");
            if (string.IsNullOrEmpty(_opponentBirdColorHex) || _opponentBirdColorHex.Equals(_myBirdColorHex, StringComparison.OrdinalIgnoreCase))
            {
                _opponentBirdColorHex = _myBirdColorHex.Equals("#38BDF8", StringComparison.OrdinalIgnoreCase) ? "#F43F5E" : "#38BDF8";
            }
            ApplyBirdColors();
            UpdateSetupColorSelectionUI(_myBirdColorHex);
        }

        private void OnSetupColorCyanTapped(object sender, TappedEventArgs e) => SelectSetupColor("#38BDF8");
        private void OnSetupColorRoseTapped(object sender, TappedEventArgs e) => SelectSetupColor("#F43F5E");
        private void OnSetupColorGoldTapped(object sender, TappedEventArgs e) => SelectSetupColor("#F59E0B");
        private void OnSetupColorEmeraldTapped(object sender, TappedEventArgs e) => SelectSetupColor("#10B981");

        private void SelectSetupColor(string hex)
        {
            _myBirdColorHex = hex;
            Preferences.Set("MyBirdColor", hex);
            ApplyBirdColors();
            UpdateSetupColorSelectionUI(hex);
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
        }

        private void UpdateSetupColorSelectionUI(string selectedHex)
        {
            SetSetupBorder(SetupColorCyanBorder, SetupColorCyanCheck, selectedHex.Equals("#38BDF8", StringComparison.OrdinalIgnoreCase), "#38BDF8");
            SetSetupBorder(SetupColorRoseBorder, SetupColorRoseCheck, selectedHex.Equals("#F43F5E", StringComparison.OrdinalIgnoreCase), "#F43F5E");
            SetSetupBorder(SetupColorGoldBorder, SetupColorGoldCheck, selectedHex.Equals("#F59E0B", StringComparison.OrdinalIgnoreCase), "#F59E0B");
            SetSetupBorder(SetupColorEmeraldBorder, SetupColorEmeraldCheck, selectedHex.Equals("#10B981", StringComparison.OrdinalIgnoreCase), "#10B981");
        }

        private static void SetSetupBorder(Border? border, Label? check, bool isSelected, string baseHex)
        {
            if (border == null || check == null) return;
            if (isSelected)
            {
                border.Stroke = Colors.White;
                border.StrokeThickness = 3;
                border.Scale = 1.12;
                check.IsVisible = true;
            }
            else
            {
                border.Stroke = Color.FromArgb(baseHex);
                border.StrokeThickness = 1;
                border.Scale = 1.0;
                check.IsVisible = false;
            }
        }

        private static SKColor DarkenColor(SKColor color, float factor = 0.72f)
        {
            byte r = (byte)Math.Clamp((int)(color.Red * factor), 0, 255);
            byte g = (byte)Math.Clamp((int)(color.Green * factor), 0, 255);
            byte b = (byte)Math.Clamp((int)(color.Blue * factor), 0, 255);
            return new SKColor(r, g, b, color.Alpha);
        }

        private void ApplyBirdColors()
        {
            try
            {
                var myColor = SKColor.Parse(_myBirdColorHex);
                _birdBodyPaint.Color = myColor;
                _birdWingPaint.Color = DarkenColor(myColor, 0.72f);

                // ضمان التباين: إذا كان لون الخصم مطابقاً للوني يتم تغييره فوراً
                if (_opponentBirdColorHex.Equals(_myBirdColorHex, StringComparison.OrdinalIgnoreCase))
                {
                    _opponentBirdColorHex = _myBirdColorHex.Equals("#38BDF8", StringComparison.OrdinalIgnoreCase) ? "#F43F5E" : "#38BDF8";
                }
                var opColor = SKColor.Parse(_opponentBirdColorHex);
                _opponentBodyPaint.Color = opColor;
                _opponentWingPaint.Color = DarkenColor(opColor, 0.72f);
            }
            catch { }
        }

        private void SetOpponentBirdColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return;
            _opponentBirdColorHex = hex;
            ApplyBirdColors();
            GameCanvas?.InvalidateSurface();
        }

        private async void OnStartChallengeClicked(object sender, EventArgs e)
        {
            if (_memorizedWords == null || _memorizedWords.Count == 0)
            {
                _memorizedWords = await TenWords.GetMemorizedWordsAsync();
            }

            if (_memorizedWords == null || _memorizedWords.Count == 0)
            {
                await Toast.Make("لم تقم بحفظ أي كلمات بعد 📚! أتمم اختبار الكلمات العشر أولاً.").Show();
                return;
            }

            if (!_isMultiplayer)
            {
                _gameWords = _memorizedWords
                    .Where(w => !string.IsNullOrWhiteSpace(w.EnglishWord) && !string.IsNullOrWhiteSpace(w.ArabicWord))
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(10)
                    .ToList();

                SetupView.IsVisible = false;
                GameView.IsVisible = true;
                UpdateGameModeUIVisibility();
                StartGame();
            }
            else
            {
                await LaunchFriendChallengeAsync();
            }
        }

        // ============================================================
        // ⚔️ مسار تحدي الصديق (Multiplayer Friend Duel Pipeline)
        // ============================================================

        private async Task LaunchFriendChallengeAsync()
        {
            try
            {
                SetLoadingState(true);

                if (!await Service.HasActiveInternetAsync(5))
                {
                    await Toast.Make("لا يوجد اتصال بالإنترنت. يرجى التحقق من الشبكة.").Show();
                    return;
                }

                long localChallenges = Preferences.Get("FriendsChallengeCount", 0L);
                long localCoins = Preferences.Get("Coins", 0L);

                if (localChallenges <= 0)
                {
                    await Toast.Make("لا توجد لديك محاولات لتحدي الأصدقاء ⚠️").Show();
                    return;
                }

                if (localCoins < DuelStake)
                {
                    await Toast.Make($"لا يوجد لديك عملات كافية للتحدي (المطلوب {DuelStake} كوينز) ⚠️").Show();
                    return;
                }

                if (_memorizedWords.Count < 5)
                {
                    await Toast.Make("يجب أن تحفظ 5 كلمات على الأقل لتتمكن من خوض هذا التحدي 📚").Show();
                    return;
                }

                AppShell? appShell = Application.Current?.MainPage as AppShell ?? Shell.Current as AppShell;
                if (appShell == null)
                {
                    await Toast.Make("تعذر الوصول لخدمات الاتصال.").Show();
                    return;
                }

                string currentUser = Preferences.Get("UserName", "");
                if (appShell.GameHub != null && (appShell.GameHub.HubConnection == null || appShell.GameHub.HubConnection.State != HubConnectionState.Connected))
                {
                    await appShell.GameHub.ConnectAsync(currentUser);
                }

                var friendsTask = Service.GetFriendsAsync(currentUser);
                var onlineUsersTask = appShell.GetOnlineUsersAsync();
                await Task.WhenAll(friendsTask, onlineUsersTask);

                string[] friendsArray = await friendsTask;
                List<string> myFriends = friendsArray != null ? [.. friendsArray] : new List<string>();
                List<string> onlineUsers = await onlineUsersTask ?? new List<string>();

                if (myFriends == null || myFriends.Count == 0)
                {
                    try
                    {
                        string cachedJson = Preferences.Get("Cached_Friends_List", "[]");
                        myFriends = JsonSerializer.Deserialize<List<string>>(cachedJson) ?? new List<string>();
                    }
                    catch { }
                }

                if (myFriends == null || myFriends.Count == 0)
                {
                    await Toast.Make("ليس لديك أصدقاء مضافون حتى الآن.").Show();
                    return;
                }

                var connectedFriends = myFriends.Where(f => onlineUsers.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList();
                if (connectedFriends.Count == 0)
                {
                    await Toast.Make("لا يوجد أي صديق متصل حالياً لتحديه 📭").Show();
                    return;
                }

                SetLoadingState(false);

                var selectPopup = new Popups.FriendSelectPopup("تحدي الطائر الصاعد", connectedFriends);
                var selectedResult = await this.ShowPopupAsync(selectPopup);

                if (selectedResult is string targetFriend && !string.IsNullOrEmpty(targetFriend))
                {
                    await InitiateDuelWithTargetFriendAsync(appShell, targetFriend);
                }
            }
            catch (Exception ex)
            {
                await Toast.Make($"حدث خطأ: {ex.Message}").Show();
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async Task InitiateDuelWithTargetFriendAsync(AppShell appShell, string targetFriend)
        {
            var waitingPopup = new Popups.WaitingChallengePopup(targetFriend, startInPreparingState: true);

            Action<string, bool, string> onChallengeResponded = (responder, isAccepted, category) =>
            {
                if (responder == targetFriend)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        waitingPopup.CloseWithResult(isAccepted);
                    });
                }
            };

            appShell.GameHub.OnChallengeResponseReceived += onChallengeResponded;

            string payloadJson = "";

            _ = Task.Run(async () =>
            {
                try
                {
                    var wordsTask = Service.FetchFriendMemorizedWordsCountAsync(targetFriend);
                    var syncTask = Service.SyncUserStatsWithServerAsync();
                    var friendTask = Service.GetUserByUserName(targetFriend);

                    await Task.WhenAll(wordsTask, syncTask, friendTask);

                    if (waitingPopup.IsCancelled) return;

                    long myCoins = Preferences.Get("Coins", 0L);
                    long myChallenges = Preferences.Get("FriendsChallengeCount", 0L);
                    if (myChallenges <= 0)
                    {
                        waitingPopup.CloseWithError("لا توجد لديك محاولات لتحدي الأصدقاء ⚠️");
                        return;
                    }
                    if (myCoins < DuelStake)
                    {
                        waitingPopup.CloseWithError($"لا يوجد لديك عملات كافية (مطلوب {DuelStake} كوينز) ⚠️");
                        return;
                    }

                    var friendResult = friendTask.Result;
                    if (!friendResult.Success || friendResult.Data == null)
                    {
                        waitingPopup.CloseWithError("تعذر التحقق من حالة الصديق. حاول لاحقاً.");
                        return;
                    }

                    if (friendResult.Data.FriendsChallengeCount <= 0)
                    {
                        waitingPopup.CloseWithError($"{targetFriend} ليس لديه محاولات متبقية لتلقي التحدي.");
                        return;
                    }

                    if (friendResult.Data.Coins < DuelStake)
                    {
                        waitingPopup.CloseWithError($"{targetFriend} لا يملك عملات كافية للمبارزة.");
                        return;
                    }

                    int friendWordsCount = wordsTask.Result;
                    int myWordsCount = _memorizedWords.Count;
                    int effectiveLimit = (friendWordsCount > 0) ? Math.Min(myWordsCount, friendWordsCount) : myWordsCount;

                    var sourceWords = _memorizedWords.Take(effectiveLimit).ToList();
                    _gameWords = sourceWords.OrderBy(_ => Guid.NewGuid()).Take(10).ToList();

                    if (_gameWords.Count < 3)
                    {
                        waitingPopup.CloseWithError("الكلمات المشتركة المحفوظة غير كافية لإجراء السباق.");
                        return;
                    }

                    _myBirdColorHex = Preferences.Get("MyBirdColor", "#38BDF8");

                    var allBirdColors = new List<string> { "#38BDF8", "#F43F5E", "#F59E0B", "#10B981" };
                    var availableForReceiver = allBirdColors
                        .Where(c => !c.Equals(_myBirdColorHex, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var payloadObj = new SkywardBirdPayload
                    {
                        SenderColor = _myBirdColorHex,
                        AvailableColors = availableForReceiver,
                        Words = _gameWords
                    };
                    payloadJson = JsonSerializer.Serialize(payloadObj);

                    await appShell.GameHub.SendChallengeAsync(targetFriend, "SkywardBirdMulti", payloadJson);

                    if (waitingPopup.IsCancelled) return;
                    waitingPopup.SetWaitingForFriendState(targetFriend);
                }
                catch (Exception ex)
                {
                    waitingPopup.CloseWithError($"حدث خطأ أثناء إعداد التحدي: {ex.Message}");
                }
            });

            var waitResult = await this.ShowPopupAsync(waitingPopup);
            appShell.GameHub.OnChallengeResponseReceived -= onChallengeResponded;

            if (waitResult is string err && err.StartsWith("Error:"))
            {
                await Toast.Make(err.Substring(6)).Show();
            }
            else if (waitResult is string status && status == "Accepted")
            {
                long originalChallenges = Preferences.Get("FriendsChallengeCount", 0L);
                long originalCoins = Preferences.Get("Coins", 0L);
                long newChallenges = originalChallenges > 0 ? originalChallenges - 1 : 0;
                long newCoins = originalCoins - DuelStake;

                Preferences.Set("FriendsChallengeCount", newChallenges);
                Preferences.Set("Coins", newCoins);

                long.TryParse(Preferences.Get("ID", "0")?.Trim('"', ' '), out long id);
                string uName = Preferences.Get("UserName", "");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (await Service.HasActiveInternetAsync(5))
                        {
                            await Service.UpdateFriendsChallengeCount(new User { ID = id, UserName = uName, FriendsChallengeCount = newChallenges });
                            await Service.UpdateCoins(new User { ID = id, UserName = uName, Coins = newCoins });
                        }
                    }
                    catch { }
                });

                string roomName = string.Compare(uName, targetFriend, StringComparison.Ordinal) < 0
                    ? $"room_{uName}_{targetFriend}"
                    : $"room_{targetFriend}_{uName}";

                _hubConnection = appShell.GameHub.HubConnection;
                _roomName = roomName;
                _opponentName = targetFriend;
                _isMultiplayer = true;

                SetupView.IsVisible = false;
                GameView.IsVisible = true;
                UpdateGameModeUIVisibility();

                MyNameLabel.Text = uName;
                OpponentNameLabel.Text = _opponentName;

                SetupMultiplayerCurrentConnection(payloadJson);

                if (_hubConnection != null)
                {
                    await _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, uName, "PAYLOAD:" + payloadJson);
                    await _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, uName, "COLOR:" + _myBirdColorHex);
                }

                PrepareMultiplayerSync();
            }
            else
            {
                await Toast.Make($"اعتذر {targetFriend} عن قبول التحدي.").Show();
            }
        }

        // ============================================================
        // 📡 معالجة أحداث وتزامن SignalR (Real-time Dual Bird Sync)
        // ============================================================

        private void SetupMultiplayerCurrentConnection(string payloadJson)
        {
            if (_hubConnection != null)
            {
                _hubConnection.On<string, string>("ReceiveDuelAnswer", (responder, msg) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (string.Equals(responder?.Trim(), _opponentName?.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            if (msg == "READY" || msg == "READY_ACK")
                            {
                                _isOpponentReady = true;
                                if (msg == "READY" && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                                {
                                    _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, "READY_ACK");
                                }

                                if (_isLocalReady && !_isCountingDown)
                                {
                                    StopReadyHandshakeTimer();
                                    LaunchSynchronizedCountdown();
                                }
                            }
                            else if (msg.StartsWith("COLOR:"))
                            {
                                string opColor = msg.Substring("COLOR:".Length).Trim();
                                SetOpponentBirdColor(opColor);
                            }
                            else if (msg.StartsWith("FLAP:"))
                            {
                                var parts = msg.Split(':');
                                if (parts.Length >= 3 && float.TryParse(parts[1], out float y) && float.TryParse(parts[2], out float velY))
                                {
                                    _opponentTargetBirdY = y;
                                    _opponentBirdVelY = velY;
                                    _opponentWingPhase += 1.5f;
                                    SpawnSparks(_opponentBirdX - 20, _opponentBirdY + 10, _opponentBodyPaint.Color);
                                }
                            }
                            else if (msg.StartsWith("POS:"))
                            {
                                var parts = msg.Split(':');
                                if (parts.Length >= 3 && float.TryParse(parts[1], out float y) && float.TryParse(parts[2], out float velY))
                                {
                                    _opponentTargetBirdY = y;
                                    _opponentBirdVelY = velY;
                                }
                            }
                            else if (msg.StartsWith("HIT:"))
                            {
                                var parts = msg.Split(':');
                                string hitWord = parts.Length >= 5 ? parts[1] : "";
                                int opScore = 0;
                                int opHearts = 3;

                                if (parts.Length >= 5)
                                {
                                    int.TryParse(parts[3], out opScore);
                                    int.TryParse(parts[4], out opHearts);
                                }
                                else if (parts.Length >= 4)
                                {
                                    int.TryParse(parts[2], out opScore);
                                    int.TryParse(parts[3], out opHearts);
                                }

                                _opponentScore = opScore;
                                _opponentHearts = opHearts;
                                OpponentScoreLabel.Text = $"{_opponentScore} ⭐";
                                UpdateOpponentHeartsUI();

                                // 💥 تفجير وحذف السحابة المستهدفة فوراً بلون طائر الخصم!
                                var targetCloud = (!string.IsNullOrEmpty(hitWord)
                                    ? _clouds.FirstOrDefault(c => c.EnglishWord.Equals(hitWord, StringComparison.OrdinalIgnoreCase))
                                    : null) ?? _clouds.FirstOrDefault(c => c.IsActive && c.IsTarget);

                                if (targetCloud != null)
                                {
                                    targetCloud.IsActive = false;
                                    SpawnSparks(targetCloud.X + targetCloud.Width / 2, targetCloud.Y + targetCloud.Height / 2, _opponentBodyPaint.Color);
                                    SpawnSparks(targetCloud.X + targetCloud.Width / 2, targetCloud.Y + targetCloud.Height / 2, SKColor.Parse("#F59E0B"));
                                }

                                SpawnSparks(_opponentBirdX, _opponentBirdY, _opponentBodyPaint.Color);

                                // إشعار لطيف للاعب بأن الصديق خطف السحابة
                                _ = Toast.Make($"⚡ خطف {_opponentName} السحابة أولاً!").Show();

                                // الانتقال للكلمة التالية معاً دون خصم قلوب للاعب المحلي
                                _combo = 1;
                                UpdateComboUI();
                                _collectedCount++;
                                int winGoal = Math.Min(10, _gameWords.Count);
                                if (_collectedCount >= winGoal)
                                {
                                    HandleRoundFinished();
                                }
                                else
                                {
                                    PickNextTargetWord();
                                }
                            }
                            else if (msg.StartsWith("WRONG:"))
                            {
                                var parts = msg.Split(':');
                                string wrongWord = parts.Length >= 4 ? parts[1] : "";
                                int opHearts = 3;
                                int opScore = 0;

                                if (parts.Length >= 4)
                                {
                                    int.TryParse(parts[2], out opHearts);
                                    int.TryParse(parts[3], out opScore);
                                }
                                else if (parts.Length >= 3)
                                {
                                    int.TryParse(parts[1], out opHearts);
                                    int.TryParse(parts[2], out opScore);
                                }

                                _opponentHearts = opHearts;
                                _opponentScore = opScore;
                                OpponentScoreLabel.Text = $"{_opponentScore} ⭐";
                                UpdateOpponentHeartsUI();

                                // تفجير وحذف السحابة الخاطئة التي اصطدم بها الصديق فوراً لكي لا تظل ظاهرة!
                                var wrongCloud = !string.IsNullOrEmpty(wrongWord)
                                    ? _clouds.FirstOrDefault(c => c.EnglishWord.Equals(wrongWord, StringComparison.OrdinalIgnoreCase))
                                    : null;

                                if (wrongCloud != null)
                                {
                                    wrongCloud.IsActive = false;
                                    SpawnSparks(wrongCloud.X + wrongCloud.Width / 2, wrongCloud.Y + wrongCloud.Height / 2, SKColor.Parse("#EF4444"));
                                }

                                SpawnSparks(_opponentBirdX, _opponentBirdY, SKColor.Parse("#EF4444"));
                            }
                            else if (msg.StartsWith("FINISHED:"))
                            {
                                _hasOpponentFinished = true;
                                var parts = msg.Split(':');
                                if (parts.Length >= 2 && int.TryParse(parts[1], out int finalScore))
                                {
                                    _opponentScore = finalScore;
                                    OpponentScoreLabel.Text = $"{_opponentScore} ⭐";
                                }

                                if (_hasLocalFinished)
                                {
                                    ShowFinalMultiplayerResult();
                                }
                            }
                        }
                        else if (msg.StartsWith("PAYLOAD:") && _gameWords.Count == 0)
                        {
                            string json = msg.Substring("PAYLOAD:".Length);
                            ParsePayload(json);
                        }
                    });
                });

                _hubConnection.On<string>("ReceiveDuelWithdrawal", (withdrawingUser) =>
                {
                    if (string.Equals(withdrawingUser?.Trim(), _opponentName?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (_isLeaving) return;
                            StopGame();
                            StopSyncTimer();
                            StopReadyHandshakeTimer();
                            if (_countdownTimer != null)
                            {
                                _countdownTimer.Stop();
                                _countdownTimer = null;
                            }
                            DuelWaitingOverlay.IsVisible = false;
                            DuelCountdownOverlay.IsVisible = false;

                            AwardWinnerCoins(DuelStake * 2);

                            WithdrawalIconLabel.Text = "⚠️";
                            WithdrawalTextLabel.Text = $"⚠️ اللاعب {withdrawingUser} انسحب من التحدي!";
                            WithdrawalSubTextLabel.Text = "🏆 تم احتساب الفوز لصالحك ونيل 10 كوينز كاملة!";
                            WithdrawalBanner.IsVisible = true;
                        });
                    }
                });

                // إرسال مصافحة اللون المختار إلى الخصم داخل الغرفة
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(300);
                        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                        {
                            await _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, "COLOR:" + _myBirdColorHex);
                        }
                    }
                    catch { }
                });
            }

            if (!string.IsNullOrEmpty(payloadJson) && _gameWords.Count == 0)
            {
                ParsePayload(payloadJson);
            }
        }

        private void ParsePayload(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                var complex = JsonSerializer.Deserialize<SkywardBirdPayload>(json);
                if (complex?.Words != null && complex.Words.Count > 0)
                {
                    _gameWords = complex.Words;
                    if (!string.IsNullOrWhiteSpace(complex.SenderColor))
                    {
                        SetOpponentBirdColor(complex.SenderColor);
                    }
                    if (_isMultiplayer)
                    {
                        if (GameView.IsVisible && !_isLocalReady && !_isCountingDown)
                        {
                            PrepareMultiplayerSync();
                        }
                    }
                    else
                    {
                        StartGame();
                    }
                    return;
                }
            }
            catch { }

            try
            {
                var simple = JsonSerializer.Deserialize<List<WordModel>>(json);
                if (simple != null && simple.Count > 0)
                {
                    _gameWords = simple;
                    if (_isMultiplayer)
                    {
                        if (GameView.IsVisible && !_isLocalReady && !_isCountingDown)
                        {
                            PrepareMultiplayerSync();
                        }
                    }
                    else
                    {
                        StartGame();
                    }
                }
            }
            catch { }
        }

        // ============================================================
        // 🔄 التزامن الثنائي والمصافحة والعد التنازلي المشترك
        // ============================================================

        private void PrepareMultiplayerSync()
        {
            if (!_isMultiplayer) return;
            if (_isCountingDown) return;

            _isLocalReady = true;
            _isWaitingForOpponentReady = true;
            _isPlaying = false;
            _isGameOver = false;

            // ضبط موقع تحليق الطائرين المبدئي
            _birdY = 650f;
            _birdVelY = 0f;
            _opponentBirdY = 650f;
            _opponentTargetBirdY = 650f;
            _opponentBirdVelY = 0f;
            _wingPhase = 0f;
            _opponentWingPhase = 0f;

            _clouds.Clear();
            _particles.Clear();

            // تهيئة النجوم في السماء لتكون الخلفية جاهزة
            if (_stars.Count == 0)
            {
                for (int i = 0; i < 40; i++)
                {
                    _stars.Add(new Star
                    {
                        X = Random.Shared.Next(0, 1080),
                        Y = Random.Shared.Next(0, 1920),
                        Size = (float)(Random.Shared.NextDouble() * 3 + 1),
                        Alpha = (float)(Random.Shared.NextDouble() * 0.7 + 0.3)
                    });
                }
            }

            ResultOverlay.IsVisible = false;
            WithdrawalBanner.IsVisible = false;
            DuelWaitingOverlay.IsVisible = true;
            DuelCountdownOverlay.IsVisible = false;

            // تشغيل محرك الرسم ليحلق الطائران بنعومة في المكان أثناء الانتظار
            if (_gameLoopTimer == null)
            {
                _gameLoopTimer = Dispatcher.CreateTimer();
                _gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16);
                _gameLoopTimer.Tick += OnGameLoopTick;
                _gameLoopTimer.Start();
            }

            if (_isOpponentReady)
            {
                if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, "READY_ACK");
                }
                StopReadyHandshakeTimer();
                LaunchSynchronizedCountdown();
            }
            else
            {
                StartReadyHandshakeTimer();
            }
        }

        private void StartReadyHandshakeTimer()
        {
            StopReadyHandshakeTimer();
            SendReadySignal();

            _readyHandshakeTimer = Dispatcher.CreateTimer();
            _readyHandshakeTimer.Interval = TimeSpan.FromMilliseconds(400);
            _readyHandshakeTimer.Tick += (s, e) =>
            {
                if (_isOpponentReady || _isCountingDown || !_isMultiplayer)
                {
                    StopReadyHandshakeTimer();
                    return;
                }
                SendReadySignal();
            };
            _readyHandshakeTimer.Start();
        }

        private void SendReadySignal()
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(_roomName))
            {
                _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, "READY");
            }
        }

        private void StopReadyHandshakeTimer()
        {
            if (_readyHandshakeTimer != null)
            {
                _readyHandshakeTimer.Stop();
                _readyHandshakeTimer = null;
            }
        }

        private void LaunchSynchronizedCountdown()
        {
            if (_isCountingDown) return;
            _isCountingDown = true;
            _isWaitingForOpponentReady = false;

            StopReadyHandshakeTimer();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DuelWaitingOverlay.IsVisible = false;
                DuelCountdownOverlay.IsVisible = true;

                _countdownSeconds = 3;
                CountdownNumberLabel.Text = "3";
                CountdownNumberLabel.TextColor = Color.FromArgb("#F59E0B");
                CountdownNumberLabel.Scale = 1.3;
                _ = CountdownNumberLabel.ScaleTo(1.0, 300, Easing.CubicOut);
                CountdownSubLabel.Text = "استعد للطيران! 🚀";

                try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                _countdownTimer?.Stop();
                _countdownTimer = Dispatcher.CreateTimer();
                _countdownTimer.Interval = TimeSpan.FromSeconds(1);
                _countdownTimer.Tick += (s, e) =>
                {
                    _countdownSeconds--;
                    if (_countdownSeconds > 0)
                    {
                        CountdownNumberLabel.Text = _countdownSeconds.ToString();
                        CountdownNumberLabel.Scale = 1.3;
                        _ = CountdownNumberLabel.ScaleTo(1.0, 300, Easing.CubicOut);
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
                    }
                    else if (_countdownSeconds == 0)
                    {
                        CountdownNumberLabel.Text = "انطلق! 🚀";
                        CountdownNumberLabel.TextColor = Color.FromArgb("#10B981");
                        CountdownNumberLabel.Scale = 1.4;
                        _ = CountdownNumberLabel.ScaleTo(1.0, 300, Easing.BounceOut);
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
                    }
                    else
                    {
                        _countdownTimer?.Stop();
                        _countdownTimer = null;
                        DuelCountdownOverlay.IsVisible = false;
                        _isCountingDown = false;
                        StartGame();
                    }
                };
                _countdownTimer.Start();
            });
        }

        // ============================================================
        // 🎮 دورة اللعبة والفيزياء الأفقية (Horizontal Clouds & Two Birds)
        // ============================================================

        private void StartGame()
        {
            _isWaitingForOpponentReady = false;
            _isCountingDown = false;
            StopReadyHandshakeTimer();
            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer = null;
            }

            StopGame();
            ResultOverlay.IsVisible = false;
            WithdrawalBanner.IsVisible = false;
            DuelWaitingOverlay.IsVisible = false;
            DuelCountdownOverlay.IsVisible = false;

            _isGameOver = false;
            _hasLocalFinished = false;
            _hearts = 3;
            _opponentHearts = 3;
            _combo = 1;
            _sessionScore = 0;
            _opponentScore = 0;
            _collectedCount = 0;
            _currentWordIndex = 0;

            _birdY = 650f;
            _birdVelY = 0;
            _opponentBirdY = 650f;
            _opponentTargetBirdY = 650f;
            _opponentBirdVelY = 0;

            _clouds.Clear();
            _particles.Clear();

            UpdateHeartsUI();
            UpdateOpponentHeartsUI();
            UpdateComboUI();
            SessionScoreLabel.Text = "0";
            if (_isMultiplayer)
            {
                LocalScoreLabel.Text = "0 ⭐";
                OpponentScoreLabel.Text = "0 ⭐";
                DuelProgressLabel.Text = $"1 / {Math.Min(10, _gameWords.Count)}";
            }

            // إعداد النجوم في الخلفية
            _stars.Clear();
            for (int i = 0; i < 40; i++)
            {
                _stars.Add(new Star
                {
                    X = Random.Shared.Next(0, 1080),
                    Y = Random.Shared.Next(0, 1920),
                    Size = (float)(Random.Shared.NextDouble() * 3 + 1),
                    Alpha = (float)(Random.Shared.NextDouble() * 0.7 + 0.3)
                });
            }

            PickNextTargetWord();

            _isPlaying = true;
            _gameLoopTimer = Dispatcher.CreateTimer();
            _gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _gameLoopTimer.Tick += OnGameLoopTick;
            _gameLoopTimer.Start();

            if (_isMultiplayer)
            {
                StartSyncTimer();
            }
        }

        private void StopGame()
        {
            _isPlaying = false;
            _isCountingDown = false;
            _isWaitingForOpponentReady = false;
            _isLocalReady = false;
            StopReadyHandshakeTimer();
            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer = null;
            }
            if (_gameLoopTimer != null)
            {
                _gameLoopTimer.Stop();
                _gameLoopTimer = null;
            }
        }

        private void StartSyncTimer()
        {
            StopSyncTimer();
            _syncTimer = Dispatcher.CreateTimer();
            _syncTimer.Interval = TimeSpan.FromMilliseconds(120);
            _syncTimer.Tick += (s, e) =>
            {
                if (_isMultiplayer && _isPlaying && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, $"POS:{_birdY:F1}:{_birdVelY:F1}");
                }
            };
            _syncTimer.Start();
        }

        private void StopSyncTimer()
        {
            if (_syncTimer != null)
            {
                _syncTimer.Stop();
                _syncTimer = null;
            }
        }

        private void PickNextTargetWord()
        {
            if (_gameWords.Count == 0) return;

            _currentTargetWord = _gameWords[_currentWordIndex % _gameWords.Count];
            _currentWordIndex++;

            TargetArabicWordLabel.Text = _currentTargetWord.ArabicWord;

            if (_isMultiplayer)
            {
                DuelProgressLabel.Text = $"{_collectedCount + 1} / {Math.Min(10, _gameWords.Count)}";
            }

            SpawnCloudsForTarget(_currentTargetWord);
        }

        private static int GetDeterministicHashCode(string str)
        {
            if (string.IsNullOrEmpty(str)) return 0;
            unchecked
            {
                int hash = 17;
                foreach (char c in str)
                {
                    hash = hash * 31 + c;
                }
                return Math.Abs(hash);
            }
        }

        // توليد السحب أفقياً من اليمين لتتحرك نحو اليسار باتجاه الطائر بتزامن حتمي دقيق ومطابق على كلا الهاتفين
        private void SpawnCloudsForTarget(WordModel target)
        {
            _clouds.Clear();

            int seed = GetDeterministicHashCode(target.EnglishWord ?? "") + (_currentWordIndex * 7919);
            var rnd = new Random(seed);

            // استخراج وتوزيع كلمات التشويش بحتمية تامة وتطابق مطلق بين جهازي الصديقين
            var availableDistractors = _gameWords
                .Where(w => !string.Equals(w.EnglishWord, target.EnglishWord, StringComparison.OrdinalIgnoreCase))
                .Select(w => w.EnglishWord)
                .OrderBy(w => w, StringComparer.Ordinal)
                .ToList();

            for (int i = availableDistractors.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (availableDistractors[i], availableDistractors[j]) = (availableDistractors[j], availableDistractors[i]);
            }
            var distractors = availableDistractors.Take(2).ToList();

            var wordsForClouds = new List<(string text, bool isTarget)>
            {
                (target.EnglishWord, true),
                (distractors.Count > 0 ? distractors[0] : "Cloud", false),
                (distractors.Count > 1 ? distractors[1] : "Sky", false)
            };

            for (int i = wordsForClouds.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (wordsForClouds[i], wordsForClouds[j]) = (wordsForClouds[j], wordsForClouds[i]);
            }

            float startX = 850f;
            float spacingX = 420f;
            float[] possibleY = { 480f, 850f, 1220f };

            for (int i = 0; i < wordsForClouds.Count; i++)
            {
                float cloudW = Math.Max(260f, wordsForClouds[i].text.Length * 24f + 60f);
                float cloudH = 110f;

                _clouds.Add(new WordCloud
                {
                    X = startX + (i * spacingX),
                    Y = possibleY[i % possibleY.Length],
                    Width = cloudW,
                    Height = cloudH,
                    EnglishWord = wordsForClouds[i].text,
                    IsTarget = wordsForClouds[i].isTarget,
                    IsActive = true
                });
            }
        }

        private void OnGameLoopTick(object? sender, EventArgs e)
        {
            if (!_isPlaying)
            {
                if (_isWaitingForOpponentReady || _isCountingDown)
                {
                    // 🕊️ حركة رفرفة هادئة وتحليق في المكان أثناء انتظار اتصال الصديق أو العد التنازلي
                    _wingPhase += 0.22f;
                    _opponentWingPhase += 0.22f;
                    _birdY = 650f + (float)Math.Sin(_wingPhase) * 12f;
                    _opponentBirdY = 650f + (float)Math.Sin(_opponentWingPhase + 1.2f) * 12f;

                    for (int i = _particles.Count - 1; i >= 0; i--)
                    {
                        var p = _particles[i];
                        p.X += p.VelX;
                        p.Y += p.VelY;
                        p.Alpha -= 0.035f;
                        if (p.Alpha <= 0)
                        {
                            _particles.RemoveAt(i);
                        }
                    }

                    GameCanvas?.InvalidateSurface();
                }
                return;
            }

            // 1. فيزياء الطائر المحلي (Player 1)
            _birdVelY += Gravity;
            _birdY += _birdVelY;
            _wingPhase += 0.25f;

            // حدود السقف والأرضية مع ارتداد ناعم
            if (_birdY < 220)
            {
                _birdY = 220;
                _birdVelY = 0;
            }
            else if (_birdY > 1750)
            {
                _birdY = 1750;
                _birdVelY = FlapStrength * 0.7f;
            }

            // 2. فيزياء طائر الخصم (Player 2)
            if (_isMultiplayer)
            {
                _opponentBirdY += (_opponentTargetBirdY - _opponentBirdY) * 0.22f;
                _opponentBirdVelY += Gravity * 0.5f;
                _opponentWingPhase += 0.25f;
            }

            // 3. حركة السحب أفقياً من اليمين إلى اليسار
            float speedX = 4.8f + (_combo * 0.2f);
            bool targetPassedUncollected = false;

            for (int i = 0; i < _clouds.Count; i++)
            {
                var cloud = _clouds[i];
                if (!cloud.IsActive) continue;

                cloud.X -= speedX;

                // فحص الاصطدام مع الطائر المحلي
                if (CheckCollision(_birdX, _birdY, 55, cloud))
                {
                    cloud.IsActive = false;
                    if (cloud.IsTarget)
                    {
                        HandleTargetCloudCollected(cloud);
                        break;
                    }
                    else
                    {
                        HandleWrongCloudHit(cloud);
                        break;
                    }
                }

                // فحص الاصطدام لطائر الصديق أيضاً لضمان تفجير السحابة فورياً عند ملامسته لها
                if (_isMultiplayer && CheckCollision(_opponentBirdX, _opponentBirdY, 55, cloud))
                {
                    cloud.IsActive = false;
                    if (cloud.IsTarget)
                    {
                        SpawnSparks(cloud.X + cloud.Width / 2, cloud.Y + cloud.Height / 2, _opponentBodyPaint.Color);
                        SpawnSparks(cloud.X + cloud.Width / 2, cloud.Y + cloud.Height / 2, SKColor.Parse("#F59E0B"));
                    }
                    else
                    {
                        SpawnSparks(cloud.X + cloud.Width / 2, cloud.Y + cloud.Height / 2, SKColor.Parse("#EF4444"));
                    }
                    break;
                }

                if (cloud.IsTarget && cloud.X + cloud.Width < -50)
                {
                    targetPassedUncollected = true;
                }
            }

            if (targetPassedUncollected)
            {
                if (_currentTargetWord != null)
                    SpawnCloudsForTarget(_currentTargetWord);
            }

            // 4. تحديث الجزيئات
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.X += p.VelX;
                p.Y += p.VelY;
                p.Alpha -= 0.035f;
                if (p.Alpha <= 0)
                {
                    _particles.RemoveAt(i);
                }
            }

            GameCanvas.InvalidateSurface();
        }

        private static bool CheckCollision(float birdX, float birdY, float birdRadius, WordCloud cloud)
        {
            float closestX = Math.Clamp(birdX, cloud.X, cloud.X + cloud.Width);
            float closestY = Math.Clamp(birdY, cloud.Y, cloud.Y + cloud.Height);
            float dx = birdX - closestX;
            float dy = birdY - closestY;
            return (dx * dx + dy * dy) < (birdRadius * birdRadius);
        }

        private void FlapBird()
        {
            if (!_isPlaying || _isGameOver || _isCountingDown || _isWaitingForOpponentReady) return;

            _birdVelY = FlapStrength;
            _wingPhase += 1.4f;

            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

            // أثر نفاث خلف الطائر المحلي
            SpawnSparks(_birdX - 25, _birdY + 8, _birdBodyPaint.Color);

            if (_isMultiplayer && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, $"FLAP:{_birdY:F1}:{_birdVelY:F1}");
            }
        }

        private void HandleTargetCloudCollected(WordCloud cloud)
        {
            _ = Sounds.PlayAsync(Sounds.Correct());
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

            _ = TextToSpeech.Default.SpeakAsync(cloud.EnglishWord);

            SpawnSparks(cloud.X + cloud.Width / 2, cloud.Y + cloud.Height / 2, _birdBodyPaint.Color);
            SpawnSparks(cloud.X + cloud.Width / 2, cloud.Y + cloud.Height / 2, SKColor.Parse("#F59E0B"));

            _birdVelY = FlapStrength * 0.8f;
            _combo++;
            UpdateComboUI();

            _collectedCount++;
            int pointsEarned = 10 * _combo;
            _sessionScore += pointsEarned;
            SessionScoreLabel.Text = _sessionScore.ToString();

            if (_isMultiplayer)
            {
                LocalScoreLabel.Text = $"{_sessionScore} ⭐";
                if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, $"HIT:{cloud.EnglishWord}:{_currentWordIndex}:{_sessionScore}:{_hearts}");
                }
            }

            int winGoal = Math.Min(10, _gameWords.Count);
            if (_collectedCount >= winGoal)
            {
                HandleRoundFinished();
            }
            else
            {
                PickNextTargetWord();
            }
        }

        private void HandleWrongCloudHit(WordCloud cloud)
        {
            _ = Sounds.PlayAsync(Sounds.Wrong());
            SpawnSparks(cloud.X + cloud.Width / 2, cloud.Y + cloud.Height / 2, SKColor.Parse("#EF4444"));

            _combo = 1;
            UpdateComboUI();
            _hearts--;
            UpdateHeartsUI();

            if (_isMultiplayer && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, $"WRONG:{cloud.EnglishWord}:{_hearts}:{_sessionScore}");
            }

            if (_hearts <= 0)
            {
                HandleGameOver();
            }
        }

        private void HandleGameOver()
        {
            _isPlaying = false;
            _isGameOver = true;
            _hasLocalFinished = true;

            if (_isMultiplayer)
            {
                if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, $"FINISHED:{_sessionScore}");
                }

                if (_hasOpponentFinished)
                {
                    ShowFinalMultiplayerResult();
                }
                else
                {
                    WithdrawalIconLabel.Text = "⏳";
                    WithdrawalTextLabel.Text = "سقط طائرك! بانتظار إكمال الصديق للسباق...";
                    WithdrawalSubTextLabel.Text = "سيتم تحديد الفائز وحسم الكوينز فور انتهاء الصديق ⏱️";
                    WithdrawalExitButton.IsVisible = false;
                    WithdrawalBanner.IsVisible = true;
                }
            }
            else
            {
                ResultIconLabel.Text = "💔";
                ResultTitleLabel.Text = "سقط الطائر!";
                ResultDescLabel.Text = "نفدت قلوب المحاولات. تفادَ السحب المشتتة وحاول مجدداً!";
                ResultWordsCountLabel.Text = $"{_collectedCount} ☁️";
                ResultScoreLabel.Text = $"{_sessionScore} ⭐";
                ResultOverlay.IsVisible = true;
            }
        }

        private void HandleRoundFinished()
        {
            _isPlaying = false;
            _hasLocalFinished = true;

            if (_isMultiplayer)
            {
                if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, $"FINISHED:{_sessionScore}");
                }

                if (_hasOpponentFinished)
                {
                    ShowFinalMultiplayerResult();
                }
                else
                {
                    WithdrawalIconLabel.Text = "⏳";
                    WithdrawalTextLabel.Text = "أنهيت جميع السحب! بانتظار إكمال الصديق للسباق...";
                    WithdrawalSubTextLabel.Text = "سيتم احتساب النتيجة وتوزيع الكوينز فور إكمال الصديق ⏱️";
                    WithdrawalExitButton.IsVisible = false;
                    WithdrawalBanner.IsVisible = true;
                }
            }
            else
            {
                ResultIconLabel.Text = "👑";
                ResultTitleLabel.Text = "طيران أسطوري! 🎉";
                ResultDescLabel.Text = "تمكن الطائر من جمع جميع السحب الصحيحة ببراعة تامة.";
                ResultWordsCountLabel.Text = $"{_collectedCount} ☁️";
                ResultScoreLabel.Text = $"+{_sessionScore} ⭐";
                ResultOverlay.IsVisible = true;
            }
        }

        // ============================================================
        // 💰 حسم وتوزيع الكوينز والنتيجة النهائية (Duel Settlement)
        // ============================================================

        private void ShowFinalMultiplayerResult()
        {
            WithdrawalBanner.IsVisible = false;
            ResultWordsCountLabel.Text = $"{_collectedCount} ☁️";
            PlayAgainButton.IsVisible = false;

            if (_sessionScore > _opponentScore)
            {
                AwardWinnerCoins(DuelStake * 2);

                ResultIconLabel.Text = "🏆";
                ResultTitleLabel.Text = "فوز أسطوري بالسباق! 👑";
                ResultTitleLabel.TextColor = Color.FromArgb("#FBBF24");
                ResultDescLabel.Text = $"تفوق طائرك على {_opponentName} وحصدت 10 كوينز ذهبي كاملة!";
                ResultStat1Title.Text = "نقاطك";
                ResultWordsCountLabel.Text = $"{_sessionScore} ⭐";
                ResultStat2Title.Text = "الجائزة";
                ResultScoreLabel.Text = "+10 💰";
                ResultScoreLabel.TextColor = Color.FromArgb("#FBBF24");
            }
            else if (_sessionScore < _opponentScore)
            {
                ResultIconLabel.Text = "💔";
                ResultTitleLabel.Text = "هاردلك! فاز الصديق 🦅";
                ResultTitleLabel.TextColor = Color.FromArgb("#F43F5E");
                ResultDescLabel.Text = $"سجل {_opponentName} نقاطاً أعلى. حظاً أوفر في التحدي القادم!";
                ResultStat1Title.Text = "نقاطك";
                ResultWordsCountLabel.Text = $"{_sessionScore} ⭐";
                ResultStat2Title.Text = "نقاط الخصم";
                ResultScoreLabel.Text = $"{_opponentScore} ⭐";
                ResultScoreLabel.TextColor = Color.FromArgb("#F43F5E");
            }
            else
            {
                AwardWinnerCoins(DuelStake);

                ResultIconLabel.Text = "🤝";
                ResultTitleLabel.Text = "تعادل متكافئ! ⚖️";
                ResultTitleLabel.TextColor = Color.FromArgb("#38BDF8");
                ResultDescLabel.Text = "تساوت نقاط الطائرين وتم استرداد 5 كوينز ذهبي لرصيدك.";
                ResultStat1Title.Text = "نقاطك";
                ResultWordsCountLabel.Text = $"{_sessionScore} ⭐";
                ResultStat2Title.Text = "استرداد";
                ResultScoreLabel.Text = "+5 💰";
                ResultScoreLabel.TextColor = Color.FromArgb("#38BDF8");
            }

            ResultOverlay.IsVisible = true;
        }

        private void AwardWinnerCoins(int reward)
        {
            try
            {
                long coins = Preferences.Get("Coins", 0L) + reward;
                Preferences.Set("Coins", coins);

                long.TryParse(Preferences.Get("ID", "0")?.Trim('"', ' '), out long id);
                string userName = Preferences.Get("UserName", "");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (await Service.HasActiveInternetAsync(5))
                        {
                            var res = await Service.UpdateCoins(new User { ID = id, UserName = userName, Coins = coins });
                            if (res != "1")
                            {
                                Service.AddPendingCoinsUpdate(id, coins, userName);
                            }
                        }
                        else
                        {
                            Service.AddPendingCoinsUpdate(id, coins, userName);
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        // ============================================================
        // 🎨 رسم SkiaSharp لكلا الطائرين والسحب (Rendering Canvas)
        // ============================================================

        private void OnCanvasPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColor.Parse("#080D1A"));

            // 1. النجوم
            foreach (var star in _stars)
            {
                _starPaint.Color = new SKColor(255, 255, 255, (byte)(star.Alpha * 255));
                canvas.DrawCircle(star.X, star.Y, star.Size, _starPaint);
            }

            // 2. السحب العائمة أفقياً
            foreach (var cloud in _clouds)
            {
                if (!cloud.IsActive) continue;

                var roundRect = new SKRoundRect(new SKRect(cloud.X, cloud.Y, cloud.X + cloud.Width, cloud.Y + cloud.Height), 34, 34);
                canvas.DrawRoundRect(roundRect, _cloudFillPaint);
                canvas.DrawRoundRect(roundRect, _cloudBorderPaint);

                float textY = cloud.Y + (cloud.Height / 2f) + (_cloudTextPaint.TextSize / 3f);
                canvas.DrawText(cloud.EnglishWord, cloud.X + (cloud.Width / 2f), textY, _cloudTextPaint);
            }

            // 3. الجزيئات
            foreach (var p in _particles)
            {
                _particlePaint.Color = new SKColor(p.Color.Red, p.Color.Green, p.Color.Blue, (byte)(p.Alpha * 255));
                canvas.DrawCircle(p.X, p.Y, p.Radius, _particlePaint);
            }

            // 4. رسم طائر الصديق (Player 2) في الخلفية إذا كانت الجولة ثنائية
            if (_isMultiplayer)
            {
                DrawBird(canvas, _opponentBirdX, _opponentBirdY, _opponentWingPhase, _opponentBirdVelY, _opponentBodyPaint, _opponentWingPaint, isLocal: false);
            }

            // 5. رسم الطائر المحلي (Player 1) في المقدمة
            DrawBird(canvas, _birdX, _birdY, _wingPhase, _birdVelY, _birdBodyPaint, _birdWingPaint, isLocal: true);
        }

        private void DrawBird(SKCanvas canvas, float x, float y, float wingPhase, float velY, SKPaint bodyPaint, SKPaint wingPaint, bool isLocal = false)
        {
            canvas.Save();
            canvas.Translate(x, y);

            float pitchAngle = Math.Clamp(velY * 2.5f, -25f, 35f);
            canvas.RotateDegrees(pitchAngle);

            // هالة ضوئية لطيفة تميز طائر اللاعب المحلي في المبارزة
            if (isLocal && _isMultiplayer)
            {
                using var ringPaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    Color = SKColor.Parse("#38BDF8").WithAlpha(120),
                    StrokeWidth = 3f,
                    IsAntialias = true
                };
                canvas.DrawOval(0, 0, 54, 42, ringPaint);
            }

            // جسد الطائر
            canvas.DrawOval(0, 0, 48, 36, bodyPaint);

            // المنقار
            using (var beakPath = new SKPath())
            {
                beakPath.MoveTo(40, -6);
                beakPath.LineTo(62, 2);
                beakPath.LineTo(40, 10);
                beakPath.Close();
                canvas.DrawPath(beakPath, _birdBeakPaint);
            }

            // العين والبؤبؤ
            canvas.DrawCircle(24, -10, 10, _birdEyePaint);
            canvas.DrawCircle(28, -10, 5, _birdPupilPaint);

            // الجناح المتحرك
            float wingOffset = (float)Math.Sin(wingPhase) * 16f;
            using (var wingPath = new SKPath())
            {
                wingPath.MoveTo(-10, 0);
                wingPath.LineTo(-35, -20 + wingOffset);
                wingPath.LineTo(0, 14);
                wingPath.Close();
                canvas.DrawPath(wingPath, wingPaint);
            }

            canvas.Restore();
        }

        private void SpawnSparks(float x, float y, SKColor color)
        {
            for (int i = 0; i < 14; i++)
            {
                double angle = Random.Shared.NextDouble() * Math.PI * 2;
                float speed = (float)(Random.Shared.NextDouble() * 7 + 3);

                _particles.Add(new SparkParticle
                {
                    X = x,
                    Y = y,
                    VelX = (float)(Math.Cos(angle) * speed),
                    VelY = (float)(Math.Sin(angle) * speed),
                    Alpha = 1f,
                    Color = color,
                    Radius = (float)(Random.Shared.NextDouble() * 5 + 3)
                });
            }
        }

        // ============================================================
        // 👆 التفاعل والتحكم (Touch & Controls)
        // ============================================================

        private void OnCanvasTouch(object sender, SKTouchEventArgs e)
        {
            if (e.ActionType == SKTouchAction.Pressed)
            {
                FlapBird();
                e.Handled = true;
            }
        }

        private void OnFlapButtonClicked(object sender, EventArgs e)
        {
            FlapBird();
        }

        private void UpdateHeartsUI()
        {
            Heart1Label.Opacity = _hearts >= 1 ? 1.0 : 0.2;
            Heart2Label.Opacity = _hearts >= 2 ? 1.0 : 0.2;
            Heart3Label.Opacity = _hearts >= 3 ? 1.0 : 0.2;

            if (_isMultiplayer)
            {
                LocalHeartsLabel.Text = string.Concat(Enumerable.Repeat("❤️", Math.Max(0, _hearts)));
            }
        }

        private void UpdateOpponentHeartsUI()
        {
            if (_isMultiplayer)
            {
                OpponentHeartsLabel.Text = string.Concat(Enumerable.Repeat("❤️", Math.Max(0, _opponentHearts)));
            }
        }

        private void UpdateComboUI()
        {
            ComboLabel.Text = $"x{_combo}";
        }

        private void SetLoadingState(bool isLoading)
        {
            StartLoadingIndicator.IsRunning = isLoading;
            StartLoadingIndicator.IsVisible = isLoading;
            StartGameButton.IsEnabled = !isLoading;
            StartGameButton.Opacity = isLoading ? 0.6 : 1.0;
        }

        private void OnPlayAgainClicked(object sender, EventArgs e)
        {
            ResultOverlay.IsVisible = false;
            StartGame();
        }

        private async void OnExitClicked(object sender, EventArgs e)
        {
            await ConfirmExitAsync();
        }

        private async void OnExitConfirmedClicked(object sender, EventArgs e)
        {
            await ConfirmExitAsync();
        }

        private async Task ConfirmExitAsync()
        {
            if (_isLeaving) return;

            if (WithdrawalBanner.IsVisible || ResultOverlay.IsVisible)
            {
                _isLeaving = true;
                StopGame();
                StopSyncTimer();
                CleanUpMultiplayer();
                await Navigation.PopModalAsync();
                return;
            }

            if (DuelWaitingOverlay.IsVisible || _isWaitingForOpponentReady)
            {
                _isLeaving = true;
                if (_isMultiplayer && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    try
                    {
                        await _hubConnection.InvokeAsync("SendDuelWithdrawal", _roomName, _currentUserName);
                    }
                    catch { }
                }
                StopGame();
                StopSyncTimer();
                CleanUpMultiplayer();
                await Navigation.PopModalAsync();
                return;
            }

            if (_isPlaying)
            {
                string title = _isMultiplayer ? "تأكيد الانسحاب ⚠️" : "تأكيد الخروج";
                string msg = _isMultiplayer
                    ? "هل أنت متأكد من الانسحاب؟ سيتم احتساب فوز للخصم وخسارة رهان الكوينز."
                    : "هل تريد الخروج من الجولة الحالية؟";
                string confirmBtn = _isMultiplayer ? "نعم، انسحب" : "نعم";

                bool confirm = await DisplayAlert(title, msg, confirmBtn, "إلغاء");
                if (!confirm) return;

                if (_isMultiplayer && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    try
                    {
                        await _hubConnection.InvokeAsync("SendDuelWithdrawal", _roomName, _currentUserName);
                    }
                    catch { }
                }
            }

            _isLeaving = true;
            StopGame();
            StopSyncTimer();
            CleanUpMultiplayer();
            await Navigation.PopModalAsync();
        }

        private void CleanUpMultiplayer()
        {
            if (_hubConnection != null)
            {
                try
                {
                    _hubConnection.Remove("ReceiveDuelAnswer");
                    _hubConnection.Remove("ReceiveDuelWithdrawal");
                }
                catch { }
            }
        }

        protected override bool OnBackButtonPressed()
        {
            _ = ConfirmExitAsync();
            return true;
        }
    }
}
