using System.Text.Json;
using English.Services;
using English.Models;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using Microsoft.AspNetCore.SignalR.Client;

namespace English.Pages;

public partial class ChoiceChallengePage : ContentPage
{
    private class QuestionItem
    {
        public string Word { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
    }

    private class GamePayload
    {
        public List<QuestionItem> Questions { get; set; } = new();
        public int TimePerQuestion { get; set; }
    }

    private List<QuestionItem> _questions = new();
    private int _currentIndex = 0;
    private int _score = 0;
    private int _opponentScore = 0;
    private System.Timers.Timer? _timer;
    private System.Timers.Timer? _nextQuestionTimer;
    private int _nextQuestionTimeLeft = 5;
    private int _timePerQuestion = 5;
    private int _timeLeft = 5;
    private bool _answered = false;
    private bool _isLeaving = false;
    private bool _isMultiplayer = false;
    private List<WordModel> _memorizedWords = new();

    private HubConnection? _hubConnection;
    private string _roomName = "";
    private string _opponentName = "";

    // للعب الفردي
    public ChoiceChallengePage()
    {
        InitializeComponent();
        LoadSetupData();
        UpdateGameModeUI();
    }

    // للعب الثنائي (متلقي التحدي أو مرسل التحدي بعد القبول)
    public ChoiceChallengePage(HubConnection hubConnection, string roomName, string opponentName, string payloadJson)
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
                            var payload = JsonSerializer.Deserialize<GamePayload>(json);
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

