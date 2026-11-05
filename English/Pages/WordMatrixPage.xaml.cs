using System.Diagnostics;
using System.Text.Json;
using English.Models;
using English.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace English.Pages
{
    public partial class WordMatrixPage : ContentPage
    {
        private class MatrixCardItem
        {
            public int PairId { get; set; }
            public string DisplayText { get; set; } = string.Empty;
            public bool IsEnglish { get; set; }
            public bool IsFlipped { get; set; }
            public bool IsMatched { get; set; }
            public Color AccentColor { get; set; } = Colors.Purple;
            public Border? CardBorder { get; set; }
            public Label? CardLabel { get; set; }
            public Label? HintIcon { get; set; }
        }

        private readonly GameSessionConfig _config;
        private readonly List<MatrixCardItem> _cards = new();
        private MatrixCardItem? _firstSelectedCard;
        private bool _isProcessingSelection = false;
        private bool _isObservingPhase = true;
        private int _observationSeconds = 4;
        private int _hearts = 3;
        private int _combo = 1;
        private int _sessionScore = 0;
        private int _totalPairsCount = 0;
        private int _matchedPairsCount = 0;
        private readonly Stopwatch _stopwatch = new();
        private IDispatcherTimer? _observationTimer;
        private bool _isDisposed = false;

        // وضع تحدي الصديق والكوينز
        private bool _isMultiplayer = false;
        private HubConnection? _hubConnection;
        private string _roomName = string.Empty;
        private string _opponentName = string.Empty;
        private string _currentUserName = string.Empty;
        private int _opponentScore = 0;
        private bool _hasOpponentFinished = false;
        private bool _hasLocalFinished = false;
        private const int DuelStake = 4; // 4 كوينز رهان، الفائز يحصل على 8 كوينز

        private static readonly Color[] PairColors =
        {
            Color.FromArgb("#38BDF8"), // فيروزي
            Color.FromArgb("#A855F7"), // بنفسجي
            Color.FromArgb("#F59E0B"), // كهرماني
            Color.FromArgb("#10B981"), // زمردي
            Color.FromArgb("#F43F5E"), // وردي
            Color.FromArgb("#6366F1")  // نيلي
        };

        public WordMatrixPage() : this(GameSessionConfig.FromMemorizedWords(TenWords.GetMemorizedWords(), GameDifficulty.Medium))
        {
        }

        public WordMatrixPage(GameSessionConfig config)
        {
            InitializeComponent();
            _config = config;
            _currentUserName = Preferences.Get("UserName", "أنا");
            ModeBadgeLabel.Text = _config.SourceTitle;
        }

        public WordMatrixPage(HubConnection hubConnection, string roomName, string opponentName, string payloadJson)
        {
            InitializeComponent();
            _hubConnection = hubConnection;
            _roomName = roomName;
            _opponentName = opponentName;
            _isMultiplayer = true;
            _currentUserName = Preferences.Get("UserName", "أنا");

            var words = new List<WordModel>();
            if (!string.IsNullOrEmpty(payloadJson))
            {
                try { words = JsonSerializer.Deserialize<List<WordModel>>(payloadJson) ?? new List<WordModel>(); } catch { }
            }
            if (words.Count == 0) words = TenWords.GetMemorizedWords();
            _config = GameSessionConfig.FromMemorizedWords(words, GameDifficulty.Medium);
            ModeBadgeLabel.Text = $"تحدي صديق ⚔️ ({_opponentName})";

            SetupMultiplayerDuel();
        }

        private void SetupMultiplayerDuel()
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
                                if (int.TryParse(msg.Substring("SCORE:".Length), out int opScore))
                                {
                                    _opponentScore = opScore;
                                }
                            }
                            else if (msg.StartsWith("FINISHED:"))
                            {
                                _hasOpponentFinished = true;
                                if (int.TryParse(msg.Substring("FINISHED:".Length), out int opScore))
                                {
                                    _opponentScore = opScore;
                                }
                                if (_hasLocalFinished)
                                {
                                    ShowFinalMultiplayerResult();
                                }
                            }
                        }
                    });
                });

                _hubConnection.On<string>("ReceiveDuelWithdrawal", (withdrawingUser) =>
                {
                    if (withdrawingUser == _opponentName)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            StopTimer();
                            AwardWinnerCoins(DuelStake * 2);
                            ResultIconLabel.Text = "⚠️";
                            ResultTitleLabel.Text = "انسحب الخصم من التحدي!";
                            ResultTitleLabel.TextColor = Color.FromArgb("#EF4444");
                            ResultDescLabel.Text = $"انسحب {_opponentName} وتم احتساب الفوز لصالحك ونيل 8 كوينز كاملة 🏆";
                            ResultScoreLabel.Text = "+8 💰";
                            ResultOverlay.IsVisible = true;
                        });
                    }
                });
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartNewGameRound();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isDisposed = true;
            StopTimer();
            CleanUpMultiplayer();
        }

        private void StopTimer()
        {
            if (_observationTimer != null)
            {
                _observationTimer.Stop();
                _observationTimer = null;
            }
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

        private void StartNewGameRound()
        {
            StopTimer();
            ResultOverlay.IsVisible = false;
            _cards.Clear();
            MatrixGrid.Children.Clear();
            MatrixGrid.RowDefinitions.Clear();
            MatrixGrid.ColumnDefinitions.Clear();

            _firstSelectedCard = null;
            _isProcessingSelection = false;
            _isObservingPhase = true;
            _hasLocalFinished = false;
            _hearts = 3;
            _combo = 1;
            _matchedPairsCount = 0;
            _sessionScore = 0;
            SessionScoreLabel.Text = "0";
            _stopwatch.Restart();

            UpdateHeartsUI();
            UpdateComboUI();

            int pairsToTake = _config.Difficulty switch
            {
                GameDifficulty.Easy => 3,
                GameDifficulty.Hard => 5,
                _ => 4
            };

            var selectedWords = _config.Words
                .Where(w => !string.IsNullOrWhiteSpace(w.EnglishWord) && !string.IsNullOrWhiteSpace(w.ArabicWord))
                .OrderBy(_ => Guid.NewGuid())
                .Take(pairsToTake)
                .ToList();

            if (selectedWords.Count < 2)
            {
                selectedWords = _config.Words.Take(2).ToList();
            }

            _totalPairsCount = selectedWords.Count;
            ProgressPairsLabel.Text = $"الأزواج المتبقية: {_totalPairsCount}";

            int pairIdCounter = 0;
            for (int i = 0; i < selectedWords.Count; i++)
            {
                var w = selectedWords[i];
                var color = PairColors[i % PairColors.Length];

                _cards.Add(new MatrixCardItem
                {
                    PairId = pairIdCounter,
                    DisplayText = w.EnglishWord,
                    IsEnglish = true,
                    IsFlipped = true,
                    AccentColor = color
                });

                _cards.Add(new MatrixCardItem
                {
                    PairId = pairIdCounter,
                    DisplayText = w.ArabicWord,
                    IsEnglish = false,
                    IsFlipped = true,
                    AccentColor = color
                });

                pairIdCounter++;
            }

            var shuffledCards = _cards.OrderBy(_ => Guid.NewGuid()).ToList();
            BuildMatrixGrid(shuffledCards);
            StartObservationPhase();
        }

        private void BuildMatrixGrid(List<MatrixCardItem> cards)
        {
            int total = cards.Count;
            int cols = 2; // عمودان عريضان دائماً لمنح البطاقات حجماً كبيراً ووضوحاً فائقاً
            int rows = (int)Math.Ceiling((double)total / cols);

            for (int r = 0; r < rows; r++)
                MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int c = 0; c < cols; c++)
                MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            for (int i = 0; i < cards.Count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                var card = cards[i];

                var border = CreateCardView(card);
                card.CardBorder = border;

                MatrixGrid.Add(border, c, r);
            }
        }

        private Border CreateCardView(MatrixCardItem card)
        {
            var border = new Border
            {
                StrokeThickness = 2,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
                BackgroundColor = Color.FromArgb("#162033"),
                Stroke = card.AccentColor,
                HeightRequest = 96, // حجم بطاقة كبير وبارز ومريح للضغط
                Padding = new Thickness(12, 10),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center
            };

            var grid = new Grid();

            var hintIcon = new Label
            {
                Text = card.IsEnglish ? "EN" : "AR",
                FontSize = 11,
                FontFamily = "CairoBold",
                TextColor = Color.FromArgb("#94A3B8"),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 0, 4, 0)
            };
            card.HintIcon = hintIcon;

            var label = new Label
            {
                Text = card.DisplayText,
                FontSize = card.DisplayText.Length > 12 ? 16 : 18.5,
                FontFamily = "CairoBold",
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.WordWrap
            };
            card.CardLabel = label;

            grid.Children.Add(hintIcon);
            grid.Children.Add(label);
            border.Content = grid;

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => OnCardTapped(card);
            border.GestureRecognizers.Add(tap);

            return border;
        }

        private void StartObservationPhase()
        {
            _isObservingPhase = true;
            _observationSeconds = 4;
            PhaseTitleLabel.Text = "مرحلة التشفير والملاحظة 👁️";
            PhaseSubLabel.Text = "احفظ مواقع الكلمات ومعانيها قبل انقلاب البطاقات!";
            TimerTextLabel.Text = $"{_observationSeconds}";

            _observationTimer = Dispatcher.CreateTimer();
            _observationTimer.Interval = TimeSpan.FromSeconds(1);
            _observationTimer.Tick += (s, e) =>
            {
                _observationSeconds--;
                if (_observationSeconds > 0)
                {
                    TimerTextLabel.Text = $"{_observationSeconds}";
                }
                else
                {
                    StopTimer();
                    TransitionToRetrievalPhase();
                }
            };
            _observationTimer.Start();
        }

        private async void TransitionToRetrievalPhase()
        {
            _isObservingPhase = false;
            PhaseTitleLabel.Text = "مرحلة الاسترجاع النشط ⚡";
            PhaseSubLabel.Text = "طابق كل كلمة إنجليزية بمعناها العربي من الذاكرة!";
            TimerTextLabel.Text = "⚡";

            var flipTasks = new List<Task>();
            foreach (var card in _cards)
            {
                if (card.CardBorder != null)
                {
                    flipTasks.Add(FlipCardDownAsync(card));
                }
            }
            await Task.WhenAll(flipTasks);
        }

        private async void OnCardTapped(MatrixCardItem card)
        {
            if (_isObservingPhase || _isProcessingSelection || _isDisposed)
                return;

            if (card.IsMatched || card.IsFlipped)
                return;

            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

            await FlipCardUpAsync(card);

            if (_firstSelectedCard == null)
            {
                _firstSelectedCard = card;
            }
            else
            {
                _isProcessingSelection = true;
                await CheckCardsMatchAsync(_firstSelectedCard, card);
                _firstSelectedCard = null;
                _isProcessingSelection = false;
            }
        }

        private async Task CheckCardsMatchAsync(MatrixCardItem card1, MatrixCardItem card2)
        {
            if (card1.PairId == card2.PairId && card1.IsEnglish != card2.IsEnglish)
            {
                card1.IsMatched = true;
                card2.IsMatched = true;

                _ = Sounds.PlayAsync(Sounds.Correct());
                try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                var speakWord = card1.IsEnglish ? card1.DisplayText : card2.DisplayText;
                _ = TextToSpeech.Default.SpeakAsync(speakWord);

                _combo++;
                UpdateComboUI();

                int points = 10 * _combo;
                _sessionScore += points;
                SessionScoreLabel.Text = _sessionScore.ToString();

                if (_isMultiplayer && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, $"SCORE:{_sessionScore}");
                }

                _matchedPairsCount++;
                int remaining = _totalPairsCount - _matchedPairsCount;
                ProgressPairsLabel.Text = $"الأزواج المتبقية: {remaining}";

                await Task.WhenAll(
                    AnimateCardMatchSuccess(card1),
                    AnimateCardMatchSuccess(card2)
                );

                if (_matchedPairsCount >= _totalPairsCount)
                {
                    HandleGameVictory();
                }
            }
            else
            {
                _ = Sounds.PlayAsync(Sounds.Wrong());
                _combo = 1;
                UpdateComboUI();

                _hearts--;
                UpdateHeartsUI();

                await Task.WhenAll(
                    AnimateCardMismatchError(card1),
                    AnimateCardMismatchError(card2)
                );

                await Task.Delay(400);

                await Task.WhenAll(
                    FlipCardDownAsync(card1),
                    FlipCardDownAsync(card2)
                );

                if (_hearts <= 0)
                {
                    HandleGameOver();
                }
            }
        }

        private async Task FlipCardUpAsync(MatrixCardItem card)
        {
            if (card.CardBorder == null) return;
            card.IsFlipped = true;

            await card.CardBorder.RotateYTo(90, 120, Easing.CubicIn);

            card.CardBorder.BackgroundColor = Color.FromArgb("#1E293B");
            card.CardBorder.Stroke = card.AccentColor;
            if (card.CardLabel != null)
            {
                card.CardLabel.Text = card.DisplayText;
                card.CardLabel.FontSize = card.DisplayText.Length > 12 ? 16 : 18.5;
                card.CardLabel.TextColor = Colors.White;
            }
            if (card.HintIcon != null)
            {
                card.HintIcon.IsVisible = true;
            }

            await card.CardBorder.RotateYTo(0, 120, Easing.CubicOut);
        }

        private async Task FlipCardDownAsync(MatrixCardItem card)
        {
            if (card.CardBorder == null) return;
            card.IsFlipped = false;

            await card.CardBorder.RotateYTo(90, 120, Easing.CubicIn);

            card.CardBorder.BackgroundColor = Color.FromArgb("#0D1525");
            card.CardBorder.Stroke = Color.FromArgb("#243247");
            if (card.CardLabel != null)
            {
                card.CardLabel.Text = "❓";
                card.CardLabel.FontSize = 24;
                card.CardLabel.TextColor = Color.FromArgb("#475569");
            }
            if (card.HintIcon != null)
            {
                card.HintIcon.IsVisible = false;
            }

            await card.CardBorder.RotateYTo(0, 120, Easing.CubicOut);
        }

        private async Task AnimateCardMatchSuccess(MatrixCardItem card)
        {
            if (card.CardBorder == null) return;
            card.CardBorder.BackgroundColor = Color.FromArgb("#064E3B");
            card.CardBorder.Stroke = Color.FromArgb("#10B981");

            await card.CardBorder.ScaleTo(1.08, 150, Easing.CubicOut);
            await card.CardBorder.ScaleTo(1.0, 150, Easing.CubicIn);
        }

        private async Task AnimateCardMismatchError(MatrixCardItem card)
        {
            if (card.CardBorder == null) return;
            card.CardBorder.BackgroundColor = Color.FromArgb("#450A0A");
            card.CardBorder.Stroke = Color.FromArgb("#EF4444");

            await card.CardBorder.TranslateTo(-8, 0, 50);
            await card.CardBorder.TranslateTo(8, 0, 50);
            await card.CardBorder.TranslateTo(0, 0, 50);
        }

        private void UpdateHeartsUI()
        {
            Heart1Label.Opacity = _hearts >= 1 ? 1.0 : 0.2;
            Heart2Label.Opacity = _hearts >= 2 ? 1.0 : 0.2;
            Heart3Label.Opacity = _hearts >= 3 ? 1.0 : 0.2;
        }

        private void UpdateComboUI()
        {
            ComboLabel.Text = $"x{_combo}";
        }

        private void HandleGameVictory()
        {
            _stopwatch.Stop();
            _hasLocalFinished = true;
            _ = Sounds.PlayAsync(Sounds.Win());

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
                    ResultIconLabel.Text = "⏳";
                    ResultTitleLabel.Text = "أنهيت مصفوفة الذاكرة! 🧠";
                    ResultTitleLabel.TextColor = Color.FromArgb("#38BDF8");
                    ResultDescLabel.Text = "بانتظار إكمال الصديق للتحدي لحسم النتيجة والكوينز...";
                    ResultScoreLabel.Text = $"{_sessionScore} ⭐";
                    ResultTimeLabel.Text = $"{_stopwatch.Elapsed.Seconds}s ⏱️";
                    ResultOverlay.IsVisible = true;
                }
            }
            else
            {
                ResultIconLabel.Text = "🏆";
                ResultTitleLabel.Text = "ذاكرة خارقة! 🎉";
                ResultTitleLabel.TextColor = Color.FromArgb("#10B981");
                ResultDescLabel.Text = $"أتقنت استرجاع {_totalPairsCount} أزواج في {_stopwatch.Elapsed.Seconds} ثانية فقط.";
                ResultScoreLabel.Text = $"+{_sessionScore} ⭐";
                ResultTimeLabel.Text = $"{_stopwatch.Elapsed.Seconds}s ⏱️";
                ResultOverlay.IsVisible = true;
            }
        }

        private void HandleGameOver()
        {
            _stopwatch.Stop();
            _hasLocalFinished = true;
            _ = Sounds.PlayAsync(Sounds.GameOver());

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
                    ResultIconLabel.Text = "⏳";
                    ResultTitleLabel.Text = "انتهت محاولاتك!";
                    ResultTitleLabel.TextColor = Color.FromArgb("#EF4444");
                    ResultDescLabel.Text = "بانتظار إكمال الصديق لاحتساب النتيجة والكوينز...";
                    ResultScoreLabel.Text = $"{_sessionScore} ⭐";
                    ResultTimeLabel.Text = $"{_stopwatch.Elapsed.Seconds}s ⏱️";
                    ResultOverlay.IsVisible = true;
                }
            }
            else
            {
                ResultIconLabel.Text = "💔";
                ResultTitleLabel.Text = "انتهت المحاولات!";
                ResultTitleLabel.TextColor = Color.FromArgb("#EF4444");
                ResultDescLabel.Text = "لا بأس! حاول مجدداً وراقب مواضع الكلمات بتركيز أكبر.";
                ResultScoreLabel.Text = $"{_sessionScore} ⭐";
                ResultTimeLabel.Text = $"{_stopwatch.Elapsed.Seconds}s ⏱️";
                ResultOverlay.IsVisible = true;
            }
        }

        private void ShowFinalMultiplayerResult()
        {
            if (_sessionScore > _opponentScore)
            {
                AwardWinnerCoins(DuelStake * 2);
                ResultIconLabel.Text = "👑";
                ResultTitleLabel.Text = "فوز أسطوري بالذاكرة! 🏆";
                ResultTitleLabel.TextColor = Color.FromArgb("#10B981");
                ResultDescLabel.Text = $"تفوقت على {_opponentName} وحصدت 8 كوينز ذهبي كاملة!";
                ResultScoreLabel.Text = "+8 💰";
            }
            else if (_sessionScore < _opponentScore)
            {
                ResultIconLabel.Text = "💔";
                ResultTitleLabel.Text = "هاردلك! فاز الصديق";
                ResultTitleLabel.TextColor = Color.FromArgb("#EF4444");
                ResultDescLabel.Text = $"سجل {_opponentName} نقاطاً أعلى. حظاً أوفر في التحدي القادم!";
                ResultScoreLabel.Text = $"{_sessionScore} ⭐";
            }
            else
            {
                AwardWinnerCoins(DuelStake);
                ResultIconLabel.Text = "🤝";
                ResultTitleLabel.Text = "تعادل متكافئ! ⚖️";
                ResultTitleLabel.TextColor = Color.FromArgb("#38BDF8");
                ResultDescLabel.Text = "تساوت النقاط وتم استرداد 4 كوينز ذهبي لرصيدك.";
                ResultScoreLabel.Text = "+4 💰";
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

        private void OnPlayAgainClicked(object sender, EventArgs e)
        {
            StartNewGameRound();
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
            if (_isMultiplayer && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                bool confirm = await DisplayAlert("تأكيد الانسحاب ⚠️", "هل تريد الانسحاب؟ سيتم احتساب فوز للخصم.", "انسحب", "إلغاء");
                if (!confirm) return;

                try
                {
                    await _hubConnection.InvokeAsync("SendDuelWithdrawal", _roomName, _currentUserName);
                }
                catch { }
            }

            StopTimer();
            CleanUpMultiplayer();
            await Navigation.PopModalAsync();
        }
    }
}
