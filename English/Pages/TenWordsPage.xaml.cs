using CommunityToolkit.Maui.Alerts;
using English.Services;
using English.ViewModels;

namespace English.Pages;

public partial class TenWordsPage : ContentPage
{
    private bool _isLoading = false;
    private bool _isNavigating = false;
    TenWordsVM vm;

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
}