using System.Text.Json;
using English.Services;
using English.Models;
using English.Popups;
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
    private bool _hasLocalFinished = false;
    private bool _hasOpponentFinished = false;
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
                    int friendWordsCount = await Service.FetchFriendMemorizedWordsCountAsync(targetFriend);
                    int myWordsCount = _memorizedWords.Count;
                    int effectiveLimit = (friendWordsCount > 0) ? Math.Min(myWordsCount, friendWordsCount) : myWordsCount;

                    GenerateQuestions(count, effectiveLimit);

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

    private void GenerateQuestions(int count, int maxAllowedWords = 0)
    {
        _questions.Clear();
        var allWordsFromFile = TenWords.GetAllWords();
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
            sourceWords = allWordsFromFile;
        }

        if (sourceWords.Count == 0) return;

        var selectedWords = sourceWords.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();

        foreach (var word in selectedWords)
        {
            var options = new List<string> { word.ArabicWord };
            var wrongOptions = allWordsFromFile
                .Where(w => !string.IsNullOrWhiteSpace(w.ArabicWord) && w.ArabicWord != word.ArabicWord)
                .Select(w => w.ArabicWord)
                .Distinct()
                .OrderBy(_ => Guid.NewGuid())
                .Take(3)
                .ToList();

            options.AddRange(wrongOptions);

            _questions.Add(new QuestionItem
            {
                Word = word.EnglishWord,
                CorrectAnswer = word.ArabicWord,
                Options = options.OrderBy(_ => Guid.NewGuid()).ToList()
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

        Border? border = sender as Border;
        string param = e.Parameter?.ToString() ?? "";

        if (border == null && sender is TapGestureRecognizer tap)
        {
            border = tap.Parent as Border;
        }

        if (border == null)
        {
            if (param == "0") border = OptionBorder0;
            else if (param == "1") border = OptionBorder1;
            else if (param == "2") border = OptionBorder2;
            else if (param == "3") border = OptionBorder3;
        }

        if (border != null)
        {
            string selected = "";
            if (border == OptionBorder0 || param == "0") selected = OptionBtn0.Text;
            else if (border == OptionBorder1 || param == "1") selected = OptionBtn1.Text;
            else if (border == OptionBorder2 || param == "2") selected = OptionBtn2.Text;
            else if (border == OptionBorder3 || param == "3") selected = OptionBtn3.Text;

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

            if (_hasOpponentFinished) // في حال الخصم انتهى من الاختبار
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

        if (NextButton != null)
        {
            NextButton.IsVisible = false;
        }
    }

    private async void ShowFinalMultiplayerResult()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        StopTimer();
        StopNextQuestionTimer();

        string myName = Preferences.Get("UserName", "أنا");
        var resultPopup = new ChallengeResultPopup(myName, _score, _opponentName, _opponentScore);
        await this.ShowPopupAsync(resultPopup);

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