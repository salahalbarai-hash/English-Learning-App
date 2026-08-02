using System.Text.Json;
using English.Services;
using English.Models;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using Microsoft.AspNetCore.SignalR.Client;

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
        public List<WritingQuestionItem> Questions { get; set; } = new();
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
    private bool _isMultiplayer = false;
    private List<WordModel> _memorizedWords = new();

    private HubConnection? _hubConnection;
    private string _roomName = "";
    private string _opponentName = "";

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
                    if (responder == _opponentName && msg.StartsWith("SCORE:"))
                    {
                        if (int.TryParse(msg.Replace("SCORE:", ""), out int opScore))
                        {
                            _opponentScore = opScore;
                            OpponentScoreLabel.Text = _opponentScore.ToString();
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

                        WithdrawalTextLabel.Text = $"⚠️ اللاعب {withdrawingUser} انسحب من التحدي!";
                        WithdrawalBanner.IsVisible = true;

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
                List<string> myFriends = await FetchFriendsFromDatabaseAsync();

                if (myFriends == null || myFriends.Count == 0)
                {
                    await DisplayAlert("عذراً", "ليس لديك أصدقاء مضافين حالياً لتحديهم. قم بإضافة أصدقاء أولاً!", "حسناً");
                    return;
                }

                var popup = new Popups.FriendSelectPopup("تحدي الكتابة", myFriends);
                var result = await this.ShowPopupAsync(popup);

                if (result is string targetFriend && !string.IsNullOrEmpty(targetFriend))
                {
                    if (Shell.Current is AppShell appShell)
                    {
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
                            string currentUser = Preferences.Get("UserName", "");
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
                                    await _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, "PAYLOAD:" + payloadJson);
                                }
                            }
                            catch { }
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
        }
    }

    private async Task<List<string>> FetchFriendsFromDatabaseAsync()
    {
        var currentUserName = Preferences.Get("UserName", "");
        if (string.IsNullOrEmpty(currentUserName))
            return new List<string>();

        string[] friendsArray = await Service.GetFriendsAsync(currentUserName);
        return [.. friendsArray];
    }

    private void GenerateQuestions(int count)
    {
        var rnd = new Random();
        var selectedWords = _memorizedWords.OrderBy(_ => rnd.Next()).Take(count).ToList();

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

    private void LoadQuestion()
    {
        StopNextQuestionTimer();

        if (_currentIndex >= _questions.Count)
        {
            EndGame();
            return;
        }

        _answered = false;
        var q = _questions[_currentIndex];

        QuestionIndexLabel.Text = $"سؤال {_currentIndex + 1} من {_questions.Count}";
        ArabicWordLabel.Text = q.ArabicWord;

        AnswerEntry.Text = string.Empty;
        AnswerEntry.IsEnabled = true;
        AnswerBorder.Stroke = Color.FromArgb("#0284C7");
        FeedbackLabel.IsVisible = false;

        ActionButton.Text = "تأكيد الإجابة ➔";

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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_isLeaving) return;
                TimerLabel.Text = Math.Max(0, _timeLeft).ToString();
                TimerProgress.Progress = (double)Math.Max(0, _timeLeft) / _timePerQuestion;

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
                    _currentIndex++;
                    LoadQuestion();
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

    private async void OnActionClicked(object sender, EventArgs e)
    {
        if (WithdrawalBanner != null && WithdrawalBanner.IsVisible)
        {
            _isLeaving = true;
            StopTimer();
            StopNextQuestionTimer();
            await SafePopAsync();
            return;
        }

        if (!_answered)
        {
            ProcessAnswer(AnswerEntry.Text);
        }
        else
        {
            StopNextQuestionTimer();
            _currentIndex++;
            LoadQuestion();
        }
    }

    private void ProcessAnswer(string? userAnswer)
    {
        if (_answered) return;
        _answered = true;
        StopTimer();
        AnswerEntry.IsEnabled = false;

        var currentQ = _questions[_currentIndex];
        bool isCorrect = !string.IsNullOrWhiteSpace(userAnswer) &&
                         userAnswer.Trim().Equals(currentQ.EnglishWord.Trim(), StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            _score += 10;
            ScoreLabel.Text = _score.ToString();

            AnswerBorder.Stroke = Color.FromArgb("#10B981");
            FeedbackLabel.Text = "إجابة صحيحة! 🎉";
            FeedbackLabel.TextColor = Color.FromArgb("#10B981");
            FeedbackLabel.IsVisible = true;
        }
        else
        {
            AnswerBorder.Stroke = Color.FromArgb("#EF4444");
            FeedbackLabel.Text = $"إجابة خاطئة! الكلمة الصحيحة هي: {currentQ.EnglishWord}";
            FeedbackLabel.TextColor = Color.FromArgb("#EF4444");
            FeedbackLabel.IsVisible = true;
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
            await DisplayAlert("انتهاء اللعبة 🏆", $"أحسنت! أنهيت التحدي بنجاح.\nنقاطك الإجمالية: {_score}", "حسناً");
        }
        else
        {
            string resultMessage;
            if (_score > _opponentScore)
            {
                resultMessage = $"تهانينا! لقد فزت في التحدي 🎉\n\nنقاطك: {_score}\nنقاط {_opponentName}: {_opponentScore}";
            }
            else if (_score < _opponentScore)
            {
                resultMessage = $"للأسف، خسرت هذا التحدي! 👍\n\nنقاطك: {_score}\nنقاط {_opponentName}: {_opponentScore}";
            }
            else
            {
                resultMessage = $"تعادل ممتاز بينكما! 🤝\n\nالنقاط: {_score}";
            }

            await DisplayAlert("نتيجة التحدي 🏁", resultMessage, "حسناً");
        }

        _isLeaving = true;
        await SafePopAsync();
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
    }
}
