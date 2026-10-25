using English.Models;
using English.Services;

namespace English.Pages;

public partial class TikTokLiveArenaPage : ContentPage
{
    private class QuestionItem
    {
        public string Word { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
    }

    private readonly TikTokLiveConfig _config;
    private readonly List<QuestionItem> _questions = new();
    private int _currentIndex = 0;

    private System.Timers.Timer? _roundTimer;
    private int _timeLeft = 10;
    private bool _isRoundActive = false;
    private bool _isPaused = false;
    private bool _isLeaving = false;

    // حالة التصويت اللحظي لكل سؤال
    private readonly int[] _votesCount = new int[4];
    private readonly HashSet<string> _votedUsersThisRound = new(StringComparer.OrdinalIgnoreCase);
    private string? _fastestUserThisRound = null;

    // سجل نقاط المشاهدين (Leaderboard)
    private readonly Dictionary<string, TikTokViewerScore> _scoresMap = new(StringComparer.OrdinalIgnoreCase);

    public TikTokLiveArenaPage(TikTokLiveConfig config)
    {
        InitializeComponent();
        _config = config;

        HostHandleLabel.Text = $"@{_config.HostUsername}";
        if (_config.IsSimulationMode)
        {
            HostHandleLabel.Text = "@تجربة_محاكاة 🎮";
        }

        PrepareQuestions();
    }

    protected override bool OnBackButtonPressed()
    {
        // اعتراض زر الرجوع الخاص بالنظام لإظهار رسالة تأكيد الخروج
        _ = ConfirmAndExitAsync();
        return true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        TikTokLiveService.Instance.OnCommentReceived += HandleLiveComment;
        TikTokLiveService.Instance.OnStatusChanged += HandleStatusMessage;

        if (_config.IsSimulationMode)
        {
            WaitingLiveOverlay.IsVisible = false;
            await TikTokLiveService.Instance.ConnectAsync(_config.HostUsername, isSimulation: true);
            StartGame();
        }
        else
        {
            await CheckAndConnectLiveAsync();
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        _isLeaving = true;
        StopTimer();

        TikTokLiveService.Instance.OnCommentReceived -= HandleLiveComment;
        TikTokLiveService.Instance.OnStatusChanged -= HandleStatusMessage;
        await TikTokLiveService.Instance.DisconnectAsync();
    }

    private async Task CheckAndConnectLiveAsync()
    {
        WaitingLiveOverlay.IsVisible = true;
        WaitingIconLabel.Text = "📡";
        WaitingTitleLabel.Text = "جاري فحص حالة البث المباشر...";
        WaitingSubtitleLabel.Text = $"نتحقق من فتح البث على تيك توك لحساب @{_config.HostUsername}...";
        WaitingSpinner.IsRunning = true;
        WaitingSpinner.IsVisible = true;
        WaitingActionsLayout.IsVisible = false;

        var (isLive, roomId, msg) = await TikTokLiveService.Instance.CheckLiveStatusAsync(_config.HostUsername);

        if (isLive && !string.IsNullOrEmpty(roomId))
        {
            WaitingIconLabel.Text = "🟢";
            WaitingTitleLabel.Text = "تم اكتشاف البث المباشر!";
            WaitingSubtitleLabel.Text = "جاري الاتصال بالشات وبدء التحدي الآن...";
            WaitingSpinner.IsRunning = false;
            WaitingSpinner.IsVisible = false;

            await TikTokLiveService.Instance.ConnectAsync(_config.HostUsername, isSimulation: false);
            await Task.Delay(1400);

            if (!_isLeaving)
            {
                WaitingLiveOverlay.IsVisible = false;
                StartGame();
            }
        }
        else
        {
            WaitingIconLabel.Text = "⚠️";
            WaitingTitleLabel.Text = "البث المباشر غير نشط حالياً";
            WaitingSubtitleLabel.Text = $"لم نتمكن من العثور على بث مباشر مفتوح لحساب @{_config.HostUsername}.\nيرجى فتح البث (Go Live) في TikTok أولاً ثم الضغط على إعادة الفحص.";
            WaitingSpinner.IsRunning = false;
            WaitingSpinner.IsVisible = false;
            WaitingActionsLayout.IsVisible = true;
        }
    }

    private async void OnRetryCheckLiveClicked(object sender, EventArgs e)
    {
        await CheckAndConnectLiveAsync();
    }

    private async void OnSwitchToSimulationClicked(object sender, EventArgs e)
    {
        _config.IsSimulationMode = true;
        HostHandleLabel.Text = "@تجربة_محاكاة 🎮";
        WaitingLiveOverlay.IsVisible = false;
        await TikTokLiveService.Instance.ConnectAsync(_config.HostUsername, isSimulation: true);
        StartGame();
    }

    private void OnForceStartClicked(object sender, EventArgs e)
    {
        WaitingLiveOverlay.IsVisible = false;
        StartGame();
    }

    private void PrepareQuestions()
    {
        _questions.Clear();
        var allWords = TenWords.GetAllWords();
        var source = TenWords.GetMemorizedWords();
        if (source == null || source.Count == 0)
        {
            source = allWords.Count > 0 ? allWords : TenWords.All();
        }

        if (source.Count == 0) return;

        // خلط الكلمات واختيار العدد المطلوب
        var shuffled = new List<WordModel>(source);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        int count = _config.QuestionsCount > 0 ? Math.Min(_config.QuestionsCount, shuffled.Count) : shuffled.Count;
        var selected = shuffled.Take(count).ToList();

        var arabicPool = allWords.Select(w => w.ArabicWord).Where(w => !string.IsNullOrWhiteSpace(w)).Distinct().ToList();
        if (arabicPool.Count < 4)
        {
            arabicPool = source.Select(w => w.ArabicWord).Where(w => !string.IsNullOrWhiteSpace(w)).Distinct().ToList();
        }

        foreach (var word in selected)
        {
            var options = new List<string>(4) { word.ArabicWord };
            var wrongPool = arabicPool.Where(a => a != word.ArabicWord).ToList();

            int needed = Math.Min(3, wrongPool.Count);
            for (int i = 0; i < needed; i++)
            {
                int idx = Random.Shared.Next(wrongPool.Count);
                options.Add(wrongPool[idx]);
                wrongPool.RemoveAt(idx);
            }

            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (options[i], options[j]) = (options[j], options[i]);
            }

            _questions.Add(new QuestionItem
            {
                Word = word.EnglishWord,
                CorrectAnswer = word.ArabicWord,
                Options = options
            });
        }
    }

