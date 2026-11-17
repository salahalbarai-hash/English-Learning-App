using English.Popups;
using English.ViewModels;

namespace English.Pages;

public partial class TenWordsPage : ContentPage
{
    private bool _isLoading = false;
    private bool _isNavigating = false;

    private TenWordsVM vm;

    private CancellationTokenSource? _autoReciteCts;
    private bool _isRecitingActive;

    // =========================================================
    // إعدادات المسافة السفلية وإظهار الأزرار
    // =========================================================

    private bool _footerVisible = false;
    private int _footerAnimationVersion = 0;

    private const double CardItemHeight = 90.0;
    private const double CardSpacing = 20.0;

    private const double EstimatedFooterHeight = 180.0;

    // أقل مساحة تمرير مطلوبة بعد الأزرار
    private const double MinimumScrollRange = 72.0;

    // أقل ارتفاع للفاصل الموجود أسفل الأزرار
    private const double MinimumBottomSpacer = 80.0;

    public TenWordsPage()
    {
        InitializeComponent();
        BindingContext = vm = new TenWordsVM();

        // إخفاء الأزرار عند إنشاء الصفحة
        FooterContainer.Opacity = 0;
        FooterContainer.InputTransparent = true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _isNavigating = false;

        vm.ProgressStatus = Preferences.Get("Coins", 0L).ToString();

        if (ProgressBarContainer.Width > 0)
        {
            vm.UpdateContainerWidth(ProgressBarContainer.Width);
        }

        long savedWords = 0;

        try
        {
            savedWords = Preferences.Get("MemorizedWords", 0L);
        }
        catch
        {
            try
            {
                savedWords = Preferences.Get("MemorizedWords", 0);
            }
            catch { }
        }

        vm.MemorizedWordsCount = savedWords;

        if (vm.Titles.Count == 0)
        {
            vm.LoadDailyWords();
        }

        // تحديث مساحة التمرير بعد تحميل الكلمات
        UpdateFooterBottomSpacer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        StopAutoRecite();

        SpeechRecognitionService.CancelListening();

        if (_currentListeningWord != null)
        {
            _currentListeningWord.IsListening = false;
            _currentListeningWord = null;
        }
    }

    // =========================================================
    // حساب المساحة السفلية
    // =========================================================

    private void OnCVSizeChanged(object sender, EventArgs e)
    {
        UpdateFooterBottomSpacer();
    }

    private void OnFooterContainerSizeChanged(object sender, EventArgs e)
    {
        UpdateFooterBottomSpacer();
    }

    private void UpdateFooterBottomSpacer()
    {
        if (CV == null ||
            FooterContainer == null ||
            FooterBottomSpacer == null ||
            vm == null)
        {
            return;
        }

        double viewportHeight = CV.Height;

        if (viewportHeight <= 0)
            return;

        int count = vm.Titles?.Count > 0
            ? vm.Titles.Count
            : 5;

        // ارتفاع البطاقات شامل الحاشية العلوية والسفلية
        double cardsTotalHeight =
            (count * CardItemHeight) +
            (Math.Max(0, count - 1) * CardSpacing);

        // استخدام الارتفاع الفعلي للـ Footer إن كان متاحًا
        double footerHeight = FooterContainer.Height > 0
            ? FooterContainer.Height
            : EstimatedFooterHeight;

        // حساب المسافة المطلوبة أسفل الأزرار
        // لضمان إمكانية التمرير حتى لو كانت البطاقات قليلة
        double requiredBottomSpacer = Math.Max(
            MinimumBottomSpacer,
            viewportHeight +
            MinimumScrollRange -
            cardsTotalHeight -
            footerHeight
        );

        // تجنب تحديث القيمة دون حاجة
        if (Math.Abs(FooterBottomSpacer.HeightRequest -
                     requiredBottomSpacer) > 1)
        {
            FooterBottomSpacer.HeightRequest = requiredBottomSpacer;
        }
    }

    // =========================================================
    // إظهار وإخفاء الأزرار عند التمرير
    // =========================================================

    private void OnCVScrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        // لا تظهر الأزرار إلا بعد بدء التمرير
        bool shouldShowFooter = e.VerticalOffset > 20;

        if (_footerVisible == shouldShowFooter)
            return;

        _footerVisible = shouldShowFooter;

        // منع الضغط على الأزرار وهي مخفية
        FooterContainer.InputTransparent = !shouldShowFooter;

        int animationVersion = ++_footerAnimationVersion;

