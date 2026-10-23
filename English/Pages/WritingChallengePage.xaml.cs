using English.Popups;
using CommunityToolkit.Maui.Core;
using English.Helpers;
namespace English.Pages;

public partial class WritingChallengePage : ContentPage
{
    private class WritingQuestionItem
    {
        public string ArabicWord { get; set; } = string.Empty;
        public string EnglishWord { get; set; } = string.Empty;
    }

    private class WritingGamePayload
    {
        public List<WritingQuestionItem> Questions { get; set; } = [];
        public int TimePerQuestion { get; set; }
    }

    private List<WritingQuestionItem> _questions = new();
    private int _currentIndex = 0;
    private int _score = 0;
    private int _opponentScore = 0;
    private System.Timers.Timer? _timer;
    private System.Timers.Timer? _nextQuestionTimer;
    private int _nextQuestionTimeLeft = 5;
    private int _timePerQuestion = 10;
    private int _timeLeft = 10;
    private bool _answered = false;
    private bool _isLeaving = false;
    private bool _isTransitioning = false;
    private bool _isMultiplayer = false;
    private bool _hasLocalFinished = false;
    private bool _hasOpponentFinished = false;
    private List<WordModel> _memorizedWords = new();

    private HubConnection? _hubConnection;
    private string _roomName = "";
    private string _opponentName = "";

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.AnimatePageInAsync(PageAnimationOptions.ContainerOnly);
    }

    // For Single Player
    public WritingChallengePage()
    {
        InitializeComponent();
        LoadSetupData();
        UpdateGameModeUI();
    }

    // For Multiplayer (Receiver / Accepted sender)
    public WritingChallengePage(HubConnection hubConnection, string roomName, string opponentName, string payloadJson)
    {
        InitializeComponent();

        _hubConnection = hubConnection;
        _roomName = roomName;
        _opponentName = opponentName;
        _isMultiplayer = true;

        SetupView.IsVisible = false;
        GameView.IsVisible = true;
        OpponentScoreContainer.IsVisible = true;

        MyNameLabel.Text = Preferences.Get("UserName", "أنا");
        OpponentNameLabel.Text = _opponentName;

        SetupMultiplayerCurrentConnection(payloadJson);
    }

    private void SetupMultiplayerCurrentConnection(string payloadJson)
    {
        if (_hubConnection != null)
        {
            _hubConnection.On<string, string>("ReceiveDuelAnswer", (responder, msg) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (responder == _opponentName)
                    {
                        if (msg.StartsWith("SCORE:"))
                        {
                            if (int.TryParse(msg.Replace("SCORE:", ""), out int opScore))
                            {
                                _opponentScore = opScore;
                                OpponentScoreLabel.Text = _opponentScore.ToString();
                            }
                        }
                        else if (msg.StartsWith("FINISHED:"))
                        {
                            _hasOpponentFinished = true;
                            if (int.TryParse(msg.Replace("FINISHED:", ""), out int opScore))
                            {
                                _opponentScore = opScore;
                                OpponentScoreLabel.Text = _opponentScore.ToString();
                            }

                            if (_hasLocalFinished)
                            {
                                ShowFinalMultiplayerResult();
                            }
                        }
                    }
                    else if (msg.StartsWith("PAYLOAD:") && _questions.Count == 0)
                    {
                        string json = msg.Replace("PAYLOAD:", "");
                        try
                        {
                            var payload = JsonSerializer.Deserialize<WritingGamePayload>(json);
                            if (payload != null)
                            {
                                _questions = payload.Questions;
                                _timePerQuestion = payload.TimePerQuestion;
                                LoadQuestion();
                            }
                        }
                        catch { }
                    }
                });
            });

            _hubConnection.On<string>("ReceiveDuelWithdrawal", (withdrawingUser) =>
            {
                if (withdrawingUser == _opponentName)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (_isLeaving) return;
                        StopTimer();
                        StopNextQuestionTimer();

                        AwardWinnerCoins(10);

                        if (WithdrawalIconLabel != null) WithdrawalIconLabel.Text = "⚠️";
                        if (WithdrawalTextLabel != null)
                        {
                            WithdrawalTextLabel.Text = $"⚠️ اللاعب {withdrawingUser} انسحب من التحدي!";
                            WithdrawalTextLabel.TextColor = Color.FromArgb("#FCA5A5");
                        }
                        if (WithdrawalSubTextLabel != null)
                        {
                            WithdrawalSubTextLabel.Text = "🏆 تم إنهاء التحدي واحتساب الفوز لصالحك تلقائياً!";
                            WithdrawalSubTextLabel.TextColor = Color.FromArgb("#FDE68A");
                            WithdrawalSubTextLabel.IsVisible = true;
                        }
                        if (WithdrawalBanner != null)
                        {
                            WithdrawalBanner.BackgroundColor = Color.FromArgb("#2D1517");
                            WithdrawalBanner.Stroke = Color.FromArgb("#EF4444");
                            WithdrawalBanner.IsVisible = true;
                        }

                        AnswerEntry.IsEnabled = false;
                        ActionButton.Text = "العودة للقائمة الرئيسية ➔";
                        ActionButton.IsVisible = true;
                    });
                }
            });
        }

        if (!string.IsNullOrEmpty(payloadJson))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<WritingGamePayload>(payloadJson);
                if (payload != null)
                {
                    _questions = payload.Questions;
                    _timePerQuestion = payload.TimePerQuestion;
                    LoadQuestion();
                }
            }
            catch { }
        }
    }

    private void LoadSetupData()
    {
        _memorizedWords = TenWords.GetMemorizedWords();
        if (_memorizedWords.Count == 0)
        {
            _memorizedWords = TenWords.All();
        }

        MaxWordsLabel.Text = _memorizedWords.Count.ToString();

        if (_memorizedWords.Count > 0)
        {
            int defaultCount = Math.Min(10, _memorizedWords.Count);
            WordsCountEntry.Text = defaultCount.ToString();
        }
        else
        {
            WordsCountEntry.Text = "10";
        }
    }

    private void OnSinglePlayerTapped(object sender, TappedEventArgs e)
    {
        _isMultiplayer = false;
        UpdateGameModeUI();
    }

    private void OnMultiplayerTapped(object sender, TappedEventArgs e)
    {
        _isMultiplayer = true;
        UpdateGameModeUI();
    }

    private void UpdateGameModeUI()
    {
        Color normalBorderColor = Color.FromArgb("#333752");
        Color normalBgColor = Color.FromArgb("#151624");
        Color normalTextColor = Color.FromArgb("#94A3B8");

        Color selectedBorderColor = Color.FromArgb("#00E5FF");
        Color selectedBgColor = Color.FromArgb("#112A38");
        Color selectedTextColor = Colors.White;

        SinglePlayerCard.BackgroundColor = !_isMultiplayer ? selectedBgColor : normalBgColor;
        SinglePlayerCard.Stroke = !_isMultiplayer ? selectedBorderColor : normalBorderColor;
        SinglePlayerText.TextColor = !_isMultiplayer ? selectedTextColor : normalTextColor;

        MultiplayerCard.BackgroundColor = _isMultiplayer ? selectedBgColor : normalBgColor;
        MultiplayerCard.Stroke = _isMultiplayer ? selectedBorderColor : normalBorderColor;
        MultiplayerText.TextColor = _isMultiplayer ? selectedTextColor : normalTextColor;
    }

    private async void OnStartChallengeClicked(object sender, EventArgs e)
    {
        int count = 10;
        if (!string.IsNullOrWhiteSpace(WordsCountEntry.Text))
        {
            if (!int.TryParse(WordsCountEntry.Text, out count) || count <= 0)
            {
                await Toast.Make("يرجى إدخال عدد صحيح أكبر من الصفر للكلمات.", ToastDuration.Short).Show();
                return;
            }
        }

        if (count > _memorizedWords.Count)
        {
            await Toast.Make($"عدد الكلمات لا يمكن أن يتجاوز الكلمات المتاحة ({_memorizedWords.Count}).", ToastDuration.Short).Show();
            return;
        }

        if (_memorizedWords.Count == 0)
        {
            await Toast.Make("لا توجد كلمات متاحة حالياً.", ToastDuration.Short).Show();
            return;
        }

        int seconds = 10;
        if (!string.IsNullOrWhiteSpace(TimePerQuestionEntry.Text))
        {
            if (!int.TryParse(TimePerQuestionEntry.Text, out seconds) || seconds <= 0)
            {
                await Toast.Make("يرجى إدخال عدد ثوانٍ صحيح للزمن المسموح.", ToastDuration.Short).Show();
                return;
            }
        }

        _timePerQuestion = seconds;
        GenerateQuestions(count);

        if (!_isMultiplayer)
        {
            SetupView.IsVisible = false;
            GameView.IsVisible = true;
            LoadQuestion();
        }
        else
        {
            // --- Friend Challenge Mode ---
            try
            {
                SetLoadingState(true, "جاري فحص المتصلين... 🔍");

                if (!await Service.HasActiveInternetAsync(5))
                {
                    await Toast.Make("لا يوجد اتصال بالإنترنت. يرجى التحقق من الاتصال قبل بدء التحدي.").Show();
                    return;
                }

                AppShell? appShellForOnline = Application.Current?.MainPage as AppShell ?? Shell.Current as AppShell;
                if (appShellForOnline == null)
                {
                    await Toast.Make("تعذر الوصول لخدمات الاتصال.").Show();
                    return;
                }

                string currentUser = Preferences.Get("UserName", "");
                if (appShellForOnline.GameHub != null && (appShellForOnline.GameHub.HubConnection == null || appShellForOnline.GameHub.HubConnection.State != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected))
                {
                    SetLoadingState(true, "جاري الاتصال بالخادم... ⚡");
                    await appShellForOnline.GameHub.ConnectAsync(currentUser);
                }

                SetLoadingState(true, "جاري فحص الأصدقاء المتصلين... 🔍");
                var friendsTask = FetchFriendsFromDatabaseAsync();
                var onlineUsersTask = appShellForOnline.GetOnlineUsersAsync();

                await Task.WhenAll(friendsTask, onlineUsersTask);

                List<string> myFriends = await friendsTask;
                List<string> onlineUsers = (await onlineUsersTask) ?? new List<string>();

                if (myFriends == null || myFriends.Count == 0)
                {
                    try
                    {
                        string cachedFriendsJson = Preferences.Get("Cached_Friends_List", "[]");
                        myFriends = System.Text.Json.JsonSerializer.Deserialize<List<string>>(cachedFriendsJson) ?? new List<string>();
                    }
                    catch { }
                }

                if (myFriends == null || myFriends.Count == 0)
                {
                    await Toast.Make("ليس لديك أصدقاء مضافين حتى الآن.").Show();
                    return;
                }

                myFriends = myFriends.Where(f => onlineUsers.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList();

                if (myFriends.Count == 0)
                {
                    await Toast.Make("لا يوجد أي صديق متصل حالياً لتحديه 📭").Show();
                    return;
                }

                SetLoadingState(false);

                var popup = new Popups.FriendSelectPopup("تحدي الكتابة", myFriends);
                var result = await this.ShowPopupAsync(popup);

                if (result is string targetFriend && !string.IsNullOrEmpty(targetFriend))
                {
                    int friendWordsCount = await Service.FetchFriendMemorizedWordsCountAsync(targetFriend);
                    int myWordsCount = _memorizedWords.Count;
                    int effectiveLimit = (friendWordsCount > 0) ? Math.Min(myWordsCount, friendWordsCount) : myWordsCount;

                    GenerateQuestions(count, effectiveLimit);

                    if (Shell.Current is AppShell appShell)
                    {
                        // --- Pre-checks: ensure both sender and receiver have enough coins and remaining friend-challenge attempts ---
                        const int stakeForWriting = 5;

                        // Sync with server first to adopt the latest/higher stats if updated
                        await Service.SyncUserStatsWithServerAsync();

                        long localFriendsRemaining = Preferences.Get("FriendsChallengeCount", 0L);
                        long localCoins = Preferences.Get("Coins", 0L);
                        if (localFriendsRemaining <= 0)
                        {
                            await Toast.Make("لا توجد لديك محاولات لتحدي الأصدقاء ⚠️").Show();
                            return;
                        }
                        if (localCoins < stakeForWriting)
                        {
                            await Toast.Make("لا يوجد لديك عملات كافية لإرسال التحدي ⚠️").Show();
                            return;
                        }

                        if (!await Service.HasActiveInternetAsync(5))
                        {
                            await Toast.Make("يتطلب إرسال التحدي اتصالاً بالإنترنت للتحقق من حالة الصديق.").Show();
                            return;
                        }

                        // Fetch friend info directly by username (username is unique)
                        var friendResult = await Service.GetUserByUserName(targetFriend);
                        if (!friendResult.Success || friendResult.Data == null)
                        {
                            await Toast.Make("تعذر التحقق من حالة الصديق. حاول لاحقاً.").Show();
                            return;
                        }

                        if (friendResult.Data.FriendsChallengeCount <= 0)
                        {
                            await Toast.Make($"{targetFriend} ليس لديه محاولات متبقية لتلقي التحدي.").Show();
                            return;
                        }

                        if (friendResult.Data.Coins < stakeForWriting)
                        {
                            await Toast.Make($"{targetFriend} لا يملك عملات كافية لقبول التحدي.").Show();
                            return;
                        }

                        var payload = new WritingGamePayload { Questions = _questions, TimePerQuestion = _timePerQuestion };
                        string payloadJson = JsonSerializer.Serialize(payload);

                        try
                        {
                            await appShell.GameHub.SendChallengeAsync(targetFriend, "WritingMulti", payloadJson);
                        }
                        catch
                        {
                            await appShell.GameHub.SendChallengeAsync(targetFriend, "WritingMulti");
                        }

                        var waitingPopup = new Popups.WaitingChallengePopup(targetFriend);

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
                        var waitResult = await this.ShowPopupAsync(waitingPopup);
                        appShell.GameHub.OnChallengeResponseReceived -= onChallengeResponded;

                        if (waitResult is string status && status == "Accepted")
                        {
                            currentUser = Preferences.Get("UserName", "");
                            _hubConnection = appShell.GameHub.HubConnection;
                            _roomName = string.Compare(currentUser, targetFriend, StringComparison.Ordinal) < 0
                                        ? $"room_{currentUser}_{targetFriend}" : $"room_{targetFriend}_{currentUser}";
                            _opponentName = targetFriend;

                            MyNameLabel.Text = currentUser;
                            OpponentNameLabel.Text = targetFriend;

                            SetupView.IsVisible = false;
                            GameView.IsVisible = true;
                            OpponentScoreContainer.IsVisible = true;

                            SetupMultiplayerCurrentConnection(payloadJson);

                            try
                            {
                                if (_hubConnection != null)
                                {
                                    await _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, currentUser, "PAYLOAD:" + payloadJson);
                                }
                            }
                            catch { }
                            // --- Challenge accepted: decrement local FriendsChallengeCount (only challenger) and deduct stake coins ---
                            const int stake = 5; // writing challenge

                            long originalChallenges = Preferences.Get("FriendsChallengeCount", 0L);
                            long originalCoins = Preferences.Get("Coins", 0L);

                            long.TryParse(Preferences.Get("ID", "0")?.Trim('"', ' '), out long id);

                            long newChallenges = originalChallenges > 0 ? originalChallenges - 1 : 0;
                            long newCoins = originalCoins - stake;

                            // الخصم محلياً
                            Preferences.Set("FriendsChallengeCount", newChallenges);
                            Preferences.Set("Coins", newCoins);

                            // الخصم في السيرفر بشكل إلزامي قبل الدخول للجولة
                            bool serverDeducted = false;
                            try
                            {
                                if (await Service.HasActiveInternetAsync(5))
                                {
                                    var resCount = await Service.UpdateFriendsChallengeCount(new User
                                    {
                                        ID = id,
                                        UserName = currentUser,
                                        FriendsChallengeCount = newChallenges
                                    });

                                    var resCoins = await Service.UpdateCoins(new User
                                    {
                                        ID = id,
                                        UserName = currentUser,
                                        Coins = newCoins
                                    });

                                    serverDeducted = (resCount == "1" && resCoins == "1");
                                }
                            }
                            catch { }

                            if (!serverDeducted)
                            {
                                // التراجع عن الخصم المحلي لعدم نجاح السيرفر
                                Preferences.Set("FriendsChallengeCount", originalChallenges);
                                Preferences.Set("Coins", originalCoins);

                                await Toast.Make("تعذر بدء التحدي: فشل الاتصال بالسيرفر لخصم الرصيد ⚠️").Show();
                                return;
                            }
                        }
                        else if (waitResult is string s && s == "Rejected")
                        {
                            await Toast.Make($"{targetFriend} رفض التحدي أو هو مشغول حالياً.").Show();
                        }
                        else if (waitResult is string s2 && s2 == "Cancel")
                        {
                            await appShell.GameHub.CancelChallengeAsync(targetFriend);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("خطأ في الاتصال", $"تعذر بدء التحدي: {ex.Message}", "حسناً");
            }
            finally
            {
                SetLoadingState(false);
            }
        }
    }

    private void SetLoadingState(bool isLoading, string statusText = "")
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StartLoadingIndicator.IsRunning = isLoading;
            StartLoadingIndicator.IsVisible = isLoading;
            StartGameButton.Text = isLoading ? statusText : "🚀 بدء التحدي";
        });
    }

    private async Task<List<string>> FetchFriendsFromDatabaseAsync()
    {
        var currentUserName = Preferences.Get("UserName", "");
        if (string.IsNullOrEmpty(currentUserName))
            return new List<string>();

        string[] friendsArray = await Service.GetFriendsAsync(currentUserName);
        return [.. friendsArray];
    }

    private void GenerateQuestions(int count, int maxAllowedWords = 0)
    {
        List<WordModel> sourceWords;
        if (_memorizedWords != null && _memorizedWords.Count > 0)
        {
            int limit = (maxAllowedWords > 0 && maxAllowedWords < _memorizedWords.Count)
                ? maxAllowedWords
                : _memorizedWords.Count;
            sourceWords = _memorizedWords.Take(limit).ToList();
        }
        else
        {
            sourceWords = TenWords.All();
        }

        if (sourceWords.Count == 0) return;

        // Shuffle using Fisher-Yates
        var shuffledSource = new List<WordModel>(sourceWords);
        for (int i = shuffledSource.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (shuffledSource[i], shuffledSource[j]) = (shuffledSource[j], shuffledSource[i]);
        }

        var selectedWords = shuffledSource.Take(count).ToList();

        _questions = selectedWords.Select(w => new WritingQuestionItem
        {
            ArabicWord = w.ArabicWord,
            EnglishWord = w.EnglishWord
        }).ToList();

        _currentIndex = 0;
        _score = 0;
        _opponentScore = 0;
        ScoreLabel.Text = "0";
        OpponentScoreLabel.Text = "0";
    }

    private async void LoadQuestion()
    {
        StopNextQuestionTimer();

        if (_currentIndex >= _questions.Count)
        {
            EndGame();
            return;
        }

        _isTransitioning = false;
        _answered = false;
        var q = _questions[_currentIndex];

        QuestionIndexLabel.Text = $"سؤال {_currentIndex + 1} من {_questions.Count}";
        ArabicWordLabel.Text = q.ArabicWord;

        AnswerEntry.Text = string.Empty;
        AnswerEntry.IsEnabled = true;
        AnswerBorder.Stroke = Color.FromArgb("#0284C7");
        AnswerBorder.TranslationX = 0;
        AnswerBorder.Scale = 1.0;
        FeedbackLabel.IsVisible = false;

        ActionButton.Text = "تأكيد الإجابة ➔";

        // انتقال بصري سلس للكلمة
        ArabicWordLabel.Opacity = 0.5;
        await ArabicWordLabel.FadeTo(1.0, 180);

        StartTimer();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AnswerEntry.Focus();
        });
    }

    private void StartTimer()
    {
        StopTimer();
        _timeLeft = _timePerQuestion;
        TimerLabel.Text = _timeLeft.ToString();
        TimerProgress.Progress = 1.0;

        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += (s, e) =>
        {
            _timeLeft--;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_isLeaving || _answered) return;

                TimerLabel.Text = Math.Max(0, _timeLeft).ToString();
                double targetProgress = Math.Max(0.0, (double)_timeLeft / _timePerQuestion);
                try
                {
                    await TimerProgress.ProgressTo(targetProgress, 600, Easing.Linear);
                }
                catch
                {
                    TimerProgress.Progress = targetProgress;
                }

                if (_timeLeft <= 0)
                {
                    StopTimer();
                    ProcessAnswer(null);
                }
            });
        };
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private void StartNextQuestionTimer()
    {
        StopNextQuestionTimer();
        if (_isLeaving || (WithdrawalBanner != null && WithdrawalBanner.IsVisible)) return;

        _nextQuestionTimeLeft = 5;
        bool isLast = _currentIndex >= _questions.Count - 1;
        string buttonBaseText = isLast ? "عرض النتيجة" : "السؤال التالي";
        string buttonIcon = isLast ? "🏆" : "➔";

        ActionButton.Text = $"{buttonBaseText} ({_nextQuestionTimeLeft})... {buttonIcon}";

        _nextQuestionTimer = new System.Timers.Timer(1000);
        _nextQuestionTimer.Elapsed += (s, e) =>
        {
            _nextQuestionTimeLeft--;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_isLeaving || (WithdrawalBanner != null && WithdrawalBanner.IsVisible))
                {
                    StopNextQuestionTimer();
                    return;
                }

                if (_nextQuestionTimeLeft > 0)
                {
                    ActionButton.Text = $"{buttonBaseText} ({_nextQuestionTimeLeft})... {buttonIcon}";
                }
                else
                {
                    StopNextQuestionTimer();
                    OnActionClicked(this, EventArgs.Empty);
                }
            });
        };
        _nextQuestionTimer.Start();
    }

    private void StopNextQuestionTimer()
    {
        if (_nextQuestionTimer != null)
        {
            _nextQuestionTimer.Stop();
            _nextQuestionTimer.Dispose();
            _nextQuestionTimer = null;
        }
    }

    private void OnAnswerTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_answered)
        {
            AnswerBorder.Stroke = Color.FromArgb("#00E5FF");
        }
    }

    private void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_answered) return;
        ProcessAnswer(AnswerEntry.Text);
    }

    private async Task SafePopAsync()
    {
        try
        {
            if (Navigation.ModalStack.Count > 0)
            {
                await Navigation.PopModalAsync();
            }
            else if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
        }
        catch { }
    }

    private void OnActionClicked(object sender, EventArgs e)
    {
        if (WithdrawalBanner != null && WithdrawalBanner.IsVisible)
        {
            _isLeaving = true;
            StopTimer();
            StopNextQuestionTimer();
            CleanUpMultiplayer();
            _ = SafePopAsync();
            return;
        }

        if (!_answered)
        {
            ProcessAnswer(AnswerEntry.Text);
        }
        else
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            StopNextQuestionTimer();
            _currentIndex++;
            LoadQuestion();
        }
    }

    private static string NormalizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var cleaned = input.Trim()
                           .TrimEnd('.', ',', '!', '?')
                           .Replace('’', '\'')
                           .Replace('‘', '\'')
                           .Replace('`', '\'');
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
        return cleaned;
    }

    private void ProcessAnswer(string? userAnswer)
    {
        if (_answered) return;
        _answered = true;
        StopTimer();
        AnswerEntry.IsEnabled = false;

        var currentQ = _questions[_currentIndex];
        string cleanUser = NormalizeText(userAnswer);
        string cleanExpected = NormalizeText(currentQ.EnglishWord);

        bool isCorrect = !string.IsNullOrEmpty(cleanUser) &&
                         cleanUser.Equals(cleanExpected, StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            _score += 10;
            ScoreLabel.Text = _score.ToString();

            AnswerBorder.Stroke = Color.FromArgb("#10B981");
            FeedbackLabel.Text = "إجابة صحيحة! 🎉";
            FeedbackLabel.TextColor = Color.FromArgb("#10B981");
            FeedbackLabel.IsVisible = true;

            // نبضة تفاعلية خفيفة عند الصحة
            _ = AnswerBorder.ScaleTo(1.03, 80).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => AnswerBorder.ScaleTo(1.0, 80)));
        }
        else
        {
            AnswerBorder.Stroke = Color.FromArgb("#EF4444");
            FeedbackLabel.Text = $"إجابة خاطئة! الكلمة الصحيحة هي: {currentQ.EnglishWord}";
            FeedbackLabel.TextColor = Color.FromArgb("#EF4444");
            FeedbackLabel.IsVisible = true;

            // اهتزاز خفيف مدروس للتنبيه بالخطأ
            _ = Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        await AnswerBorder.TranslateTo(-7, 0, 35);
                        await AnswerBorder.TranslateTo(7, 0, 35);
                        await AnswerBorder.TranslateTo(-4, 0, 35);
                        await AnswerBorder.TranslateTo(4, 0, 35);
                        await AnswerBorder.TranslateTo(0, 0, 35);
                    }
                    catch { }
                });
            });
        }

        // Multiplayer score update
        if (_isMultiplayer && _hubConnection != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    string currentUser = Preferences.Get("UserName", "");
                    await _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, currentUser, "SCORE:" + _score);
                }
                catch { }
            });
        }

        StartNextQuestionTimer();
    }

    private async void EndGame()
    {
        StopTimer();
        StopNextQuestionTimer();

        if (!_isMultiplayer)
        {
            if (_isLeaving) return;

            _isLeaving = true;

            var resultPopup = new SingleChallengeResultPopup(
                _score,
                _questions.Count);

            await this.ShowPopupAsync(resultPopup);

            await SafePopAsync();
        }
        else
        {
            _hasLocalFinished = true;

            if (_hubConnection != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string currentUser = Preferences.Get("UserName", "");
                        await _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, currentUser, $"FINISHED:{_score}");
                    }
                    catch { }
                });
            }

            if (_hasOpponentFinished)
            {
                ShowFinalMultiplayerResult();
            }
            else
            {
                ShowWaitingForOpponentUI();
            }
        }
    }

    private void ShowWaitingForOpponentUI()
    {
        if (AnswerEntry != null)
        {
            AnswerEntry.IsEnabled = false;
        }

        if (WithdrawalIconLabel != null) WithdrawalIconLabel.Text = "⏳";
        if (WithdrawalTextLabel != null)
        {
            WithdrawalTextLabel.Text = "لقد أنهيت جميع الأسئلة! بانتظار الخصم لإكمال التحدي...";
            WithdrawalTextLabel.TextColor = Color.FromArgb("#38BDF8");
        }
        if (WithdrawalSubTextLabel != null)
        {
            WithdrawalSubTextLabel.Text = "سيتم إظهار النتيجة النهائية فور إكمال الطرف الآخر للأسئلة ⏱️";
            WithdrawalSubTextLabel.TextColor = Color.FromArgb("#94A3B8");
            WithdrawalSubTextLabel.IsVisible = true;
        }
        if (WithdrawalBanner != null)
        {
            WithdrawalBanner.BackgroundColor = Color.FromArgb("#0F172A");
            WithdrawalBanner.Stroke = Color.FromArgb("#0EA5E9");
            WithdrawalBanner.IsVisible = true;
        }

        if (ActionButton != null)
        {
            ActionButton.IsVisible = false;
        }
    }

    private async void ShowFinalMultiplayerResult()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        StopTimer();
        StopNextQuestionTimer();

        string myName = Preferences.Get("UserName", "أنا");
        // --- Award coins to winner (writing: winner gets 10 coins) or refund on draw ---
        if (_score > _opponentScore)
        {
            AwardWinnerCoins(10);
        }
        else if (_score == _opponentScore)
        {
            AwardWinnerCoins(5);
        }

        var resultPopup = new ChallengeResultPopup(myName, _score, _opponentName, _opponentScore);
        await this.ShowPopupAsync(resultPopup);

        CleanUpMultiplayer();
        await SafePopAsync();
    }

    private void AwardWinnerCoins(int reward = 10)
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

    private async Task ConfirmExitAsync()
    {
        if (_isLeaving) return;

        // إذا كانت اللعبة قد انتهت بانسحاب الخصم، اخرج مباشرة دون إظهار رسالة تأكيد
        if (WithdrawalBanner != null && WithdrawalBanner.IsVisible)
        {
            _isLeaving = true;
            StopTimer();
            StopNextQuestionTimer();
            CleanUpMultiplayer();
            await SafePopAsync();
            return;
        }

        if (GameView.IsVisible && _currentIndex < _questions.Count)
        {
            string title = _isMultiplayer ? "تأكيد الانسحاب" : "تأكيد الخروج";
            string message = _isMultiplayer
                ? "هل أنت متأكد أنك تريد الانسحاب؟ سيتم إنهاء اللعبة واحتساب فوز للخصم."
                : "هل أنت متأكد أنك تريد الخروج من الجولة؟";
            string confirmBtn = _isMultiplayer ? "نعم، انسحب" : "نعم";

            bool confirm = await DisplayAlert(title, message, confirmBtn, "إلغاء");
            if (!confirm) return;

            if (_isMultiplayer && _hubConnection != null)
            {
                try
                {
                    string currentUser = Preferences.Get("UserName", "");
                    await _hubConnection.InvokeAsync("SendDuelWithdrawal", _roomName, currentUser);
                }
                catch { }
            }
        }

        _isLeaving = true;
        StopTimer();
        StopNextQuestionTimer();
        CleanUpMultiplayer();
        await SafePopAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await ConfirmExitAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        if (GameView.IsVisible && !_isLeaving)
        {
            Dispatcher.Dispatch(async () => await ConfirmExitAsync());
            return true;
        }
        return base.OnBackButtonPressed();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTimer();
        StopNextQuestionTimer();
        CleanUpMultiplayer();
    }
}
