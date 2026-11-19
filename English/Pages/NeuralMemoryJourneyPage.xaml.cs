using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Maui.Alerts;
using English.Models;
using English.Services;

namespace English.Pages
{
    public partial class NeuralMemoryJourneyPage : ContentPage
    {
        private enum JourneyState
        {
            Station1_Encoding,
            Station2_Reflex,
            Station3_Spelling,
            Station4_MineSweeper,
            Station5_DualMirror,
            Station6_GoldenVault,
            Victory
        }

        private class ReflexQuestion
        {
            public WordModel TargetWord { get; set; } = new();
            public List<string> Options { get; set; } = new();
            public string CorrectOption { get; set; } = string.Empty;
        }

        private class GateQuestion
        {
            public WordModel TargetWord { get; set; } = new();
            public string PromptType { get; set; } = "Meaning"; // Meaning, Audio, Spelling
            public string QuestionText { get; set; } = string.Empty;
            public List<string> Options { get; set; } = new();
            public string CorrectOption { get; set; } = string.Empty;
        }

        private class MineTileInfo
        {
            public char Letter { get; set; }
            public bool IsMine { get; set; }
            public Border TileBorder { get; set; } = null!;
            public Label TileLabel { get; set; } = null!;
        }

        private class VaultSlotInfo
        {
            public int SlotIndex { get; set; }
            public WordModel Word { get; set; } = new();
            public bool IsUnlocked { get; set; }
            public Border SlotBorder { get; set; } = null!;
            public Label StatusIconLabel { get; set; } = null!;
            public Label TitleLabel { get; set; } = null!;
            public Label SubLabel { get; set; } = null!;
            public Border SpeakerBorder { get; set; } = null!;
        }

        // --- البيانات العامة ---
        private readonly List<WordModel> _allSessionWords = new();
        private List<WordModel> _selectedWords = new();

        // --- نظام التقسيم الذكي للأفواج (Micro-Batching) ---
        private List<List<WordModel>> _batches = new();
        private int _currentBatchIndex = 0;
        private List<WordModel> CurrentBatch => _batches.Count > _currentBatchIndex ? _batches[_currentBatchIndex] : new();

        // --- مؤقتات التحكم ---
        private IDispatcherTimer? _reflexTimer;
        private int _reflexTimeTicks = 0;
        private const int ReflexTotalTicks = 18; // 1.8 ثانية خاطفة بمعدل 100ms
        private bool _isProcessingAnswer = false;

        // --- مؤقت بومودورو المحطة 1 (دقيقتان لكل كلمة) ---
        private IDispatcherTimer? _flashcardPomodoroTimer;
        private int _flashcardRemainingSeconds = 120;
        private const int FlashcardPomodoroTotalSeconds = 120;

        // --- حالة المحطة 1: Flashcards & Matching ---
        private int _flashcardIndex = 0;
        private Border? _selectedEnglishTile;
        private WordModel? _selectedMatchWord;
        private int _matchedPairsCount = 0;

        // --- حالة المحطة 2: Reflex Radar ---
        private List<ReflexQuestion> _reflexQuestions = new();
        private int _reflexQuestionIndex = 0;
        private int _reflexScore = 0;

        // --- حالة المحطة 3: Spelling Builder ---
        private int _spellingWordIndex = 0;
        private WordModel? _currentSpellingWord;
        private readonly List<char> _placedLetters = new();
        private readonly List<Border> _placedLetterBorders = new();

        // --- حالة المحطة 4: Mine Sweeper كاسحة الألغام ---
        private int _mineWordIndex = 0;
        private WordModel? _currentMineWord;
        private int _remainingMinesCount = 0;
        private readonly List<MineTileInfo> _currentMineTiles = new();

        // --- حالة المحطة 5: Gatekeeper & Dual Mirror ---
        private List<GateQuestion> _gateQuestions = new();
        private int _gateQuestionIndex = 0;
        private readonly List<WordModel> _repairWordsQueue = new();
        private bool _isInRepairMode = false;

        // --- حالة المحطة 6: The Golden Vault خزنة التسميع الغيبي ---
        private readonly List<VaultSlotInfo> _vaultSlots = new();
        private bool _isListeningVault = false;

        public NeuralMemoryJourneyPage(List<WordModel> sessionWords)
        {
            InitializeComponent();

            _allSessionWords = sessionWords
                .Where(w => !string.IsNullOrWhiteSpace(w.EnglishWord) && !string.IsNullOrWhiteSpace(w.ArabicWord))
                .Take(6)
                .ToList();

            // مباشرة الكلمات الـ 6 كاملة دون شاشة اختيار
            _selectedWords = _allSessionWords;
            _batches = new List<List<WordModel>> { _selectedWords };
            _currentBatchIndex = 0;

            StartStation1();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopAllTimers();
            SpeechRecognitionService.CancelListening();
        }

        private void StopAllTimers()
        {
            if (_reflexTimer != null)
            {
                _reflexTimer.Stop();
                _reflexTimer = null;
            }
            if (_flashcardPomodoroTimer != null)
            {
                _flashcardPomodoroTimer.Stop();
                _flashcardPomodoroTimer = null;
            }
        }

        // ============================================================
        // 🔄 محرك التحكم بالحالات المتدفقة (Single-Screen State Machine)
        // ============================================================

        private void ShowState(JourneyState state)
        {
            Station1View.IsVisible = (state == JourneyState.Station1_Encoding);
            Station2View.IsVisible = (state == JourneyState.Station2_Reflex);
            Station3View.IsVisible = (state == JourneyState.Station3_Spelling);
            Station4View.IsVisible = (state == JourneyState.Station4_MineSweeper);
            Station5View.IsVisible = (state == JourneyState.Station5_DualMirror);
            Station6View.IsVisible = (state == JourneyState.Station6_GoldenVault);
            VictoryView.IsVisible = (state == JourneyState.Victory);

            bool inJourney = (state != JourneyState.Victory);
            StepperContainer.IsVisible = inJourney;
            BatchBadge.IsVisible = false;

            UpdateStepperBadges(state);
        }

        private void UpdateStepperBadges(JourneyState state)
        {
            ResetBadge(Step1Border, false, false);
            ResetBadge(Step2Border, false, false);
            ResetBadge(Step3Border, false, false);
            ResetBadge(Step4Border, false, false);
            ResetBadge(Step5Border, false, false);
            ResetBadge(Step6Border, false, false);

            switch (state)
            {
                case JourneyState.Station1_Encoding:
                    ResetBadge(Step1Border, true, false);
                    JourneyTitleLabel.Text = "المحطة 1: البصمة الصوتية 🎧";
                    break;
                case JourneyState.Station2_Reflex:
                    ResetBadge(Step1Border, false, true);
                    ResetBadge(Step2Border, true, false);
                    JourneyTitleLabel.Text = "المحطة 2: رادار السرعة ⚡";
                    break;
                case JourneyState.Station3_Spelling:
                    ResetBadge(Step1Border, false, true);
                    ResetBadge(Step2Border, false, true);
                    ResetBadge(Step3Border, true, false);
                    JourneyTitleLabel.Text = "المحطة 3: قناص الحروف 🧩";
                    break;
                case JourneyState.Station4_MineSweeper:
                    ResetBadge(Step1Border, false, true);
                    ResetBadge(Step2Border, false, true);
                    ResetBadge(Step3Border, false, true);
                    ResetBadge(Step4Border, true, false);
                    JourneyTitleLabel.Text = "المحطة 4: كاسحة الألغام 💣";
                    break;
                case JourneyState.Station5_DualMirror:
                    ResetBadge(Step1Border, false, true);
                    ResetBadge(Step2Border, false, true);
                    ResetBadge(Step3Border, false, true);
                    ResetBadge(Step4Border, false, true);
                    ResetBadge(Step5Border, true, false);
                    JourneyTitleLabel.Text = "المحطة 5: بوابة الإتقان 🪞";
                    break;
                case JourneyState.Station6_GoldenVault:
                    ResetBadge(Step1Border, false, true);
                    ResetBadge(Step2Border, false, true);
                    ResetBadge(Step3Border, false, true);
                    ResetBadge(Step4Border, false, true);
                    ResetBadge(Step5Border, false, true);
                    ResetBadge(Step6Border, true, false);
                    JourneyTitleLabel.Text = "المحطة 6: التسميع الغيبي 🎙️";
                    break;
                default:
                    JourneyTitleLabel.Text = "رحلة الحفظ العصبي 🧠";
                    break;
            }
        }