        _ = AnimateFooterVisibilityAsync(
            shouldShowFooter,
            animationVersion
        );
    }

    private async Task AnimateFooterVisibilityAsync(
        bool shouldShow,
        int animationVersion)
    {
        try
        {
            await FooterContainer.FadeTo(
                shouldShow ? 1 : 0,
                180,
                Easing.CubicOut
            );

            // تجاهل أي حركة قديمة إذا بدأ تحريك أحدث
            if (animationVersion != _footerAnimationVersion)
                return;
        }
        catch
        {
            if (animationVersion == _footerAnimationVersion)
            {
                FooterContainer.Opacity = shouldShow ? 1 : 0;
            }
        }
    }

    // =========================================================
    // تحديث شريط التقدم
    // =========================================================

    private void OnProgressBarContainerSizeChanged(
        object sender,
        EventArgs e)
    {
        if (sender is Grid grid &&
            grid.Width > 0 &&
            vm != null)
        {
            vm.UpdateContainerWidth(grid.Width);
        }
    }

    // =========================================================
    // البحث عن نص الكلمة داخل البطاقة
    // =========================================================

    private static Label? FindWordLabel(Border border)
    {
        if (border.Content is Label label)
            return label;

        if (border.Content is Grid grid)
        {
            return grid.Children
                       .OfType<Label>()
                       .FirstOrDefault(l => Grid.GetColumn(l) == 1)
                   ?? grid.Children.OfType<Label>().FirstOrDefault();
        }

        return null;
    }

    // =========================================================
    // الضغط على الكلمة
    // =========================================================

    private async void OnWordTapped(object sender, TappedEventArgs e)
    {
        if (_isRecitingActive)
            StopAutoRecite();

        if (sender is Border border &&
            border.BindingContext is WordModel word)
        {
            if (word.Locked)
                return;

            _ = TextToSpeech.Default.SpeakAsync(word.EnglishWord);

            var label = FindWordLabel(border);

            if (label != null)
            {
                await label.ScaleTo(1.1, 120, Easing.CubicOut);
                await label.ScaleTo(1.0, 120, Easing.CubicIn);
            }
        }
    }

    // =========================================================
    // تغيير لغة الكلمة
    // =========================================================

    private async void OnWordLanguageTapped(
        object sender,
        TappedEventArgs e)
    {
        if (_isRecitingActive)
            StopAutoRecite();

        if (sender is Border border &&
            border.BindingContext is WordModel word)
        {
            if (word.Locked)
                return;

            var label = FindWordLabel(border);

            if (label != null)
            {
                await label.RotateXTo(90, 120, Easing.Linear);

                word.CurrentLanguage =
                    (word.CurrentLanguage == "EN") ? "AR" : "EN";

                await label.RotateXTo(0, 120, Easing.Linear);
            }
            else
            {
                word.CurrentLanguage =
                    (word.CurrentLanguage == "EN") ? "AR" : "EN";
            }
        }
    }

    // =========================================================
    // المايك وفحص النطق
    // =========================================================

    private WordModel? _currentListeningWord;
    private bool _isProcessingMic;

    private async void OnMicTapped(
        object sender,
        TappedEventArgs e)
    {
        if (_isRecitingActive)
            StopAutoRecite();

        if (_isProcessingMic)
            return;

        _isProcessingMic = true;

        try
        {
            if (sender is not Border micBorder ||
                micBorder.BindingContext is not WordModel word)
            {
                return;
            }

            if (word.Locked)
                return;

            // إلغاء أي استماع لكلمة أخرى
            if (_currentListeningWord != null &&
                _currentListeningWord != word)
            {
                _currentListeningWord.IsListening = false;
                SpeechRecognitionService.CancelListening();
            }

            // إلغاء الاستماع عند الضغط مرة أخرى
            if (word.IsListening)
            {
                word.IsListening = false;
                SpeechRecognitionService.CancelListening();

                _currentListeningWord = null;
                return;
            }

            _currentListeningWord = word;
            word.IsListening = true;

            // أنيميشن زر المايك
            try
            {
                await micBorder.ScaleTo(1.2, 120, Easing.CubicOut);
                await micBorder.ScaleTo(1.0, 120, Easing.CubicIn);
            }
            catch { }

            try
            {
                await Toast.Make(
                    $"🎙️ استمع إليك الآن... انطق: {word.EnglishWord}"
                ).Show();
            }
            catch { }

            var (success, spokenText, error) =
                await SpeechRecognitionService.ListenAndEvaluateAsync(
                    word.EnglishWord,
                    isListening =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            word.IsListening = isListening;
                        });
                    });

            _currentListeningWord = null;
            word.IsListening = false;

            if (success)
            {
                word.IsPronouncedCorrectly = true;

                try
                {
                    HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                }
                catch { }

                try
                {
                    await micBorder.ScaleTo(1.25, 140, Easing.CubicOut);
                    await micBorder.ScaleTo(1.0, 140, Easing.CubicIn);
                }
                catch { }

                try
                {
                    await Toast.Make(
                        $"🌟 أحسنت! نطقك سليم 100% ({word.EnglishWord})"
                    ).Show();
                }
                catch { }

                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        word.IsPronouncedCorrectly = false;
                    });
                });
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    try
                    {
                        await Toast.Make(error).Show();
                    }
                    catch { }
                }
                else if (!string.IsNullOrWhiteSpace(spokenText))
                {
                    try
                    {
                        await Toast.Make(
                            $"⚠️ سمعت: \"{spokenText}\" | الصحيح: \"{word.EnglishWord}\""
                        ).Show();
                    }
                    catch { }

                    await Task.Delay(350);

                    _ = TextToSpeech.Default.SpeakAsync(word.EnglishWord);
                }
                else
                {
                    try
                    {
                        await Toast.Make(
                            "لم يتم التقاط صوت واضح، حاول مرة أخرى 🔁"
                        ).Show();
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnMicTapped] Exception handled: {ex.Message}");
        }
        finally
        {
            if (_currentListeningWord != null)
            {
                _currentListeningWord.IsListening = false;
                _currentListeningWord = null;
            }

            _isProcessingMic = false;
        }
    }

    // =========================================================
    // رحلة الحفظ السريع
    // =========================================================

    private async void OnNeuralJourneyTapped(
        object sender,
        EventArgs e)
    {
        if (sender is VisualElement btn)
        {
            try
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }
            catch { }

            await btn.ScaleTo(0.86, 75, Easing.CubicOut);
            _ = btn.ScaleTo(1.0, 90, Easing.CubicIn);
        }

        StopAutoRecite();

        if (_isNavigating)
            return;

        var words = vm.Titles
            .Where(w =>
                !string.IsNullOrWhiteSpace(w.EnglishWord) &&
                !string.IsNullOrWhiteSpace(w.ArabicWord))
            .ToList();

        if (words.Count == 0)
        {
            await Toast.Make(
                "لا توجد كلمات متاحة لبدء رحلة الحفظ حالياً."
            ).Show();

            return;
        }

        _isNavigating = true;

        try
        {
            await Navigation.PushModalAsync(
                new NeuralMemoryJourneyPage(words)
            );
        }
        finally
        {
            _isNavigating = false;
        }
    }

    // =========================================================
    // ألعاب الكلمات
    // =========================================================

    private async void OnPracticeGamesClicked(
        object sender,
        EventArgs e)
    {
        StopAutoRecite();

        if (_isNavigating)
            return;

        var wordsForGames = vm.Titles.ToList();

        if (!wordsForGames.Any())
        {
            await Toast.Make(
                "لا توجد كلمات متاحة للتدريب حالياً."
            ).Show();

            return;
        }

        var popup = new TenWordsGamesPopup();

        var result = await this.ShowPopupAsync(popup);

        if (result is string gameChoice &&
            !string.IsNullOrEmpty(gameChoice))
        {
            _isNavigating = true;

            try
            {
                var config = GameSessionConfig.FromTenWords(wordsForGames);

                Page? targetPage = gameChoice switch
                {
                    "WordMatrix" =>
                        new WordMatrixPage(config),

                    "SkywardBird" =>
                        new SkywardBirdPage(config),

                    "SpellReactor" =>
                        new SpellReactorPage(config),

                    _ => null
                };

                if (targetPage != null)
                {
                    await Navigation.PushModalAsync(targetPage);
                }
            }
            finally
            {
                _isNavigating = false;
            }
        }
    }

    // =========================================================
    // الاختبار الرسمي
    // =========================================================

    private async void QuizButton_Clicked(
        object sender,
        EventArgs e)
    {
        StopAutoRecite();

        if (_isNavigating)
            return;

        _isNavigating = true;

        try
        {
            var wordsForExam = vm.Titles.ToList();

            if (wordsForExam.Any())
            {
                await Navigation.PushModalAsync(
                    new ExamTopTenPage(wordsForExam)
                );
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    // =========================================================
    // الضغط على الرأس
    // =========================================================

    private async void OnHeaderTapped(
        object sender,
        EventArgs e)
    {
        if (_isNavigating)
            return;

        _isNavigating = true;

        try
        {
            await Toast.Make(
                "هذه الميزة قيد التطوير، وستكون متاحة قريبًا لمساعدتك في تثبيت ما تعلمته 📚"
            ).Show();
        }
        finally
        {
            _isNavigating = false;
        }
    }

    // =========================================================
    // وضع التلقين الصوتي التلقائي
    // Hands-Free Recitation
    // =========================================================

    private async void OnAutoReciteTapped(
        object sender,
        TappedEventArgs e)
    {
        if (_isRecitingActive)
        {
            StopAutoRecite();
            return;
        }

        if (vm.Titles == null || vm.Titles.Count == 0)
            return;

        // إلغاء أي استماع حالي للمايك
        if (_currentListeningWord != null)
        {
            _currentListeningWord.IsListening = false;

            SpeechRecognitionService.CancelListening();

            _currentListeningWord = null;
        }

        // إظهار إعدادات التلقين
        var popup = new AutoReciteSettingsPopup();

        var result = await this.ShowPopupAsync(popup);

        if (result is AutoReciteConfig config)
        {
            await StartAutoReciteAsync(config);
        }
    }

    // =========================================================
    // إيقاف التلقين التلقائي
    // =========================================================

    private void StopAutoRecite()
    {
        if (!_isRecitingActive)
            return;

        try
        {
            _autoReciteCts?.Cancel();
            _autoReciteCts?.Dispose();

            _autoReciteCts = null;
        }
        catch { }

        _isRecitingActive = false;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AutoReciteImage.Source = "headphones_icon.svg";

            BtnAutoReciteIcon.Stroke =
                new SolidColorBrush(Color.FromArgb("#38BDF8"));

            BtnAutoReciteIcon.BackgroundColor =
                Color.FromArgb("#0B111D");
        });

        if (vm?.Titles != null)
        {
            foreach (var w in vm.Titles)
            {
                w.IsReciting = false;
                w.CurrentLanguage = "EN";
            }
        }
    }

    // =========================================================
    // تشغيل التلقين التلقائي
    // =========================================================

    private async Task StartAutoReciteAsync(
        AutoReciteConfig config)
    {
        _isRecitingActive = true;

        _autoReciteCts = new CancellationTokenSource();

        var token = _autoReciteCts.Token;

        AutoReciteImage.Source = "pause_icon.svg";

        BtnAutoReciteIcon.Stroke =
            new SolidColorBrush(Color.FromArgb("#F43F5E"));

        BtnAutoReciteIcon.BackgroundColor =
            Color.FromArgb("#1F1320");

        try
        {
            int currentRound = 0;

            do
            {
                currentRound++;

                var wordsList = vm.Titles.ToList();

                for (int i = 0; i < wordsList.Count; i++)
                {
                    if (token.IsCancellationRequested)
                        break;

                    var word = wordsList[i];

                    if (word.Locked)
                        continue;

                    // تمييز البطاقة والتمرير إليها
                    word.IsReciting = true;

                    try
                    {
                        CV.ScrollTo(
                            word,
                            position: ScrollToPosition.Center,
                            animate: true
                        );
                    }
                    catch { }

                    // تكرار نطق الكلمة
                    for (int r = 0; r < config.WordRepeats; r++)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        // 1. نطق الكلمة بالإنجليزية
                        word.CurrentLanguage = "EN";

                        try
                        {
                            await TextToSpeech.Default.SpeakAsync(
                                word.EnglishWord,
                                cancelToken: token
                            );
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch { }

                        if (token.IsCancellationRequested)
                            break;

                        // 2. عرض المعنى العربي
                        word.CurrentLanguage = "AR";

                        await Task.Delay(1300, token);

                        if (token.IsCancellationRequested)
                            break;

                        // 3. إعادة عرض الكلمة الإنجليزية
                        word.CurrentLanguage = "EN";

                        if (r < config.WordRepeats - 1)
                        {
                            await Task.Delay(500, token);
                        }
                    }

                    word.IsReciting = false;

                    await Task.Delay(700, token);
                }

                if (!config.IsInfiniteLoop &&
                    currentRound >= config.GroupRounds)
                {
                    break;
                }

            } while (!token.IsCancellationRequested);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[StartAutoReciteAsync] Error: {ex.Message}"
            );
        }
        finally
        {
            StopAutoRecite();
        }
    }

}
