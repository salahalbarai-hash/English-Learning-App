using System.Text.Json;
using English.Models;
using English.Services;
using Microsoft.AspNetCore.SignalR.Client;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace English.Pages
{
    public partial class SpellReactorPage : ContentPage
    {
        private class FallingLetterOrb
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float VelX { get; set; }
            public float VelY { get; set; }
            public float Radius { get; set; } = 42f;
            public char Letter { get; set; }
            public bool IsActive { get; set; } = true;
            public SKColor GlowColor { get; set; } = SKColor.Parse("#38BDF8");
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

        private readonly GameSessionConfig _config;
        private readonly List<WordModel> _gameWords = new();
        private WordModel? _currentWord;
        private int _currentWordIndex = 0;
        private string _targetEnglishUpper = string.Empty;
        private int _currentLetterSlotIndex = 0;

        // خريطة عناصر واجهة XAML لخانات الحروف
        private readonly List<Border> _slotBorders = new();
        private readonly List<Label> _slotLabels = new();

        // كرات الحروف المتساقطة والجزيئات
        private readonly List<FallingLetterOrb> _orbs = new();
        private readonly List<SparkParticle> _particles = new();

        // حالة اللعبة
        private bool _isPlaying = false;
        private bool _isGameOver = false;
        private int _hearts = 3;
        private int _combo = 1;
        private int _sessionScore = 0;
        private int _completedWordsCount = 0;
        private IDispatcherTimer? _gameLoopTimer;

        // وضع تحدي الصديق والكوينز
        private bool _isMultiplayer = false;
        private HubConnection? _hubConnection;
        private string _roomName = string.Empty;
        private string _opponentName = string.Empty;
        private string _currentUserName = string.Empty;
        private int _opponentScore = 0;
        private bool _hasOpponentFinished = false;
        private bool _hasLocalFinished = false;
        private const int DuelStake = 4; // رهان 4 كوينز، الفائز يحصل على 8 كوينز

        // فرَش SkiaSharp مجهزة مسبقاً لأعلى أداء
        private readonly SKPaint _orbFillPaint = new() { Color = SKColor.Parse("#121D30"), Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _orbBorderPaint = new() { Color = SKColor.Parse("#38BDF8"), Style = SKPaintStyle.Stroke, StrokeWidth = 3f, IsAntialias = true };
        private readonly SKPaint _orbLetterPaint = new()
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            TextSize = 34f,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        private readonly SKPaint _particlePaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };

        public SpellReactorPage() : this(GameSessionConfig.FromMemorizedWords(TenWords.GetMemorizedWords(), GameDifficulty.Medium))
        {
        }

        public SpellReactorPage(GameSessionConfig config)
        {
            InitializeComponent();
            _config = config;
            _currentUserName = Preferences.Get("UserName", "أنا");

            _gameWords = _config.Words
                .Where(w => !string.IsNullOrWhiteSpace(w.EnglishWord) && !string.IsNullOrWhiteSpace(w.ArabicWord))
                .ToList();
        }

        public SpellReactorPage(HubConnection hubConnection, string roomName, string opponentName, string payloadJson)
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

            _gameWords = _config.Words
                .Where(w => !string.IsNullOrWhiteSpace(w.EnglishWord) && !string.IsNullOrWhiteSpace(w.ArabicWord))
                .ToList();

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
                            StopGame();
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
            StartGame();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopGame();
            CleanUpMultiplayer();
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

        private void StartGame()
        {
            StopGame();
            ResultOverlay.IsVisible = false;

            _isGameOver = false;
            _hasLocalFinished = false;
            _hearts = 3;
            _combo = 1;
            _sessionScore = 0;
            _completedWordsCount = 0;
            _currentWordIndex = 0;

            _orbs.Clear();
            _particles.Clear();

            UpdateHeartsUI();
            UpdateComboUI();
            SessionScoreLabel.Text = "0";

            LoadWordAndBuildSlots();

            _isPlaying = true;
            _gameLoopTimer = Dispatcher.CreateTimer();
            _gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16); // 60 FPS
            _gameLoopTimer.Tick += OnGameLoopTick;
            _gameLoopTimer.Start();
        }

        private void StopGame()
        {
            _isPlaying = false;
            if (_gameLoopTimer != null)
            {
                _gameLoopTimer.Stop();
                _gameLoopTimer = null;
            }
        }

        private void LoadWordAndBuildSlots()
        {
            if (_gameWords.Count == 0) return;

            _currentWord = _gameWords[_currentWordIndex % _gameWords.Count];
            _currentWordIndex++;

            TargetArabicWordLabel.Text = _currentWord.ArabicWord;
            _targetEnglishUpper = _currentWord.EnglishWord.Trim().ToUpperInvariant();
            _currentLetterSlotIndex = 0;

            int totalTargetWords = Math.Min(10, _gameWords.Count);
            WordProgressLabel.Text = $"{_completedWordsCount + 1} / {totalTargetWords}";

            LetterSlotsLayout.Children.Clear();
            _slotBorders.Clear();
            _slotLabels.Clear();

            for (int i = 0; i < _targetEnglishUpper.Length; i++)
            {
                char c = _targetEnglishUpper[i];

                if (char.IsWhiteSpace(c))
                {
                    var spaceBox = new BoxView { WidthRequest = 14, HeightRequest = 42, Color = Colors.Transparent };
                    LetterSlotsLayout.Children.Add(spaceBox);
                    continue;
                }

                var label = new Label
                {
                    Text = "_",
                    TextColor = Color.FromArgb("#64748B"),
                    FontSize = 18,
                    FontFamily = "CairoBold",
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                var border = new Border
                {
                    StrokeThickness = 1.5,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                    BackgroundColor = Color.FromArgb("#0E1726"),
                    Stroke = Color.FromArgb("#1E293B"),
                    WidthRequest = 42,
                    HeightRequest = 48,
                    Content = label
                };

                _slotBorders.Add(border);
                _slotLabels.Add(label);
                LetterSlotsLayout.Children.Add(border);
            }

            HighlightActiveSlot();
            SpawnOrbsForCurrentWord();
        }

        private void SpawnOrbsForCurrentWord()
        {
            _orbs.Clear();

            var lettersToSpawn = new List<char>();

            foreach (char c in _targetEnglishUpper)
            {
                if (char.IsLetter(c))
                    lettersToSpawn.Add(c);
            }

            int distractorsCount = _config.Difficulty switch
            {
                GameDifficulty.Easy => 2,
                GameDifficulty.Hard => 5,
                _ => 3
            };

            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            for (int i = 0; i < distractorsCount; i++)
            {
                char randomLetter = alphabet[Random.Shared.Next(alphabet.Length)];
                lettersToSpawn.Add(randomLetter);
            }

            lettersToSpawn = lettersToSpawn.OrderBy(_ => Guid.NewGuid()).ToList();

            float screenWidthEstimated = 1000f;
            float spacingX = screenWidthEstimated / (lettersToSpawn.Count + 1);

            for (int i = 0; i < lettersToSpawn.Count; i++)
            {
                float posX = spacingX * (i + 1) + (float)(Random.Shared.NextDouble() * 30 - 15);
                float posY = (float)(Random.Shared.NextDouble() * -300 - 60);
                float velY = (float)(Random.Shared.NextDouble() * 1.8 + 2.2);
                float velX = (float)(Random.Shared.NextDouble() * 1.4 - 0.7);

                _orbs.Add(new FallingLetterOrb
                {
                    X = posX,
                    Y = posY,
                    VelX = velX,
                    VelY = velY,
                    Radius = 44f,
                    Letter = lettersToSpawn[i],
                    GlowColor = SKColor.Parse("#38BDF8")
                });
            }
        }

        private void HighlightActiveSlot()
        {
            for (int i = 0; i < _slotBorders.Count; i++)
            {
                if (i == _currentLetterSlotIndex)
                {
                    _slotBorders[i].Stroke = Color.FromArgb("#38BDF8");
                    _slotBorders[i].BackgroundColor = Color.FromArgb("#162942");
                }
                else if (i < _currentLetterSlotIndex)
                {
                    _slotBorders[i].Stroke = Color.FromArgb("#10B981");
                    _slotBorders[i].BackgroundColor = Color.FromArgb("#064E3B");
                }
                else
                {
                    _slotBorders[i].Stroke = Color.FromArgb("#1E293B");
                    _slotBorders[i].BackgroundColor = Color.FromArgb("#0E1726");
                }
            }
        }

        private void OnGameLoopTick(object? sender, EventArgs e)
        {
            if (!_isPlaying || _isGameOver) return;

            float screenEstimatedW = 1000f;

            foreach (var orb in _orbs)
            {
                if (!orb.IsActive) continue;

                orb.X += orb.VelX;
                orb.Y += orb.VelY;

                if (orb.X - orb.Radius < 10)
                {
                    orb.X = orb.Radius + 10;
                    orb.VelX = -orb.VelX;
                }
                else if (orb.X + orb.Radius > screenEstimatedW - 10)
                {
                    orb.X = screenEstimatedW - orb.Radius - 10;
                    orb.VelX = -orb.VelX;
                }

                if (orb.Y > 1950)
                {
                    orb.Y = -80f;
                    orb.X = (float)(Random.Shared.NextDouble() * (screenEstimatedW - 100) + 50);
                }
            }

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.X += p.VelX;
                p.Y += p.VelY;
                p.Alpha -= 0.04f;
                if (p.Alpha <= 0)
                {
                    _particles.RemoveAt(i);
                }
            }

            ReactorCanvas.InvalidateSurface();
        }

        private void OnReactorPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColor.Parse("#080D1A"));

            foreach (var p in _particles)
            {
                _particlePaint.Color = new SKColor(p.Color.Red, p.Color.Green, p.Color.Blue, (byte)(p.Alpha * 255));
                canvas.DrawCircle(p.X, p.Y, p.Radius, _particlePaint);
            }

            foreach (var orb in _orbs)
            {
                if (!orb.IsActive) continue;

                _orbBorderPaint.Color = orb.GlowColor;
                canvas.DrawCircle(orb.X, orb.Y, orb.Radius, _orbFillPaint);
                canvas.DrawCircle(orb.X, orb.Y, orb.Radius, _orbBorderPaint);

                float textY = orb.Y + (_orbLetterPaint.TextSize / 3f);
                canvas.DrawText(orb.Letter.ToString(), orb.X, textY, _orbLetterPaint);
            }
        }

        private void OnReactorTouch(object sender, SKTouchEventArgs e)
        {
            if (e.ActionType == SKTouchAction.Pressed)
            {
                float touchX = e.Location.X;
                float touchY = e.Location.Y;

                HandleTouchOnCanvas(touchX, touchY);
                e.Handled = true;
            }
        }

        private void HandleTouchOnCanvas(float tx, float ty)
        {
            if (!_isPlaying || _isGameOver) return;

            FallingLetterOrb? hitOrb = null;
            for (int i = _orbs.Count - 1; i >= 0; i--)
            {
                var orb = _orbs[i];
                if (!orb.IsActive) continue;

                float dx = tx - orb.X;
                float dy = ty - orb.Y;
                if (dx * dx + dy * dy <= orb.Radius * orb.Radius * 1.5f)
                {
                    hitOrb = orb;
                    break;
                }
            }

            if (hitOrb != null)
            {
                ProcessLetterSelected(hitOrb);
            }
        }

        private void ProcessLetterSelected(FallingLetterOrb orb)
        {
            if (_currentLetterSlotIndex >= _slotLabels.Count) return;

            string lettersOnly = new string(_targetEnglishUpper.Where(char.IsLetter).ToArray());
            if (_currentLetterSlotIndex >= lettersOnly.Length) return;

            char expectedChar = lettersOnly[_currentLetterSlotIndex];

            if (char.ToUpperInvariant(orb.Letter) == expectedChar)
            {
                _ = Sounds.PlayAsync(Sounds.Correct());
                try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                SpawnSparks(orb.X, orb.Y, SKColor.Parse("#38BDF8"));
                SpawnSparks(orb.X, orb.Y, SKColor.Parse("#10B981"));

                _slotLabels[_currentLetterSlotIndex].Text = expectedChar.ToString();
                _slotLabels[_currentLetterSlotIndex].TextColor = Colors.White;

                orb.IsActive = false;
                _currentLetterSlotIndex++;

                _combo++;
                UpdateComboUI();

                int points = 10 * _combo;
                _sessionScore += points;
                SessionScoreLabel.Text = _sessionScore.ToString();

                if (_isMultiplayer && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    _ = _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, $"SCORE:{_sessionScore}");
                }

                HighlightActiveSlot();

                if (_currentLetterSlotIndex >= lettersOnly.Length)
                {
                    HandleWordFullySpelled();
                }
            }
            else
            {
                _ = Sounds.PlayAsync(Sounds.Wrong());
                SpawnSparks(orb.X, orb.Y, SKColor.Parse("#EF4444"));

                _combo = 1;
                UpdateComboUI();

                _hearts--;
                UpdateHeartsUI();

                if (_hearts <= 0)
                {
                    HandleGameOver();
                }
            }
        }

        private async void HandleWordFullySpelled()
        {
            _ = Sounds.PlayAsync(Sounds.Win());
            if (_currentWord != null)
            {
                _ = TextToSpeech.Default.SpeakAsync(_currentWord.EnglishWord);
            }

            _completedWordsCount++;

            foreach (var border in _slotBorders)
            {
                border.BackgroundColor = Color.FromArgb("#064E3B");
                border.Stroke = Color.FromArgb("#10B981");
            }

            await Task.Delay(500);

            int totalTargetWords = Math.Min(10, _gameWords.Count);
            if (_completedWordsCount >= totalTargetWords)
            {
                HandleGameVictory();
            }
            else
            {
                LoadWordAndBuildSlots();
            }
        }

        private void SpawnSparks(float x, float y, SKColor color)
        {
            for (int i = 0; i < 15; i++)
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
                    Radius = (float)(Random.Shared.NextDouble() * 4 + 2)
                });
            }
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
            _isGameOver = true;
            _hasLocalFinished = true;
            StopGame();
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
                    ResultTitleLabel.Text = "أنهيت الكلمات! ⚡";
                    ResultTitleLabel.TextColor = Color.FromArgb("#38BDF8");
                    ResultDescLabel.Text = "بانتظار إكمال الصديق للتهجئة لاحتساب النتيجة والكوينز...";
                    ResultWordsCountLabel.Text = $"{_completedWordsCount} 🧪";
                    ResultScoreLabel.Text = $"{_sessionScore} ⭐";
                    ResultOverlay.IsVisible = true;
                }
            }
            else
            {
                ResultIconLabel.Text = "⚛️";
                ResultTitleLabel.Text = "استقرار المفاعل! 🎉";
                ResultTitleLabel.TextColor = Color.FromArgb("#10B981");
                ResultDescLabel.Text = $"أتقنت تهجئة واسترجاع {_completedWordsCount} كلمات إنجليزية بدقة عالية.";
                ResultWordsCountLabel.Text = $"{_completedWordsCount} 🧪";
                ResultScoreLabel.Text = $"+{_sessionScore} ⭐";
                ResultOverlay.IsVisible = true;
            }
        }

        private void HandleGameOver()
        {
            _isGameOver = true;
            _hasLocalFinished = true;
            StopGame();
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
                    ResultWordsCountLabel.Text = $"{_completedWordsCount} 🧪";
                    ResultScoreLabel.Text = $"{_sessionScore} ⭐";
                    ResultOverlay.IsVisible = true;
                }
            }
            else
            {
                ResultIconLabel.Text = "💔";
                ResultTitleLabel.Text = "زيادة حرارة المفاعل!";
                ResultTitleLabel.TextColor = Color.FromArgb("#EF4444");
                ResultDescLabel.Text = "نفدت محاولات الاستقرار. ركّز على ترتيب حروف الكلمة وأعد المحاولة!";
                ResultWordsCountLabel.Text = $"{_completedWordsCount} 🧪";
                ResultScoreLabel.Text = $"{_sessionScore} ⭐";
                ResultOverlay.IsVisible = true;
            }
        }

        private void ShowFinalMultiplayerResult()
        {
            if (_sessionScore > _opponentScore)
            {
                AwardWinnerCoins(DuelStake * 2);
                ResultIconLabel.Text = "👑";
                ResultTitleLabel.Text = "فوز أسطوري بالتهجئة! 🏆";
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

            StopGame();
            CleanUpMultiplayer();
            await Navigation.PopModalAsync();
        }
    }
}