    private void StartGame()
    {
        _currentIndex = 0;
        LoadNextQuestion();
    }

    private async void LoadNextQuestion()
    {
        if (_isLeaving) return;

        if (_config.QuestionsCount > 0 && _currentIndex >= _config.QuestionsCount)
        {
            // انتهاء عدد الأسئلة المحدد للجولة
            ShowRoundCompletion();
            return;
        }

        if (_currentIndex >= _questions.Count)
        {
            // في حالة الوضع المستمر أو نفاد القائمة، نعيد توليد الأسئلة ونستمر
            PrepareQuestions();
            _currentIndex = 0;
        }

        // تصفير حالة الجولة
        _fastestUserThisRound = null;
        _votedUsersThisRound.Clear();
        Array.Clear(_votesCount, 0, _votesCount.Length);

        CelebrationBanner.IsVisible = false;
        ResetOptionCardVisuals();

        var currentQ = _questions[_currentIndex];

        if (_config.QuestionsCount == 0)
        {
            QuestionProgressLabel.Text = $"سؤال {_currentIndex + 1} ♾️";
        }
        else
        {
            QuestionProgressLabel.Text = $"سؤال {_currentIndex + 1} من {_config.QuestionsCount}";
        }

        EnglishWordLabel.Text = currentQ.Word;

        OptionText0.Text = currentQ.Options.ElementAtOrDefault(0) ?? "";
        OptionText1.Text = currentQ.Options.ElementAtOrDefault(1) ?? "";
        OptionText2.Text = currentQ.Options.ElementAtOrDefault(2) ?? "";
        OptionText3.Text = currentQ.Options.ElementAtOrDefault(3) ?? "";

        UpdateVoteBarsUI();

        // نطق الكلمة صوتياً للمشاهدين إذا كان الخيار مفعلاً
        if (_config.PronounceWord)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await TextToSpeech.Default.SpeakAsync(currentQ.Word, new SpeechOptions { Volume = 1.0f });
                }
                catch { }
            });
        }

        _isRoundActive = true;
        StartTimer();
    }

    private void StartTimer()
    {
        StopTimer();
        _timeLeft = _config.TimePerQuestion;
        TimerSecondsLabel.Text = _timeLeft.ToString();
        TimerProgressBar.Progress = 1.0;
        TimerProgressBar.ProgressColor = Color.FromArgb("#38BDF8");

        _roundTimer = new System.Timers.Timer(1000);
        _roundTimer.Elapsed += (s, e) =>
        {
            if (_isPaused || !_isRoundActive || _isLeaving) return;

            _timeLeft--;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_isLeaving) return;

                TimerSecondsLabel.Text = Math.Max(0, _timeLeft).ToString();
                double progress = Math.Max(0.0, (double)_timeLeft / _config.TimePerQuestion);

                if (_timeLeft <= 3)
                {
                    TimerProgressBar.ProgressColor = Color.FromArgb("#EF4444");
                    TimerSecondsLabel.TextColor = Color.FromArgb("#EF4444");
                }
                else
                {
                    TimerProgressBar.ProgressColor = Color.FromArgb("#38BDF8");
                    TimerSecondsLabel.TextColor = Color.FromArgb("#38BDF8");
                }

                try
                {
                    await TimerProgressBar.ProgressTo(progress, 600, Easing.Linear);
                }
                catch
                {
                    TimerProgressBar.Progress = progress;
                }

                if (_timeLeft <= 0)
                {
                    StopTimer();
                    OnRoundFinished();
                }
            });
        };
        _roundTimer.Start();
    }

    private void StopTimer()
    {
        if (_roundTimer != null)
        {
            _roundTimer.Stop();
            _roundTimer.Dispose();
            _roundTimer = null;
        }
    }

    private static string NormalizeArabic(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return text.Trim()
            .Replace("أ", "ا")
            .Replace("إ", "ا")
            .Replace("آ", "ا")
            .Replace("ة", "ه")
            .Replace("ى", "ي");
    }

    private void HandleLiveComment(TikTokLiveComment comment)
    {
        if (!_isRoundActive || _isPaused || _isLeaving) return;
        if (_currentIndex >= _questions.Count) return;

        var currentQ = _questions[_currentIndex];
        int choice = -1;
        if (comment.ChoiceIndex.HasValue)
        {
            choice = comment.ChoiceIndex.Value;
        }
        else
        {
            // مطابقة ذكية: إذا كتب المشاهد نص الكلمة نفسها بدلاً من رقمها
            string normComment = NormalizeArabic(comment.CommentText);

            for (int i = 0; i < currentQ.Options.Count; i++)
            {
                string normOpt = NormalizeArabic(currentQ.Options[i]);
                if (!string.IsNullOrEmpty(normOpt) && (normComment == normOpt || normComment.Contains(normOpt)))
                {
                    choice = i;
                    break;
                }
            }
        }

        if (choice < 0 || choice > 3) return;

        string user = comment.UserName;

        // منع التصويت المزدوج لنفس السؤال
        if (_votedUsersThisRound.Contains(user)) return;
        _votedUsersThisRound.Add(user);

        // تسجيل الصوت
        _votesCount[choice]++;

        string selectedAnswer = currentQ.Options.ElementAtOrDefault(choice) ?? "";
        bool isCorrect = (selectedAnswer == currentQ.CorrectAnswer);

        if (isCorrect)
        {
            if (!_scoresMap.TryGetValue(user, out var viewerScore))
            {
                viewerScore = new TikTokViewerScore
                {
                    UserName = user,
                    DisplayName = comment.DisplayName,
                    Score = 0
                };
                _scoresMap[user] = viewerScore;
            }

            if (_fastestUserThisRound == null)
            {
                // أسرع إجابة! (+3 نقاط)
                _fastestUserThisRound = user;
                viewerScore.Score += 3;
                viewerScore.FastestAnswersCount++;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CelebrationTextLabel.Text = $"أسرع إجابة: {user} (+3 نقاط) ⚡";
                    CelebrationBanner.IsVisible = true;
                });
            }
            else
            {
                // إجابة صحيحة تالية (+1 نقطة)
                viewerScore.Score += 1;
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateVoteBarsUI();
            UpdateLeaderboardUI();
            LastChatActivityLabel.Text = $"💬 {user} اختار [ {choice + 1} ]";
        });
    }

    private void UpdateVoteBarsUI()
    {
        int total = _votesCount.Sum();

        ProgressBar[] bars = [OptionProgress0, OptionProgress1, OptionProgress2, OptionProgress3];
        Label[] labels = [OptionVotesLabel0, OptionVotesLabel1, OptionVotesLabel2, OptionVotesLabel3];

        for (int i = 0; i < 4; i++)
        {
            int count = _votesCount[i];
            double ratio = total > 0 ? (double)count / total : 0.0;
            bars[i].Progress = ratio;
            labels[i].Text = total > 0 ? $"{count} ({(int)(ratio * 100)}%)" : "0";
        }
    }

    private void UpdateLeaderboardUI()
    {
        LeaderboardLayout.Children.Clear();

        var topViewers = _scoresMap.Values
            .OrderByDescending(v => v.Score)
            .ThenByDescending(v => v.FastestAnswersCount)
            .Take(5)
            .ToList();

        if (topViewers.Count == 0)
        {
            LeaderboardLayout.Children.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#0F172A"),
                Stroke = Color.FromArgb("#1E293B"),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(12, 8),
                Content = new Label
                {
                    Text = "بانتظار الإجابات الأولى من المشاهدين في الشات... ⏳",
                    FontSize = 12,
                    FontFamily = "CairoBold",
                    TextColor = Color.FromArgb("#64748B"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            });
            return;
        }

        int rank = 1;
        foreach (var v in topViewers)
        {
            v.Rank = rank++;
            var row = CreateLeaderboardRow(v);
            LeaderboardLayout.Children.Add(row);
        }
    }

    private static Border CreateLeaderboardRow(TikTokViewerScore v)
    {
        Color strokeColor;
        Color bgColor;
        Color scoreColor;

        switch (v.Rank)
        {
            case 1:
                strokeColor = Color.FromArgb("#F59E0B"); // Gold
                bgColor = Color.FromArgb("#241D12");
                scoreColor = Color.FromArgb("#FBBF24");
                break;
            case 2:
                strokeColor = Color.FromArgb("#CBD5E1"); // Silver
                bgColor = Color.FromArgb("#1E2430");
                scoreColor = Color.FromArgb("#E2E8F0");
                break;
            case 3:
                strokeColor = Color.FromArgb("#EA580C"); // Bronze
                bgColor = Color.FromArgb("#241712");
                scoreColor = Color.FromArgb("#FB923C");
                break;
            case 4:
                strokeColor = Color.FromArgb("#0284C7"); // Sky Blue
                bgColor = Color.FromArgb("#0F1E36");
                scoreColor = Color.FromArgb("#38BDF8");
                break;
            default: // 5
                strokeColor = Color.FromArgb("#6366F1"); // Indigo
                bgColor = Color.FromArgb("#161836");
                scoreColor = Color.FromArgb("#818CF8");
                break;
        }

        var border = new Border
        {
            BackgroundColor = bgColor,
            Stroke = strokeColor,
            StrokeThickness = 1.2,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(12, 5),
            HeightRequest = 42,
            HorizontalOptions = LayoutOptions.Fill
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto), // الرتبة
                new ColumnDefinition(GridLength.Star), // اسم الحساب
                new ColumnDefinition(GridLength.Auto)  // كبسولة النقاط
            },
            ColumnSpacing = 10,
            VerticalOptions = LayoutOptions.Center
        };

        // 1. ميدالية أو رقم الرتبة
        var rankLabel = new Label
        {
            Text = v.RankBadge,
            FontSize = 18,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(rankLabel, 0);
        grid.Children.Add(rankLabel);

        // 2. اسم حساب المتسابق (كبير، عريض، يملأ عرض الشاشة ولا ينقطع أبداً)
        var nameLabel = new Label
        {
            Text = v.UserName,
            FontSize = 14.5,
            FontFamily = "CairoBold",
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(nameLabel, 1);
        grid.Children.Add(nameLabel);

        // 3. كبسولة النقاط في أقصى الطرف الآخر
        var scoreBorder = new Border
        {
            BackgroundColor = Color.FromRgba(scoreColor.Red, scoreColor.Green, scoreColor.Blue, 0.2),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(9, 3),
            VerticalOptions = LayoutOptions.Center
        };

        scoreBorder.Content = new Label
        {
            Text = $"⭐ {v.Score} ن",
            FontSize = 12,
            FontFamily = "CairoBold",
            TextColor = scoreColor,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(scoreBorder, 2);
        grid.Children.Add(scoreBorder);

        border.Content = grid;
        return border;
    }

    private async void OnRoundFinished()
    {
        _isRoundActive = false;
        HighlightAnswers();

        try
        {
            // كشف الإجابة الصحيحة لمدة 4 ثوانٍ ليراها المشاهدون بوضوح
            await Task.Delay(4000);
        }
        catch { }

        if (!_isLeaving)
        {
            _currentIndex++;
            LoadNextQuestion();
        }
    }

    private void HighlightAnswers()
    {
        var currentQ = _questions[_currentIndex];
        Border[] cards = [OptionCard0, OptionCard1, OptionCard2, OptionCard3];

        for (int i = 0; i < 4; i++)
        {
            string opt = currentQ.Options.ElementAtOrDefault(i) ?? "";
            if (opt == currentQ.CorrectAnswer)
            {
                cards[i].BackgroundColor = Color.FromArgb("#065F46");
                cards[i].Stroke = Color.FromArgb("#10B981");
                cards[i].Opacity = 1.0;
            }
            else
            {
                cards[i].Opacity = 0.35;
            }
        }
    }

    private void ResetOptionCardVisuals()
    {
        Border[] cards = [OptionCard0, OptionCard1, OptionCard2, OptionCard3];
        foreach (var c in cards)
        {
            c.BackgroundColor = Color.FromArgb("#0F172A");
            c.Stroke = Color.FromArgb("#263B5E");
            c.Opacity = 1.0;
        }
    }

    private void OnOptionManualTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string param && int.TryParse(param, out int idx))
        {
            TikTokLiveService.Instance.DispatchComment("مقدم البث (أنت)", (idx + 1).ToString());
        }
    }

    private void OnSkipQuestionTapped(object sender, TappedEventArgs e)
    {
        StopTimer();
        OnRoundFinished();
    }

    private void OnTogglePauseTapped(object sender, TappedEventArgs e)
    {
        _isPaused = !_isPaused;
        PauseButtonLabel.Text = _isPaused ? "▶️" : "⏸️";
        LastChatActivityLabel.Text = _isPaused ? "⚠️ البث متوقف مؤقتاً" : "▶️ تم استئناف التحدي";
    }

    private async void OnExitTapped(object sender, TappedEventArgs e)
    {
        await ConfirmAndExitAsync();
    }

    private async Task ConfirmAndExitAsync()
    {
        bool confirm = await DisplayAlert("إنهاء التحدي المباشر", "هل تريد إنهاء جلسة البث والعودة؟", "نعم، خروج", "إلغاء");
        if (confirm)
        {
            _isLeaving = true;
            StopTimer();
            try
            {
                await Navigation.PopModalAsync();
            }
            catch
            {
                await Navigation.PopAsync();
            }
        }
    }

    private void ShowRoundCompletion()
    {
        _isRoundActive = false;
        StopTimer();

        var top = _scoresMap.Values.OrderByDescending(s => s.Score).FirstOrDefault();
        string winnerText = top != null ? $"🏆 بطل الجولة: {top.UserName} برصيد {top.Score} نقطة!" : "تمت جميع الأسئلة بنجاح!";

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await DisplayAlert("انتهت الجولة 🌟", winnerText, "حسناً");
            await ConfirmAndExitAsync();
        });
    }

    private void HandleStatusMessage(string msg)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LastChatActivityLabel.Text = msg;
        });
    }
}
