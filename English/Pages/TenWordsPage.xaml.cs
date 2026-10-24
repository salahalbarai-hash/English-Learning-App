using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Views;
using English.Popups;
using English.Services;
using English.ViewModels;

namespace English.Pages;

public partial class TenWordsPage : ContentPage
{
    private bool _isLoading = false;
    private bool _isNavigating = false;
    TenWordsVM vm;

    private CancellationTokenSource? _autoReciteCts;
    private bool _isRecitingActive;

    public TenWordsPage()
    {
        InitializeComponent();
        BindingContext = vm = new TenWordsVM();
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
        try { savedWords = Preferences.Get("MemorizedWords", 0L); }
        catch { try { savedWords = Preferences.Get("MemorizedWords", 0); } catch { } }

        vm.MemorizedWordsCount = savedWords;
        if (vm.Titles.Count == 0)
        {
            vm.LoadDailyWords();
        }
    }

    private void OnProgressBarContainerSizeChanged(object sender, EventArgs e)
    {
        if (sender is Grid grid && grid.Width > 0 && vm != null)
        {
            vm.UpdateContainerWidth(grid.Width);
        }
    }

    private static Label? FindWordLabel(Border border)
    {
        if (border.Content is Label label) return label;
        if (border.Content is Grid grid)
        {
            return grid.Children.OfType<Label>().FirstOrDefault(l => Grid.GetColumn(l) == 1) 
                ?? grid.Children.OfType<Label>().FirstOrDefault();
        }
        return null;
    }

    private async void OnWordTapped(object sender, TappedEventArgs e)
    {
        if (_isRecitingActive) StopAutoRecite();

        if (sender is Border border && border.BindingContext is WordModel word)
        {
            if (word.Locked) return;

            _ = TextToSpeech.Default.SpeakAsync(word.EnglishWord);

            var label = FindWordLabel(border);
            if (label != null)
            {
                await label.ScaleTo(1.1, 120, Easing.CubicOut);
                await label.ScaleTo(1.0, 120, Easing.CubicIn);
            }
        }
    }