        private static void ResetBadge(Border border, bool isActive, bool isCompleted)
        {
            if (isActive)
            {
                border.BackgroundColor = Color.FromArgb("#0284C7");
                border.Stroke = Color.FromArgb("#38BDF8");
            }
            else if (isCompleted)
            {
                border.BackgroundColor = Color.FromArgb("#065F46");
                border.Stroke = Color.FromArgb("#10B981");
            }
            else
            {
                border.BackgroundColor = Color.FromArgb("#1E293B");
                border.Stroke = Color.FromArgb("#334155");
            }
        }

        // ============================================================
        // 🎧 المحطة 1: البصمة الصوتية والتشفير (Encoding & Match)
        // ============================================================

        private void StartStation1()
        {
            ShowState(JourneyState.Station1_Encoding);
            _flashcardIndex = 0;
            Station1_FlashcardContainer.IsVisible = true;
            Station1_MatchingContainer.IsVisible = false;

            Station1HeaderTitle.Text = "المحطة 1: البصمة الصوتية والتشفير 🎧";
            Station1HeaderSub.Text = "ركز مع الكلمة لدقيقتين لتثبيتها في الذاكرة، أو اضغط حفظتها مبكراً.";

            DisplayFlashcard(CurrentBatch[_flashcardIndex]);
        }