                        NextButton.Text = "العودة للقائمة الرئيسية ➔";
                        NextButton.IsVisible = true;
                    });
                }
            });
        }

        if (!string.IsNullOrEmpty(payloadJson))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<GamePayload>(payloadJson);
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
        Color normalBorderColor = Color.FromArgb("#23344D");
        Color normalBgColor = Color.FromArgb("#121B2D");
        Color normalTextColor = Color.FromArgb("#94A3B8");

        Color selectedBorderColor = Color.FromArgb("#34D399");
        Color selectedBgColor = Color.FromArgb("#121B2D"); // نفس الخلفية
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
            await Toast.Make($"عدد الكلمات لا يمكن أن يتجاوز الكلمات المحفوظة ({_memorizedWords.Count}).", ToastDuration.Short).Show();
            return;
        }

        if (_memorizedWords.Count < 4)
        {
            await Toast.Make("يجب أن تحفظ 4 كلمات على الأقل لتتمكن من اللعب.", ToastDuration.Short).Show();
            return;
        }

        int seconds = 5;
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
            // --- وضع تحدي صديق ---
            try
            {
                List<string> myFriends = await FetchFriendsFromDatabaseAsync();

                if (myFriends == null || myFriends.Count == 0)
                {
                    await DisplayAlert("عذراً", "ليس لديك أصدقاء مضافين حالياً لتحديهم. قم بإضافة أصدقاء أولاً!", "حسناً");
                    return;
                }

                var popup = new Popups.FriendSelectPopup("تحدي الخيارات", myFriends);
                var result = await this.ShowPopupAsync(popup);

                if (result is string targetFriend && !string.IsNullOrEmpty(targetFriend))
                {
                    if (Shell.Current is AppShell appShell)
                    {
                        var payload = new GamePayload { Questions = _questions, TimePerQuestion = _timePerQuestion };
                        string payloadJson = JsonSerializer.Serialize(payload);

                        try
                        {
                            await appShell.GameHub.SendChallengeAsync(targetFriend, "ChoiceMulti", payloadJson);
                        }
                        catch
                        {
                            await appShell.GameHub.SendChallengeAsync(targetFriend, "ChoiceMulti");
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

                            await appShell.GameHub.JoinDuelRoomAsync(_roomName);

                            SetupMultiplayerCurrentConnection(payloadJson);

                            try
                            {
                                await _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, currentUser, "PAYLOAD:" + payloadJson);
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
                Console.WriteLine($"Error in multiplayer game start: {ex.Message}");
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
        _questions.Clear();
        var selectedWords = _memorizedWords.OrderBy(w => Guid.NewGuid()).Take(count).ToList();
        var allWords = TenWords.All();
        if (allWords.Count < 4) allWords = _memorizedWords; // fallback

        foreach (var word in selectedWords)
        {
            var options = new List<string> { word.ArabicWord };
            var wrongOptions = allWords
                .Where(w => w.ArabicWord != word.ArabicWord)
                .OrderBy(w => Guid.NewGuid())
                .Take(3)
                .Select(w => w.ArabicWord)
                .ToList();

            options.AddRange(wrongOptions);

            _questions.Add(new QuestionItem
            {
                Word = word.EnglishWord,
                CorrectAnswer = word.ArabicWord,
                Options = options.OrderBy(o => Guid.NewGuid()).ToList()
            });
        }
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
        NextButton.IsVisible = false;
        ResetOptionButtons();

        QuestionIndexLabel.Text = $"سؤال {_currentIndex + 1} من {_questions.Count}";

        var q = _questions[_currentIndex];
        QuestionWordLabel.Text = q.Word;

        OptionBtn0.Text = q.Options[0];
        OptionBtn1.Text = q.Options[1];
        OptionBtn2.Text = q.Options[2];
        OptionBtn3.Text = q.Options[3];

        StartTimer();
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
                TimerLabel.Text = _timeLeft.ToString();
                TimerProgress.Progress = (double)_timeLeft / (double)_timePerQuestion;

                if (_timeLeft <= 0)
                {
                    StopTimer();
                    OnTimeExpired();
                }
            });
        };
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
        }
    }

    private void StartNextQuestionTimer()
    {
        StopNextQuestionTimer();
        if (_isLeaving || (WithdrawalBanner != null && WithdrawalBanner.IsVisible)) return;

        _nextQuestionTimeLeft = 5;
        string buttonBaseText = (_currentIndex < _questions.Count - 1) ? "السؤال التالي" : "عرض النتيجة";
        string buttonIcon = (_currentIndex < _questions.Count - 1) ? "➔" : "🏆";

        NextButton.Text = $"{buttonBaseText} ({_nextQuestionTimeLeft})... {buttonIcon}";
        NextButton.IsVisible = true;

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
                    NextButton.Text = $"{buttonBaseText} ({_nextQuestionTimeLeft})... {buttonIcon}";
                }
                else
                {
                    StopNextQuestionTimer();
                    OnNextClicked(this, EventArgs.Empty);
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

    private void OnTimeExpired()
    {
        if (_answered) return;
        _answered = true;
        HighlightCorrectAnswer();
        StartNextQuestionTimer();
    }

    private async void OnOptionClicked(object sender, TappedEventArgs e)
    {
        if (_answered) return;
        _answered = true;
        StopTimer();

        if (sender is Border border)
        {
            string selected = "";
            if (border == OptionBorder0) selected = OptionBtn0.Text;
            else if (border == OptionBorder1) selected = OptionBtn1.Text;
            else if (border == OptionBorder2) selected = OptionBtn2.Text;
            else if (border == OptionBorder3) selected = OptionBtn3.Text;

            var currentQ = _questions[_currentIndex];

            if (selected == currentQ.CorrectAnswer)
            {
                border.BackgroundColor = Color.FromArgb("#10B981"); // Green
                border.Stroke = Color.FromArgb("#10B981");
                _score += 10;
                ScoreLabel.Text = _score.ToString();
            }
            else
            {
                border.BackgroundColor = Color.FromArgb("#EF4444"); // Red
                border.Stroke = Color.FromArgb("#EF4444");
                HighlightCorrectAnswer();
            }

            if (_isMultiplayer && _hubConnection != null)
            {
                try
                {
                    string currentUser = Preferences.Get("UserName", "");
                    await _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, currentUser, $"SCORE:{_score}");
                }
                catch { }
            }
        }

        StartNextQuestionTimer();
    }

    private void HighlightCorrectAnswer()
    {
        var correct = _questions[_currentIndex].CorrectAnswer;

        if (OptionBtn0.Text == correct) { OptionBorder0.BackgroundColor = Color.FromArgb("#10B981"); OptionBorder0.Stroke = Color.FromArgb("#10B981"); }
        if (OptionBtn1.Text == correct) { OptionBorder1.BackgroundColor = Color.FromArgb("#10B981"); OptionBorder1.Stroke = Color.FromArgb("#10B981"); }
        if (OptionBtn2.Text == correct) { OptionBorder2.BackgroundColor = Color.FromArgb("#10B981"); OptionBorder2.Stroke = Color.FromArgb("#10B981"); }
        if (OptionBtn3.Text == correct) { OptionBorder3.BackgroundColor = Color.FromArgb("#10B981"); OptionBorder3.Stroke = Color.FromArgb("#10B981"); }
    }

    private void ResetOptionButtons()
    {
        Border[] borders = { OptionBorder0, OptionBorder1, OptionBorder2, OptionBorder3 };
        foreach (var b in borders)
        {
            b.BackgroundColor = Color.FromArgb("#121B2D"); // مطابقة لخلفية الكارد في الـ XAML الجديد
            b.Stroke = Color.FromArgb("#23344D");         // مطابقة لحدود الكارد في الـ XAML الجديد
        }
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

    private async void OnNextClicked(object sender, EventArgs e)
    {
        StopNextQuestionTimer();

        if (WithdrawalBanner != null && WithdrawalBanner.IsVisible)
        {
            _isLeaving = true;
            StopTimer();
            await SafePopAsync();
            return;
        }

        _currentIndex++;
        LoadQuestion();
    }

    private async void EndGame()
    {
        if (_isLeaving) return;
        _isLeaving = true;
        StopTimer();

        string matchResult = $"رائع جداً! لقد أكملت التحدي وحصلت على {_score} نقطة.";
        if (_isMultiplayer)
        {
            if (_score > _opponentScore) matchResult = $"🎉 لقد فزت! \nنقاطك: {_score} \nنقاط الخصم: {_opponentScore}";
            else if (_score < _opponentScore) matchResult = $"😔 لقد خسرت.. \nنقاطك: {_score} \nنقاط الخصم: {_opponentScore}";
            else matchResult = $"🤝 تعادل! \nالنقاط: {_score}";
        }

        await DisplayAlert("انتهاء التحدي 🎉", matchResult, "حسناً");
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