    private async void OnWordLanguageTapped(object sender, TappedEventArgs e)
    {
        if (_isRecitingActive) StopAutoRecite();

        if (sender is Border border && border.BindingContext is WordModel word)
        {
            if (word.Locked) return;

            var label = FindWordLabel(border);
            if (label != null)
            {
                await label.RotateXTo(90, 120, Easing.Linear);
                word.CurrentLanguage = (word.CurrentLanguage == "EN") ? "AR" : "EN";
                await label.RotateXTo(0, 120, Easing.Linear);
            }
            else
            {
                word.CurrentLanguage = (word.CurrentLanguage == "EN") ? "AR" : "EN";
            }
        }
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

    private WordModel? _currentListeningWord;
    private bool _isProcessingMic;

    private async void OnMicTapped(object sender, TappedEventArgs e)
    {
        if (_isRecitingActive) StopAutoRecite();

        if (_isProcessingMic) return;
        _isProcessingMic = true;

        try
        {
            if (sender is not Border micBorder || micBorder.BindingContext is not WordModel word)
                return;

            if (word.Locked) return;

            // إذا كان هناك استماع لكلمة أخرى، نلغيه بأمان
            if (_currentListeningWord != null && _currentListeningWord != word)
            {
                _currentListeningWord.IsListening = false;
                SpeechRecognitionService.CancelListening();
            }

            // إذا ضغط المستخدم على نفس الزر مرة ثانية لإلغاء الاستماع
            if (word.IsListening)
            {
                word.IsListening = false;
                SpeechRecognitionService.CancelListening();
                _currentListeningWord = null;
                return;
            }

            _currentListeningWord = word;
            word.IsListening = true;

            // أنيميشن نبض خفيف على زر المايك
            try
            {
                await micBorder.ScaleTo(1.2, 120, Easing.CubicOut);
                await micBorder.ScaleTo(1.0, 120, Easing.CubicIn);
            }
            catch { }

            try
            {
                await Toast.Make($"🎙️ استمع إليك الآن... انطق: {word.EnglishWord}").Show();
            }
            catch { }

            var (success, spokenText, error) = await SpeechRecognitionService.ListenAndEvaluateAsync(
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
                try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                // أنيميشن تكبير خفيف على زر النجمة نفسه بدلاً من كارت القائمة بأكمله
                try
                {
                    await micBorder.ScaleTo(1.25, 140, Easing.CubicOut);
                    await micBorder.ScaleTo(1.0, 140, Easing.CubicIn);
                }
                catch { }

                try
                {
                    await Toast.Make($"🌟 أحسنت! نطقك سليم 100% ({word.EnglishWord})").Show();
                }
                catch { }

                // إبقاء اللون الأخضر والنجمة ظاهرة لمدة ثانيتين كإشعار نجاح، ثم إعادتها إلى حالتها الطبيعية
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
                    try { await Toast.Make(error).Show(); } catch { }
                }
                else if (!string.IsNullOrWhiteSpace(spokenText))
                {
                    try
                    {
                        await Toast.Make($"⚠️ سمعت: \"{spokenText}\" | الصحيح: \"{word.EnglishWord}\"").Show();
                    }
                    catch { }
                    await Task.Delay(350);
                    _ = TextToSpeech.Default.SpeakAsync(word.EnglishWord);
                }
                else
                {
                    try
                    {
                        await Toast.Make("لم يتم التقاط صوت واضح، حاول مرة أخرى 🔁").Show();
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

    private async void QuizButton_Clicked(object sender, EventArgs e)
    {
        StopAutoRecite();
        if (_isNavigating) return;
        _isNavigating = true;

        try
        {
            var wordsForExam = vm.Titles.ToList();

            if (wordsForExam.Any())
                await Navigation.PushModalAsync(new ExamTopTenPage(wordsForExam));
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private async void OnHeaderTapped(object sender, EventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        try
        {
            await Toast.Make("هذه الميزة قيد التطوير، وستكون متاحة قريبًا لمساعدتك في تثبيت ما تعلمته 📚").Show();
        }
        finally
        {
            _isNavigating = false;
        }
    }

    #region وضع التلقين الصوتي التلقائي (Hands-Free Recitation)

    private async void OnAutoReciteTapped(object sender, TappedEventArgs e)
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

        // إظهار نافذة الإعدادات السريعة
        var popup = new AutoReciteSettingsPopup();
        var result = await this.ShowPopupAsync(popup);

        if (result is AutoReciteConfig config)
        {
            await StartAutoReciteAsync(config);
        }
    }

    private void StopAutoRecite()
    {
        if (!_isRecitingActive) return;

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
            BtnAutoReciteIcon.Stroke = new SolidColorBrush(Color.FromArgb("#38BDF8"));
            BtnAutoReciteIcon.BackgroundColor = Color.FromArgb("#0B111D");
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

    private async Task StartAutoReciteAsync(AutoReciteConfig config)
    {
        _isRecitingActive = true;
        _autoReciteCts = new CancellationTokenSource();
        var token = _autoReciteCts.Token;

        // تحديث زر الواجهة لحالة التشغيل (أيقونة الإيقاف المؤقت بلون وردي تنبيهي أنيق)
        AutoReciteImage.Source = "pause_icon.svg";
        BtnAutoReciteIcon.Stroke = new SolidColorBrush(Color.FromArgb("#F43F5E"));
        BtnAutoReciteIcon.BackgroundColor = Color.FromArgb("#1F1320");

        try
        {
            int currentRound = 0;
            do
            {
                currentRound++;
                var wordsList = vm.Titles.ToList();

                for (int i = 0; i < wordsList.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    var word = wordsList[i];
                    if (word.Locked) continue;

                    // تمييز الكرت والتمرير إليه
                    word.IsReciting = true;
                    try
                    {
                        CV.ScrollTo(word, position: ScrollToPosition.Center, animate: true);
                    }
                    catch { }

                    // تنفيذ تكرار نطق الكلمة حسب اختيار المستخدم
                    for (int r = 0; r < config.WordRepeats; r++)
                    {
                        if (token.IsCancellationRequested) break;

                        // 1. التأكد أن الكرت بالإنجليزية ونطقها بوضوح بصوت نقي
                        word.CurrentLanguage = "EN";
                        try
                        {
                            await TextToSpeech.Default.SpeakAsync(word.EnglishWord, cancelToken: token);
                        }
                        catch (OperationCanceledException) { break; }
                        catch { }

                        if (token.IsCancellationRequested) break;

                        // 2. قلب الكرت لإظهار المعنى بالعربي للقراءة بصرياً
                        word.CurrentLanguage = "AR";
                        await Task.Delay(1300, token);
                        if (token.IsCancellationRequested) break;

                        // 3. إعادة الكرت للإنجليزية
                        word.CurrentLanguage = "EN";

                        if (r < config.WordRepeats - 1)
                        {
                            // مهلة خفيفة قبل إعادة النطق للكلمة نفسها
                            await Task.Delay(500, token);
                        }
                    }

                    word.IsReciting = false;

                    // فاصل مريح قبل الانتقال للكلمة التالية
                    await Task.Delay(700, token);
                }

                // إذا لم يكن الوضع مستمراً، نتحقق من اكتمال عدد الدورات المحدد
                if (!config.IsInfiniteLoop && currentRound >= config.GroupRounds)
                {
                    break;
                }

            } while (!token.IsCancellationRequested);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartAutoReciteAsync] Error: {ex.Message}");
        }
        finally
        {
            StopAutoRecite();
        }
    }

    #endregion
}