        private void DisplayFlashcard(WordModel word)
        {
            FlashcardIndexLabel.Text = $"الكلمة {_flashcardIndex + 1} من {CurrentBatch.Count}";
            FlashcardEnglishLabel.Text = word.EnglishWord;
            FlashcardSyllablesLabel.Text = FormatSyllables(word.EnglishWord);
            FlashcardArabicLabel.Text = word.ArabicWord;

            // نطق تلقائي فوري للبصمة الصوتية
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(250);
                    await TextToSpeech.Default.SpeakAsync(word.EnglishWord);
                }
                catch { }
            });

            // تشغيل مؤقت البومودورو لدقيقتين (120 ثانية)
            StartFlashcardPomodoroTimer();
        }

        private void StartFlashcardPomodoroTimer()
        {
            _flashcardPomodoroTimer?.Stop();
            _flashcardRemainingSeconds = FlashcardPomodoroTotalSeconds;
            UpdateFlashcardTimerUI();

            _flashcardPomodoroTimer = Dispatcher.CreateTimer();
            _flashcardPomodoroTimer.Interval = TimeSpan.FromSeconds(1);
            _flashcardPomodoroTimer.Tick += OnFlashcardTimerTick;
            _flashcardPomodoroTimer.Start();
        }

        private void OnFlashcardTimerTick(object? sender, EventArgs e)
        {
            _flashcardRemainingSeconds--;
            if (_flashcardRemainingSeconds < 0)
                _flashcardRemainingSeconds = 0;

            UpdateFlashcardTimerUI();

            if (_flashcardRemainingSeconds <= 0)
            {
                _flashcardPomodoroTimer?.Stop();
                _flashcardPomodoroTimer = null;
                AdvanceFlashcard();
            }
        }

        private void UpdateFlashcardTimerUI()
        {
            int mins = _flashcardRemainingSeconds / 60;
            int secs = _flashcardRemainingSeconds % 60;
            FlashcardPomodoroTimerLabel.Text = $"{mins:D2}:{secs:D2}";
            FlashcardPomodoroProgressBar.Progress = (double)_flashcardRemainingSeconds / FlashcardPomodoroTotalSeconds;

            if (_flashcardRemainingSeconds <= 15)
            {
                FlashcardPomodoroProgressBar.ProgressColor = Color.FromArgb("#F43F5E");
                FlashcardPomodoroTimerLabel.TextColor = Color.FromArgb("#FB7185");
            }
            else
            {
                FlashcardPomodoroProgressBar.ProgressColor = Color.FromArgb("#38BDF8");
                FlashcardPomodoroTimerLabel.TextColor = Color.FromArgb("#38BDF8");
            }
        }

        private static string FormatSyllables(string word)
        {
            if (string.IsNullOrWhiteSpace(word) || word.Length <= 4)
                return word;

            // تقسيم تقريبي ناعم للمقاطع الصوتية
            var result = new List<string>();
            int chunkSize = word.Length > 7 ? 3 : 2;
            for (int i = 0; i < word.Length; i += chunkSize)
            {
                int len = Math.Min(chunkSize, word.Length - i);
                result.Add(word.Substring(i, len));
            }
            return string.Join(" · ", result);
        }

        private async void OnReplayFlashcardAudioTapped(object sender, EventArgs e)
        {
            if (CurrentBatch.Count > _flashcardIndex)
            {
                try { await TextToSpeech.Default.SpeakAsync(CurrentBatch[_flashcardIndex].EnglishWord); } catch { }
            }
        }

        private void OnNextFlashcardClicked(object sender, EventArgs e)
        {
            AdvanceFlashcard();
        }

        private void AdvanceFlashcard()
        {
            _flashcardPomodoroTimer?.Stop();
            _flashcardPomodoroTimer = null;

            _flashcardIndex++;
            if (_flashcardIndex < CurrentBatch.Count)
            {
                DisplayFlashcard(CurrentBatch[_flashcardIndex]);
            }
            else
            {
                // الانتقال إلى خطوة المطابقة الهادئة (Phase 1B)
                StartStation1_Matching();
            }
        }

        private void StartStation1_Matching()
        {
            _flashcardPomodoroTimer?.Stop();
            _flashcardPomodoroTimer = null;

            Station1_FlashcardContainer.IsVisible = false;
            Station1_MatchingContainer.IsVisible = true;

            Station1HeaderTitle.Text = "المحطة 1: مطابقة الرابط السمعي 🔗";
            Station1HeaderSub.Text = "المس الكلمة لسماعها ثم طابقها بمعناها بدون مؤقت أو توتر.";

            _selectedEnglishTile = null;
            _selectedMatchWord = null;
            _matchedPairsCount = 0;

            MatchEnglishCol.Children.Clear();
            MatchArabicCol.Children.Clear();

            // خلط عشوائي منفصل للعمودين
            var shuffledEnglish = CurrentBatch.OrderBy(_ => Guid.NewGuid()).ToList();
            var shuffledArabic = CurrentBatch.OrderBy(_ => Guid.NewGuid()).ToList();

            foreach (var w in shuffledEnglish)
            {
                MatchEnglishCol.Children.Add(CreateMatchTile(w, isEnglish: true));
            }

            foreach (var w in shuffledArabic)
            {
                MatchArabicCol.Children.Add(CreateMatchTile(w, isEnglish: false));
            }
        }

        private Border CreateMatchTile(WordModel word, bool isEnglish)
        {
            var border = new Border
            {
                Stroke = Color.FromArgb("#25364E"),
                StrokeThickness = 1.5,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
                BackgroundColor = Color.FromArgb("#0E1626"),
                Padding = new Thickness(16, 16),
                MinimumHeightRequest = 62,
                HeightRequest = 64,
                HorizontalOptions = LayoutOptions.Fill
            };

            var label = new Label
            {
                Text = isEnglish ? word.EnglishWord : word.ArabicWord,
                FontSize = isEnglish ? 17 : 16,
                FontFamily = "CairoBold",
                TextColor = isEnglish ? Color.FromArgb("#38BDF8") : Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            border.Content = label;

            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    if (isEnglish)
                    {
                        // نطق الكلمة عند الضغط عليها
                        _ = TextToSpeech.Default.SpeakAsync(word.EnglishWord);

                        // إلغاء تحديد البلاطة السابقة إن وجدت
                        if (_selectedEnglishTile != null)
                        {
                            _selectedEnglishTile.Stroke = Color.FromArgb("#25364E");
                            _selectedEnglishTile.BackgroundColor = Color.FromArgb("#0E1626");
                            _selectedEnglishTile.StrokeThickness = 1.5;
                        }

                        _selectedEnglishTile = border;
                        _selectedMatchWord = word;

                        border.Stroke = Color.FromArgb("#38BDF8");
                        border.BackgroundColor = Color.FromArgb("#163252");
                        border.StrokeThickness = 2;
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
                    }
                    else
                    {
                        // اختيار المعنى العربي لمطابقته مع الإنجليزي المحدد
                        if (_selectedMatchWord == null || _selectedEnglishTile == null)
                        {
                            await Toast.Make("المس الكلمة الإنجليزية أولاً لسماعها 🎧").Show();
                            return;
                        }

                        if (_selectedMatchWord.EnglishWord == word.EnglishWord)
                        {
                            // تطابق صحيح! 🌟
                            _ = Sounds.PlayAsync(Sounds.Correct());
                            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                            _selectedEnglishTile.BackgroundColor = Color.FromArgb("#064E3B");
                            _selectedEnglishTile.Stroke = Color.FromArgb("#10B981");
                            _selectedEnglishTile.StrokeThickness = 2;
                            _selectedEnglishTile.IsEnabled = false;

                            border.BackgroundColor = Color.FromArgb("#064E3B");
                            border.Stroke = Color.FromArgb("#10B981");
                            border.StrokeThickness = 2;
                            border.IsEnabled = false;

                            _selectedEnglishTile = null;
                            _selectedMatchWord = null;
                            _matchedPairsCount++;

                            if (_matchedPairsCount >= CurrentBatch.Count)
                            {
                                await Task.Delay(600);
                                StartStation2();
                            }
                        }
                        else
                        {
                            // خطأ لطيف
                            _ = Sounds.PlayAsync(Sounds.Wrong());
                            border.Stroke = Color.FromArgb("#EF4444");
                            await border.ScaleTo(1.05, 120);
                            await border.ScaleTo(1.0, 120);
                            border.Stroke = Color.FromArgb("#25364E");
                        }
                    }
                })
            });

            return border;
        }

        // ============================================================
        // ⚡ المحطة 2: رادار السرعة والبديهة (Speed Reflex)
        // ============================================================

        private void StartStation2()
        {
            ShowState(JourneyState.Station2_Reflex);
            _reflexScore = 0;
            ReflexScoreLabel.Text = "⭐ 0";

            // توليد أسئلة رادار سريعة (سؤالان لكل كلمة في الدفعة)
            _reflexQuestions = new();
            foreach (var w in CurrentBatch)
            {
                _reflexQuestions.Add(CreateReflexQuestion(w));
                _reflexQuestions.Add(CreateReflexQuestion(w));
            }
            _reflexQuestions = _reflexQuestions.OrderBy(_ => Guid.NewGuid()).ToList();
            _reflexQuestionIndex = 0;

            DisplayNextReflexQuestion();
        }

        private ReflexQuestion CreateReflexQuestion(WordModel target)
        {
            var distractors = _allSessionWords
                .Where(w => w.EnglishWord != target.EnglishWord)
                .OrderBy(_ => Guid.NewGuid())
                .Take(2)
                .Select(w => w.EnglishWord)
                .ToList();

            var options = new List<string> { target.EnglishWord };
            options.AddRange(distractors);
            options = options.OrderBy(_ => Guid.NewGuid()).ToList();

            return new ReflexQuestion
            {
                TargetWord = target,
                Options = options,
                CorrectOption = target.EnglishWord
            };
        }

        private void DisplayNextReflexQuestion()
        {
            StopAllTimers();
            _isProcessingAnswer = false;

            if (_reflexQuestionIndex >= _reflexQuestions.Count)
            {
                // انتهت جولة الرادار بنجاح
                _ = Sounds.PlayAsync(Sounds.Win());
                Task.Delay(700).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(StartStation3));
                return;
            }

            var q = _reflexQuestions[_reflexQuestionIndex];
            ReflexRoundProgressLabel.Text = $"السؤال {_reflexQuestionIndex + 1} من {_reflexQuestions.Count}";
            ReflexTargetWordLabel.Text = q.TargetWord.ArabicWord;

            // نطق صوتي مساعد فور ظهور السؤال
            _ = TextToSpeech.Default.SpeakAsync(q.TargetWord.EnglishWord);

            ReflexOptionsContainer.Children.Clear();
            foreach (var opt in q.Options)
            {
                ReflexOptionsContainer.Children.Add(CreateReflexOptionButton(opt, q.CorrectOption));
            }

            // إطلاق شريط المؤقت السريع (1.8 ثانية خاطفة لصناعة رد الفعل التلقائي)
            _reflexTimeTicks = ReflexTotalTicks;
            ReflexProgressBar.Progress = 1.0;
            ReflexProgressBar.ProgressColor = Color.FromArgb("#F59E0B");

            _reflexTimer = Dispatcher.CreateTimer();
            _reflexTimer.Interval = TimeSpan.FromMilliseconds(100);
            _reflexTimer.Tick += (s, e) =>
            {
                _reflexTimeTicks--;
                ReflexProgressBar.Progress = (double)_reflexTimeTicks / ReflexTotalTicks;

                if (_reflexTimeTicks <= 6)
                {
                    ReflexProgressBar.ProgressColor = Color.FromArgb("#EF4444"); // وميض أحمر تحذيري
                }

                if (_reflexTimeTicks <= 0)
                {
                    StopAllTimers();
                    HandleReflexTimeout();
                }
            };
            _reflexTimer.Start();
        }

        private Border CreateReflexOptionButton(string optionText, string correctOption)
        {
            var border = new Border
            {
                Stroke = Color.FromArgb("#4338CA"),
                StrokeThickness = 1.5,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
                BackgroundColor = Color.FromArgb("#11182E"),
                Padding = new Thickness(16, 14),
                MinimumHeightRequest = 56,
                HeightRequest = 58,
                HorizontalOptions = LayoutOptions.Fill,
                InputTransparent = false
            };

            var label = new Label
            {
                Text = optionText,
                FontSize = 16.5,
                FontFamily = "CairoBold",
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true
            };

            border.Content = label;

            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    if (_isProcessingAnswer) return;
                    _isProcessingAnswer = true;
                    StopAllTimers();

                    if (optionText == correctOption)
                    {
                        // إجابة صحيحة وخاطفة! 🌟
                        _ = Sounds.PlayAsync(Sounds.Correct());
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                        border.BackgroundColor = Color.FromArgb("#065F46");
                        border.Stroke = Color.FromArgb("#10B981");
                        _reflexScore += 100;
                        ReflexScoreLabel.Text = $"⭐ {_reflexScore}";

                        await Task.Delay(550);
                        _reflexQuestionIndex++;
                        DisplayNextReflexQuestion();
                    }
                    else
                    {
                        // إجابة خاطئة: إعادة السؤال بنهاية الجولة لضمان ترسيخه التام
                        _ = Sounds.PlayAsync(Sounds.Wrong());
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                        border.BackgroundColor = Color.FromArgb("#881337");
                        border.Stroke = Color.FromArgb("#F43F5E");

                        if (_reflexQuestionIndex < _reflexQuestions.Count)
                        {
                            var currentQ = _reflexQuestions[_reflexQuestionIndex];
                            _reflexQuestions.Add(currentQ);
                        }

                        await Task.Delay(750);
                        _reflexQuestionIndex++;
                        DisplayNextReflexQuestion();
                    }
                })
            });

            return border;
        }

        private void HandleReflexTimeout()
        {
            if (_isProcessingAnswer) return;
            _isProcessingAnswer = true;

            _ = Sounds.PlayAsync(Sounds.Wrong());
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

            // إعادة السؤال في نهاية الجولة لضمان عدم تجاوز الكلمة دون سرعة رد فعل خاطفة
            if (_reflexQuestionIndex < _reflexQuestions.Count)
            {
                var q = _reflexQuestions[_reflexQuestionIndex];
                _reflexQuestions.Add(q);
            }

            _reflexQuestionIndex++;
            Task.Delay(600).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(DisplayNextReflexQuestion));
        }

        // ============================================================
        // 🧩 المحطة 3: التركيب الحركي والتهجئة (Kinesthetic Spelling)
        // ============================================================

        private void StartStation3()
        {
            ShowState(JourneyState.Station3_Spelling);
            _spellingWordIndex = 0;
            DisplayCurrentSpellingWord();
        }

        private void DisplayCurrentSpellingWord()
        {
            if (_spellingWordIndex >= CurrentBatch.Count)
            {
                // أتممت المحطة 3 بنجاح! الآن الانطلاق إلى المحطة 4 (كاسحة ألغام الحروف)
                _ = Sounds.PlayAsync(Sounds.Win());
                Task.Delay(800).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(StartStation4));
                return;
            }

            _currentSpellingWord = CurrentBatch[_spellingWordIndex];
            SpellingProgressLabel.Text = $"الكلمة {_spellingWordIndex + 1} من {CurrentBatch.Count}";
            SpellingArabicWordLabel.Text = _currentSpellingWord.ArabicWord;

            // نطق صوتي للكلمة
            _ = TextToSpeech.Default.SpeakAsync(_currentSpellingWord.EnglishWord);

            // إعداد خانات الكلمة (Slots مرتبة من اليسار لليمين LTR)
            _placedLetters.Clear();
            _placedLetterBorders.Clear();
            SpellingSlotsContainer.FlowDirection = FlowDirection.LeftToRight;
            SpellingSlotsContainer.Children.Clear();

            string cleanWord = Regex.Replace(_currentSpellingWord.EnglishWord.ToUpperInvariant(), @"[^A-Z]", "");
            double slotWidth = cleanWord.Length >= 9 ? 30 : (cleanWord.Length >= 7 ? 36 : (cleanWord.Length >= 5 ? 42 : 46));
            double slotHeight = cleanWord.Length >= 9 ? 42 : (cleanWord.Length >= 7 ? 46 : 50);
            double fontSize = cleanWord.Length >= 9 ? 15 : (cleanWord.Length >= 7 ? 17 : 20);
            SpellingSlotsContainer.Spacing = cleanWord.Length >= 8 ? 5 : 8;

            for (int i = 0; i < cleanWord.Length; i++)
            {
                var slotBorder = new Border
                {
                    WidthRequest = slotWidth,
                    HeightRequest = slotHeight,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) },
                    BackgroundColor = Color.FromArgb("#0E1626"),
                    Stroke = Color.FromArgb("#334155"),
                    StrokeThickness = 1.5,
                    Content = new Label
                    {
                        Text = "_",
                        FontSize = fontSize,
                        FontFamily = "CairoBold",
                        TextColor = Color.FromArgb("#64748B"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        InputTransparent = true
                    }
                };

                _placedLetterBorders.Add(slotBorder);
                SpellingSlotsContainer.Children.Add(slotBorder);
            }

            // إعداد بنك الحروف المبعثرة (صفوف منتظمة ومستجيبة للمس 100% في جميع الصفوف)
            SpellingBubblesContainer.Children.Clear();

            var lettersPool = cleanWord.ToList();
            char[] distractorLetters = { 'E', 'A', 'R', 'T', 'O', 'S', 'N' };
            for (int i = 0; i < 2; i++)
            {
                lettersPool.Add(distractorLetters[Random.Shared.Next(distractorLetters.Length)]);
            }
            lettersPool = lettersPool.OrderBy(_ => Guid.NewGuid()).ToList();

            // توزيع الحروف بانتظام على صفوف (حد أقصى 5 حروف بالصف) لضمان الاستجابة التامة للمس
            int total = lettersPool.Count;
            int itemsPerRow = total <= 5 ? total : (total <= 8 ? 4 : 5);
            int rowsCount = (int)Math.Ceiling((double)total / itemsPerRow);

            double bubbleSize = total > 10 ? 44 : (total >= 9 ? 46 : 48);
            double bubbleFontSize = total > 10 ? 17 : 19;

            for (int r = 0; r < rowsCount; r++)
            {
                var rowLayout = new HorizontalStackLayout
                {
                    FlowDirection = FlowDirection.LeftToRight,
                    Spacing = 8,
                    HorizontalOptions = LayoutOptions.Center
                };

                var rowLetters = lettersPool.Skip(r * itemsPerRow).Take(itemsPerRow);
                foreach (var ch in rowLetters)
                {
                    rowLayout.Children.Add(CreateLetterBubble(ch, cleanWord, bubbleSize, bubbleFontSize));
                }

                SpellingBubblesContainer.Children.Add(rowLayout);
            }
        }

        private Border CreateLetterBubble(char letter, string targetCleanWord, double bubbleSize, double fontSize)
        {
            var bubble = new Border
            {
                WidthRequest = bubbleSize,
                HeightRequest = bubbleSize,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(14) },
                BackgroundColor = Color.FromArgb("#1E1B4B"),
                Stroke = Color.FromArgb("#818CF8"),
                StrokeThickness = 1.5,
                Margin = new Thickness(4),
                InputTransparent = false,
                Content = new Label
                {
                    Text = letter.ToString(),
                    FontSize = fontSize,
                    FontFamily = "CairoBold",
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    InputTransparent = true
                }
            };

            bubble.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    int nextIndex = _placedLetters.Count;
                    if (nextIndex >= targetCleanWord.Length) return;

                    char expectedChar = targetCleanWord[nextIndex];

                    if (letter == expectedChar)
                    {
                        // استقرار الحرف الصحيح في خانته!
                        _placedLetters.Add(letter);
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                        var slot = _placedLetterBorders[nextIndex];
                        if (slot.Content is Label slotLabel)
                        {
                            slotLabel.Text = letter.ToString();
                            slotLabel.TextColor = Color.FromArgb("#38BDF8");
                        }
                        slot.BackgroundColor = Color.FromArgb("#0C2A44");
                        slot.Stroke = Color.FromArgb("#38BDF8");

                        bubble.IsEnabled = false;
                        bubble.Opacity = 0.2;

                        if (_placedLetters.Count == targetCleanWord.Length)
                        {
                            // اكتملت الكلمة كاملة بنجاح!
                            _ = Sounds.PlayAsync(Sounds.Correct());
                            if (_currentSpellingWord != null)
                            {
                                _ = TextToSpeech.Default.SpeakAsync(_currentSpellingWord.EnglishWord);
                            }

                            // وميض ذهبي لكامل الكلمة
                            foreach (var s in _placedLetterBorders)
                            {
                                s.BackgroundColor = Color.FromArgb("#065F46");
                                s.Stroke = Color.FromArgb("#10B981");
                            }

                            await Task.Delay(900);
                            _spellingWordIndex++;
                            DisplayCurrentSpellingWord();
                        }
                    }
                    else
                    {
                        // خطأ: اهتزاز الحرف بالأحمر
                        _ = Sounds.PlayAsync(Sounds.Wrong());
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                        bubble.Stroke = Color.FromArgb("#EF4444");
                        await bubble.TranslateTo(-6, 0, 50);
                        await bubble.TranslateTo(6, 0, 50);
                        await bubble.TranslateTo(0, 0, 50);
                        bubble.Stroke = Color.FromArgb("#818CF8");
                    }
                })
            });

            return bubble;
        }

        private async void OnSpellingAudioHintTapped(object sender, EventArgs e)
        {
            if (_currentSpellingWord != null)
            {
                try { await TextToSpeech.Default.SpeakAsync(_currentSpellingWord.EnglishWord); } catch { }
            }
        }

        // ============================================================
        // 💣 المحطة 4: كاسحة ألغام الحروف (Letter Mine Sweeper)
        // ============================================================

        private void StartStation4()
        {
            ShowState(JourneyState.Station4_MineSweeper);
            _mineWordIndex = 0;
            DisplayCurrentMineWord();
        }

        private void DisplayCurrentMineWord()
        {
            if (_mineWordIndex >= CurrentBatch.Count)
            {
                // انتهت الدفعة الحالية من المحطة 4!
                _ = Sounds.PlayAsync(Sounds.Win());

                if (_currentBatchIndex < _batches.Count - 1)
                {
                    // الانتقال للدفعة التالية بدءاً من المحطة 1
                    _ = Toast.Make("أحسنت! أتممت الدفعة الأولى 🌟 استعد للدفعة الثانية 🚀").Show();
                    _currentBatchIndex++;
                    Task.Delay(1000).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(StartStation1));
                }
                else
                {
                    // جميع الدفعات انتهت من المحطات 1-4! الآن الحسم في المحطة 5 الشاملة (الانعكاس المزدوج)!
                    Task.Delay(1000).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(StartStation5));
                }
                return;
            }

            _currentMineWord = CurrentBatch[_mineWordIndex];
            MineProgressLabel.Text = $"الكلمة {_mineWordIndex + 1} من {CurrentBatch.Count}";
            MineArabicWordLabel.Text = _currentMineWord.ArabicWord;
            MineFeedbackLabel.Text = "المس الحرف المشبوه لتفجيره 💥";
            MineFeedbackLabel.TextColor = Color.FromArgb("#94A3B8");

            // نطق صوتي فوري
            _ = TextToSpeech.Default.SpeakAsync(_currentMineWord.EnglishWord);

            // توليد الحروف مع لغمين دخيلين
            string cleanWord = Regex.Replace(_currentMineWord.EnglishWord.ToUpperInvariant(), @"[^A-Z]", "");
            _currentMineTiles.Clear();
            MineTilesContainer.Children.Clear();

            // اختيار حرفين لغم غير موجودين بالكلمة إن أمكن
            char[] distractorPool = { 'X', 'Z', 'Q', 'J', 'K', 'V', 'W', 'Y', 'B', 'P', 'D', 'M' };
            var availablePool = distractorPool.Where(c => !cleanWord.Contains(c)).ToList();
            if (availablePool.Count < 2) availablePool = distractorPool.ToList();

            char mine1 = availablePool[Random.Shared.Next(availablePool.Count)];
            availablePool.Remove(mine1);
            char mine2 = availablePool[Random.Shared.Next(availablePool.Count)];

            // مواقع إدراج اللغمين في الكلمة
            int insertPos1 = Random.Shared.Next(0, Math.Max(1, cleanWord.Length / 2));
            int insertPos2 = Random.Shared.Next(Math.Max(insertPos1 + 1, cleanWord.Length / 2), cleanWord.Length + 1);

            var rawLetterItems = new List<(char Letter, bool IsMine)>();
            for (int i = 0; i <= cleanWord.Length; i++)
            {
                if (i == insertPos1) rawLetterItems.Add((mine1, true));
                if (i == insertPos2) rawLetterItems.Add((mine2, true));
                if (i < cleanWord.Length) rawLetterItems.Add((cleanWord[i], false));
            }

            _remainingMinesCount = 2;
            MineRemainingLabel.Text = $"💣 {_remainingMinesCount}";

            // بناء البلاطات
            int totalTiles = rawLetterItems.Count;
            double tileSize = totalTiles > 10 ? 42 : (totalTiles >= 8 ? 45 : 48);
            double fontSize = totalTiles > 10 ? 17 : 19;

            // إذا كان العدد كبيراً نقسمه على صفين
            int itemsPerRow = totalTiles <= 7 ? totalTiles : (int)Math.Ceiling((double)totalTiles / 2.0);
            int rowsCount = (int)Math.Ceiling((double)totalTiles / (double)itemsPerRow);

            for (int r = 0; r < rowsCount; r++)
            {
                var rowLayout = new HorizontalStackLayout
                {
                    FlowDirection = FlowDirection.LeftToRight,
                    Spacing = 7,
                    HorizontalOptions = LayoutOptions.Center
                };

                var rowItems = rawLetterItems.Skip(r * itemsPerRow).Take(itemsPerRow);
                foreach (var item in rowItems)
                {
                    var tileInfo = CreateMineTile(item.Letter, item.IsMine, tileSize, fontSize);
                    _currentMineTiles.Add(tileInfo);
                    rowLayout.Children.Add(tileInfo.TileBorder);
                }

                MineTilesContainer.Children.Add(rowLayout);
            }
        }

        private MineTileInfo CreateMineTile(char letter, bool isMine, double size, double fontSize)
        {
            var label = new Label
            {
                Text = letter.ToString(),
                FontSize = fontSize,
                FontFamily = "CairoBold",
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true
            };

            var border = new Border
            {
                WidthRequest = size,
                HeightRequest = size,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(14) },
                BackgroundColor = Color.FromArgb("#1E1B4B"),
                Stroke = Color.FromArgb("#818CF8"),
                StrokeThickness = 1.5,
                Margin = new Thickness(2),
                Content = label
            };

            var tileInfo = new MineTileInfo
            {
                Letter = letter,
                IsMine = isMine,
                TileBorder = border,
                TileLabel = label
            };

            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    if (tileInfo.IsMine)
                    {
                        // تفجير اللغم الدخيل بنجاح! 💥
                        _ = Sounds.PlayAsync(Sounds.Correct());
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                        tileInfo.TileLabel.Text = "💥";
                        tileInfo.TileBorder.BackgroundColor = Color.FromArgb("#DC2626");
                        tileInfo.TileBorder.Stroke = Color.FromArgb("#EF4444");

                        await tileInfo.TileBorder.ScaleTo(1.25, 120);
                        await tileInfo.TileBorder.FadeTo(0, 160);
                        tileInfo.TileBorder.IsVisible = false;

                        _remainingMinesCount--;
                        MineRemainingLabel.Text = $"💣 {_remainingMinesCount}";

                        if (_remainingMinesCount <= 0)
                        {
                            // تم تطهير الكلمة بالكامل! ✨
                            MineFeedbackLabel.Text = "تم تطهير الكلمة بنجاح! ✨";
                            MineFeedbackLabel.TextColor = Color.FromArgb("#10B981");

                            foreach (var t in _currentMineTiles.Where(t => !t.IsMine))
                            {
                                t.TileBorder.BackgroundColor = Color.FromArgb("#065F46");
                                t.TileBorder.Stroke = Color.FromArgb("#10B981");
                            }

                            if (_currentMineWord != null)
                            {
                                _ = TextToSpeech.Default.SpeakAsync(_currentMineWord.EnglishWord);
                            }

                            await Task.Delay(900);
                            _mineWordIndex++;
                            DisplayCurrentMineWord();
                        }
                    }
                    else
                    {
                        // حرف أصلي تم لمسه بالخطأ
                        _ = Sounds.PlayAsync(Sounds.Wrong());
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                        tileInfo.TileBorder.Stroke = Color.FromArgb("#EF4444");
                        await tileInfo.TileBorder.TranslateTo(-6, 0, 50);
                        await tileInfo.TileBorder.TranslateTo(6, 0, 50);
                        await tileInfo.TileBorder.TranslateTo(0, 0, 50);
                        tileInfo.TileBorder.Stroke = Color.FromArgb("#818CF8");

                        MineFeedbackLabel.Text = "انتبه! هذا حرف أصلي في الكلمة وليس لغماً ⚠️";
                        MineFeedbackLabel.TextColor = Color.FromArgb("#EF4444");
                    }
                })
            });

            return tileInfo;
        }

        private async void OnMineAudioHintTapped(object sender, EventArgs e)
        {
            if (_currentMineWord != null)
            {
                try { await TextToSpeech.Default.SpeakAsync(_currentMineWord.EnglishWord); } catch { }
            }
        }

        // ============================================================
        // 🪞 المحطة 5: بوابة الإتقان والانعكاس المزدوج (Dual Mirror Gate)
        // ============================================================

        private void StartStation5()
        {
            ShowState(JourneyState.Station5_DualMirror);
            _isInRepairMode = false;
            _repairWordsQueue.Clear();

            GatekeeperTitleLabel.Text = "المحطة 5: بوابة الإتقان (الانعكاس المزدوج) 🪞";
            GatekeeperSubLabel.Text = "اختبار حاسم ثنائي الاتجاه للتأكد من رسوخ جميع الكلمات في ذاكرتك 100%.";

            // توليد أسئلة الانعكاس المزدوج: كل كلمة تُختبر بالاتجاهين (إنجليزي ➔ عربي) و (عربي ➔ إنجليزي)
            _gateQuestions = new();
            foreach (var w in _selectedWords)
            {
                // الاتجاه الأول: إنجليزي ➔ عربي
                _gateQuestions.Add(new GateQuestion
                {
                    TargetWord = w,
                    PromptType = "EngToAr",
                    QuestionText = $"ما المعنى العربي لكلمة ({w.EnglishWord})؟",
                    CorrectOption = w.ArabicWord,
                    Options = GenerateGateArabicOptions(w)
                });

                // الاتجاه الثاني المعكوس: عربي ➔ إنجليزي
                _gateQuestions.Add(new GateQuestion
                {
                    TargetWord = w,
                    PromptType = "ArToEng",
                    QuestionText = $"ما الكلمة الإنجليزية لـ ({w.ArabicWord})؟",
                    CorrectOption = w.EnglishWord,
                    Options = GenerateGateEnglishOptions(w)
                });
            }

            _gateQuestions = _gateQuestions.OrderBy(_ => Guid.NewGuid()).ToList();
            _gateQuestionIndex = 0;

            DisplayNextGateQuestion();
        }

        private List<string> GenerateGateArabicOptions(WordModel target)
        {
            var distractors = _allSessionWords
                .Where(w => w.ArabicWord != target.ArabicWord)
                .OrderBy(_ => Guid.NewGuid())
                .Take(3)
                .Select(w => w.ArabicWord)
                .ToList();

            var opts = new List<string> { target.ArabicWord };
            opts.AddRange(distractors);
            return opts.OrderBy(_ => Guid.NewGuid()).ToList();
        }

        private List<string> GenerateGateEnglishOptions(WordModel target)
        {
            var distractors = _allSessionWords
                .Where(w => w.EnglishWord != target.EnglishWord)
                .OrderBy(_ => Guid.NewGuid())
                .Take(3)
                .Select(w => w.EnglishWord)
                .ToList();

            var opts = new List<string> { target.EnglishWord };
            opts.AddRange(distractors);
            return opts.OrderBy(_ => Guid.NewGuid()).ToList();
        }

        private void DisplayNextGateQuestion()
        {
            _isProcessingAnswer = false;

            if (_gateQuestionIndex >= _gateQuestions.Count)
            {
                // فحص حلقة الإنقاذ للتأكد من الإتقان التام 100%
                if (_repairWordsQueue.Count > 0)
                {
                    LaunchSmartRepairLoop();
                }
                else
                {
                    // إتقان 100% في بوابة الإتقان! الآن الانطلاق إلى المحطة 6: خزنة التسميع الغيبي الحر! 🔐
                    _ = Sounds.PlayAsync(Sounds.Win());
                    Task.Delay(800).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(StartStation6));
                }
                return;
            }

            var q = _gateQuestions[_gateQuestionIndex];
            GateProgressLabel.Text = _isInRepairMode
                ? $"تثبيت نهائي: {_gateQuestionIndex + 1} من {_gateQuestions.Count}"
                : $"السؤال {_gateQuestionIndex + 1} من {_gateQuestions.Count}";

            bool isArToEng = (q.PromptType == "ArToEng");

            GateQuestionPromptLabel.Text = isArToEng ? "ما هي الكلمة الإنجليزية المقابلة لـ:" : "ما هو المعنى العربي الصحيح لـ:";
            GateQuestionWordLabel.Text = isArToEng ? q.TargetWord.ArabicWord : q.TargetWord.EnglishWord;
            GateQuestionWordLabel.TextColor = isArToEng ? Color.FromArgb("#FBBF24") : Color.FromArgb("#38BDF8");
            GateAudioButton.IsVisible = !isArToEng;

            GateOptionsContainer.Children.Clear();
            foreach (var opt in q.Options)
            {
                GateOptionsContainer.Children.Add(CreateGateOptionButton(opt, q.CorrectOption, q.TargetWord, isArToEng));
            }
        }

        private Border CreateGateOptionButton(string optionText, string correctOption, WordModel word, bool isOptionEnglish)
        {
            var border = new Border
            {
                Stroke = Color.FromArgb("#334155"),
                StrokeThickness = 1.3,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
                BackgroundColor = Color.FromArgb("#0E1626"),
                Padding = new Thickness(16, 13),
                MinimumHeightRequest = 56,
                HeightRequest = 58,
                HorizontalOptions = LayoutOptions.Fill,
                InputTransparent = false
            };

            var label = new Label
            {
                Text = optionText,
                FontSize = 16,
                FontFamily = "CairoBold",
                TextColor = isOptionEnglish ? Color.FromArgb("#38BDF8") : Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true
            };

            border.Content = label;

            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    if (_isProcessingAnswer) return;
                    _isProcessingAnswer = true;

                    if (isOptionEnglish)
                    {
                        _ = TextToSpeech.Default.SpeakAsync(optionText);
                    }

                    if (optionText == correctOption)
                    {
                        _ = Sounds.PlayAsync(Sounds.Correct());
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                        border.BackgroundColor = Color.FromArgb("#065F46");
                        border.Stroke = Color.FromArgb("#10B981");

                        await Task.Delay(500);
                        _gateQuestionIndex++;
                        DisplayNextGateQuestion();
                    }
                    else
                    {
                        // خطأ: إضافة الكلمة لحلقة التثبيت الذكية
                        _ = Sounds.PlayAsync(Sounds.Wrong());
                        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                        border.BackgroundColor = Color.FromArgb("#881337");
                        border.Stroke = Color.FromArgb("#F43F5E");

                        if (!_repairWordsQueue.Any(rw => rw.EnglishWord == word.EnglishWord))
                        {
                            _repairWordsQueue.Add(word);
                        }

                        await Task.Delay(750);
                        _gateQuestionIndex++;
                        DisplayNextGateQuestion();
                    }
                })
            });

            return border;
        }

        private async void OnGateAudioPromptTapped(object sender, EventArgs e)
        {
            if (_gateQuestionIndex < _gateQuestions.Count)
            {
                try { await TextToSpeech.Default.SpeakAsync(_gateQuestions[_gateQuestionIndex].TargetWord.EnglishWord); } catch { }
            }
        }

        // --- حلقة الإنقاذ الذكية للكلمات المتعثرة ---
        private void LaunchSmartRepairLoop()
        {
            _isInRepairMode = true;
            GatekeeperTitleLabel.Text = "حلقة التثبيت والانعكاس 🛠️";
            GatekeeperSubLabel.Text = $"تبقى {_repairWordsQueue.Count} كلمات تحتاج تثبيتاً حاسماً، لنختبرها بالاتجاهين!";

            _gateQuestions = new();
            foreach (var rw in _repairWordsQueue)
            {
                _gateQuestions.Add(new GateQuestion
                {
                    TargetWord = rw,
                    PromptType = "EngToAr",
                    QuestionText = $"ما المعنى العربي لكلمة ({rw.EnglishWord})؟",
                    CorrectOption = rw.ArabicWord,
                    Options = GenerateGateArabicOptions(rw)
                });

                _gateQuestions.Add(new GateQuestion
                {
                    TargetWord = rw,
                    PromptType = "ArToEng",
                    QuestionText = $"ما الكلمة الإنجليزية لـ ({rw.ArabicWord})؟",
                    CorrectOption = rw.EnglishWord,
                    Options = GenerateGateEnglishOptions(rw)
                });
            }

            _gateQuestions = _gateQuestions.OrderBy(_ => Guid.NewGuid()).ToList();
            _repairWordsQueue.Clear();
            _gateQuestionIndex = 0;
            DisplayNextGateQuestion();
        }

        // ============================================================
        // 🔐 المحطة 6: خزنة التسميع الغيبي الحر (The Golden Vault)
        // ============================================================

        private void StartStation6()
        {
            ShowState(JourneyState.Station6_GoldenVault);
            _vaultSlots.Clear();
            VaultSlotsContainer.Children.Clear();
            VaultWordEntry.Text = string.Empty;
            ResetVaultMicUI();

            VaultStatusLabel.Text = "تذكر إحدى الكلمات وانطقها بالميكروفون أو اكتبها أدناه 🚀";
            VaultStatusLabel.TextColor = Color.FromArgb("#94A3B8");

            // بناء بطاقات الخزائن المقفلة بترتيب الكلمات
            for (int i = 0; i < _selectedWords.Count; i++)
            {
                var w = _selectedWords[i];
                var slotInfo = CreateVaultSlotView(i, w);
                _vaultSlots.Add(slotInfo);
                VaultSlotsContainer.Children.Add(slotInfo.SlotBorder);
            }

            UpdateVaultProgress();
        }

        private VaultSlotInfo CreateVaultSlotView(int index, WordModel word)
        {
            var statusIconLabel = new Label
            {
                Text = "🔒",
                FontSize = 20,
                VerticalOptions = LayoutOptions.Center
            };

            var titleLabel = new Label
            {
                Text = $"الكلمة السرية #{index + 1}",
                FontSize = 14,
                FontFamily = "CairoBold",
                TextColor = Color.FromArgb("#94A3B8"),
                VerticalOptions = LayoutOptions.Center
            };

            var subLabel = new Label
            {
                Text = "في انتظار استرجاعها 💭",
                FontSize = 11.5,
                FontFamily = "CairoRegular",
                TextColor = Color.FromArgb("#64748B"),
                VerticalOptions = LayoutOptions.Center
            };

            var wordsStack = new VerticalStackLayout
            {
                Spacing = 1,
                VerticalOptions = LayoutOptions.Center,
                Children = { titleLabel, subLabel }
            };

            var speakerBorder = new Border
            {
                WidthRequest = 32,
                HeightRequest = 32,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10) },
                BackgroundColor = Color.FromArgb("#11223A"),
                Stroke = Color.FromArgb("#38BDF8"),
                StrokeThickness = 1,
                IsVisible = false,
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = "🔊",
                    FontSize = 14,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            speakerBorder.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    try { await TextToSpeech.Default.SpeakAsync(word.EnglishWord); } catch { }
                })
            });

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 12,
                VerticalOptions = LayoutOptions.Center
            };

            Grid.SetColumn(statusIconLabel, 0);
            Grid.SetColumn(wordsStack, 1);
            Grid.SetColumn(speakerBorder, 2);

            grid.Children.Add(statusIconLabel);
            grid.Children.Add(wordsStack);
            grid.Children.Add(speakerBorder);

            var border = new Border
            {
                Stroke = Color.FromArgb("#334155"),
                StrokeThickness = 1.3,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
                BackgroundColor = Color.FromArgb("#0F172A"),
                Padding = new Thickness(14, 10),
                HorizontalOptions = LayoutOptions.Fill,
                Content = grid
            };

            return new VaultSlotInfo
            {
                SlotIndex = index,
                Word = word,
                IsUnlocked = false,
                SlotBorder = border,
                StatusIconLabel = statusIconLabel,
                TitleLabel = titleLabel,
                SubLabel = subLabel,
                SpeakerBorder = speakerBorder
            };
        }

        private void UpdateVaultProgress()
        {
            int unlocked = _vaultSlots.Count(s => s.IsUnlocked);
            VaultProgressLabel.Text = $"🎯 تم استرجاع {unlocked} من {_vaultSlots.Count} كلمات";
        }

        private async void OnVaultMicTapped(object sender, EventArgs e)
        {
            if (_isListeningVault)
            {
                SpeechRecognitionService.CancelListening();
                _isListeningVault = false;
                ResetVaultMicUI();
                return;
            }

            _isListeningVault = true;
            VaultMicBorder.BackgroundColor = Color.FromArgb("#064E3B");
            VaultMicBorder.Stroke = Color.FromArgb("#10B981");
            VaultMicText.Text = "جاري الاستماع... تحدث الآن بالإنجليزية 🎙️";
            VaultMicPulse.BackgroundColor = Color.FromArgb("#EF4444");

            try
            {
                var (success, spoken, err) = await SpeechRecognitionService.ListenRawAsync();
                ResetVaultMicUI();
                _isListeningVault = false;

                if (success && !string.IsNullOrWhiteSpace(spoken))
                {
                    VerifyVaultWord(spoken);
                }
                else
                {
                    VaultStatusLabel.Text = string.IsNullOrWhiteSpace(err)
                        ? "لم يتم سماع صوت بوضوح، انطق الكلمة ثانية أو اكتبها."
                        : err;
                    VaultStatusLabel.TextColor = Color.FromArgb("#EF4444");
                }
            }
            catch (Exception ex)
            {
                ResetVaultMicUI();
                _isListeningVault = false;
                VaultStatusLabel.Text = $"تعذر الاستماع: {ex.Message}";
                VaultStatusLabel.TextColor = Color.FromArgb("#EF4444");
            }
        }

        private void ResetVaultMicUI()
        {
            VaultMicBorder.BackgroundColor = Color.FromArgb("#0C2036");
            VaultMicBorder.Stroke = Color.FromArgb("#38BDF8");
            VaultMicText.Text = "اضغط للتحدث بالإنجليزية 🎙️";
            VaultMicPulse.BackgroundColor = Color.FromArgb("#10B981");
        }

        private void OnVaultSubmitClicked(object? sender, EventArgs e)
        {
            string text = VaultWordEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return;
            VerifyVaultWord(text);
        }

        private void OnVaultEntryCompleted(object sender, EventArgs e)
        {
            OnVaultSubmitClicked(sender, e);
        }

        private async void VerifyVaultWord(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            string cleanedInput = SpeechRecognitionService.CleanWord(input);

            // 1. فحص هل الكلمة فُك قفلها بالفعل
            var alreadyUnlocked = _vaultSlots.FirstOrDefault(s => s.IsUnlocked &&
                (string.Equals(SpeechRecognitionService.CleanWord(s.Word.EnglishWord), cleanedInput, StringComparison.OrdinalIgnoreCase) ||
                 SpeechRecognitionService.IsPronunciationMatch(cleanedInput, s.Word.EnglishWord)));

            if (alreadyUnlocked != null)
            {
                VaultStatusLabel.Text = $"الكلمة '{alreadyUnlocked.Word.EnglishWord}' استرجعتها مسبقاً! ابحث عن كلمة أخرى 🎯";
                VaultStatusLabel.TextColor = Color.FromArgb("#FBBF24");
                VaultWordEntry.Text = string.Empty;
                try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
                return;
            }

            // 2. البحث في الخزائن المقفلة
            var matchedSlot = _vaultSlots.FirstOrDefault(s => !s.IsUnlocked &&
                (string.Equals(SpeechRecognitionService.CleanWord(s.Word.EnglishWord), cleanedInput, StringComparison.OrdinalIgnoreCase) ||
                 SpeechRecognitionService.IsPronunciationMatch(cleanedInput, s.Word.EnglishWord)));

            if (matchedSlot != null)
            {
                // فتح الخزنة بنجاح! 🌟
                _ = Sounds.PlayAsync(Sounds.Correct());
                try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                matchedSlot.IsUnlocked = true;
                matchedSlot.SlotBorder.BackgroundColor = Color.FromArgb("#065F46");
                matchedSlot.SlotBorder.Stroke = Color.FromArgb("#10B981");
                matchedSlot.StatusIconLabel.Text = "✅";
                matchedSlot.TitleLabel.Text = $"{matchedSlot.SlotIndex + 1}. {matchedSlot.Word.EnglishWord}";
                matchedSlot.TitleLabel.TextColor = Color.FromArgb("#38BDF8");
                matchedSlot.TitleLabel.FontSize = 16;
                matchedSlot.SubLabel.Text = matchedSlot.Word.ArabicWord;
                matchedSlot.SubLabel.TextColor = Color.FromArgb("#FBBF24");
                matchedSlot.SubLabel.FontSize = 13;
                matchedSlot.SpeakerBorder.IsVisible = true;

                await matchedSlot.SlotBorder.ScaleTo(1.04, 120);
                await matchedSlot.SlotBorder.ScaleTo(1.0, 120);

                _ = TextToSpeech.Default.SpeakAsync(matchedSlot.Word.EnglishWord);

                VaultWordEntry.Text = string.Empty;
                VaultStatusLabel.Text = $"أحسنت! استرجعت الكلمة #{matchedSlot.SlotIndex + 1}: {matchedSlot.Word.EnglishWord} 🎉";
                VaultStatusLabel.TextColor = Color.FromArgb("#10B981");

                UpdateVaultProgress();

                if (_vaultSlots.All(s => s.IsUnlocked))
                {
                    VaultStatusLabel.Text = "مذهل! استرجعت جميع الكلمات بنجاح تام! 👑";
                    // لا نقوم بتشغيل Sounds.Win() هنا منعاً لتكرار الصوت مع شاشة التتويج
                    await Task.Delay(850);
                    ShowVictoryScreen();
                }
            }
            else
            {
                // الكلمة غير متطابقة
                _ = Sounds.PlayAsync(Sounds.Wrong());
                try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                VaultStatusLabel.Text = $"'{input}' ليست ضمن الكلمات المتبقية، حاول تذكر كلمة أخرى!";
                VaultStatusLabel.TextColor = Color.FromArgb("#EF4444");

                await VaultWordEntry.TranslateTo(-6, 0, 40);
                await VaultWordEntry.TranslateTo(6, 0, 40);
                await VaultWordEntry.TranslateTo(0, 0, 40);
            }
        }

        private async void OnVaultHintTapped(object? sender, EventArgs e)
        {
            var nextLocked = _vaultSlots.FirstOrDefault(s => !s.IsUnlocked);
            if (nextLocked == null)
            {
                await Toast.Make("أتممت استرجاع جميع الكلمات بالفعل! 🏆").Show();
                return;
            }

            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

            HintPopupTitleLabel.Text = $"تلميح للكلمة #{nextLocked.SlotIndex + 1}";
            HintPopupArabicLabel.Text = nextLocked.Word.ArabicWord;
            HintPopupFirstLetterLabel.Text = char.ToUpperInvariant(nextLocked.Word.EnglishWord[0]).ToString();

            VaultHintOverlay.Opacity = 0;
            VaultHintOverlay.IsVisible = true;
            await VaultHintOverlay.FadeTo(1, 160);
        }

        private async void OnCloseHintPopupTapped(object? sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            await VaultHintOverlay.FadeTo(0, 140);
            VaultHintOverlay.IsVisible = false;
        }

        // ============================================================
        // 👑 الحالة 5: شاشة التتويج والاحتفال بالإتقان الكامل
        // ============================================================

        private void ShowVictoryScreen()
        {
            ShowState(JourneyState.Victory);
            _ = Sounds.PlayAsync(Sounds.Win());
            try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }

            VictoryWordsListLabel.Text = string.Join(" • ", _selectedWords.Select(w => $"{w.EnglishWord} ({w.ArabicWord})"));
        }

        private async void OnFinishJourneyClicked(object sender, EventArgs e)
        {
            SpeechRecognitionService.CancelListening();
            await Navigation.PopModalAsync();
        }

        private async void OnExitTapped(object sender, EventArgs e)
        {
            SpeechRecognitionService.CancelListening();
            if (VictoryView.IsVisible)
            {
                await Navigation.PopModalAsync();
                return;
            }

            bool confirm = await DisplayAlert("مغادرة الرحلة", "هل تريد التوقف الآن؟ لم تكتمل كل المحطات بعد.", "نعم، خروج", "متابعة");
            if (confirm)
            {
                await Navigation.PopModalAsync();
            }
        }
    }
}
