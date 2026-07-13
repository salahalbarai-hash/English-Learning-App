using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using English.Models;
using English.Services;
using English.ViewModels;

namespace English.Pages;

public partial class TenWordsPage : ContentPage
{
    private bool _isLoading = false;
    TenWordsVM vm;

    public TenWordsPage()
    {
        InitializeComponent();
        BindingContext = vm = new TenWordsVM();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        int savedWords = Preferences.Get("MemorizedWords", 0);
        if(savedWords > 0)
            vm.MemorizedWordsCount = savedWords;
        else
        {
            vm.ProgressStatus = "عقلك الاَن أرض بكر.. ابدأ بغرس الكلمات! 🌱";
            vm.LoadDailyWords();
        }
        //await Task.Delay(100);
        //AnimateProgress();
    }

    // 🔥 Animation احترافي للبار
    private void AnimateProgress()
    {
        if (BindingContext is not TenWordsVM vm) return;

        double target = vm.ProgressWidth;

        ProgressBar.WidthRequest = 0;

        ProgressBar.Animate(
            "progress",
            new Animation(v => ProgressBar.WidthRequest = v, 0, target),
            length: 800,
            easing: Easing.CubicOut
        );

        // ✨ Pulse effect خفيف
         ProgressBar.ScaleTo(1.05, 200);
         ProgressBar.ScaleTo(1, 200);
    }

    private async void OnWordTapped(object sender, TappedEventArgs e)
    {
        if (sender is Border border && border.Content is Label label && border.BindingContext is WordModel word)
        {
            if (word.Locked) return;

            _ = TextToSpeech.Default.SpeakAsync(word.EnglishWord);

            await label.ScaleTo(1.1, 120, Easing.CubicOut);
            await label.ScaleTo(1.0, 120, Easing.CubicIn);
        }
    }

    private async void OnWordLanguageTapped(object sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is WordModel word)
        {
            if (word.Locked) return;

            if (border.Content is Label label)
            {
                await label.RotateXTo(90, 120, Easing.Linear);
                word.CurrentLanguage = (word.CurrentLanguage == "EN") ? "AR" : "EN";
                await label.RotateXTo(0, 120, Easing.Linear);
            }
        }
    }

    private async void QuizButton_Clicked(object sender, EventArgs e)
    {
        var wordsForExam = vm.Titles.ToList();

        if (wordsForExam.Any())
            await Navigation.PushModalAsync(new ExamTopTenPage(wordsForExam));
    }

    private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        if (_isLoading) return;
        _isLoading = true;

        await this.FadeTo(0.95, 150);

        try
        {
            string username = Preferences.Get("UserName", "");
            string password = Preferences.Get("Password", "");

            var user = await Service.GetUser(new User { UserName = username, Password = password });

            if (user is not null)
            {
                Preferences.Set("MemorizedWords", user.MemorizedWords);
                if (!string.IsNullOrEmpty(user.YER)) Preferences.Set("YER", user.YER);
                if (!string.IsNullOrEmpty(user.TimeFinalExam)) Preferences.Set("TimeFinalExam", user.TimeFinalExam);

                /*
                int count = Convert.ToInt32(user.WordsCount);
                Preferences.Set("MemorizedWords", count);

                if (BindingContext is TenWordsVM vm)
                    vm.MemorizedWordsCount = count;
                */
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"خطأ: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
            await this.FadeTo(1, 150);
        }
    }
    private async void OnHeaderTapped(object sender, TappedEventArgs e)
    {
        LoadingOverlay.IsVisible = true;
        try
        {
            int memorizedWords = Preferences.Get("MemorizedWords", 0);
            long id = Convert.ToInt64(Preferences.Get("ID", "0"));

            if (await Service.HasActiveInternetAsync(5))
            {
                User user = await Service.GetUser(new User
                {
                    UserName = Preferences.Get("UserName", ""),
                    Password = Preferences.Get("Password", ""),
                });

                if (memorizedWords < user.MemorizedWords)
                    memorizedWords = user.MemorizedWords;

                string result = await Service.UpdateMemorizedWords(new User
                {
                    ID = id,
                    MemorizedWords = memorizedWords
                });

                vm.MemorizedWordsCount = memorizedWords;

                string message = "تم الحفظ بنجاح 🔥";
                if (result != "1") message = "حدث خطأ 😓";

                await Toast.Make(message, ToastDuration.Short, 14).Show(new CancellationToken());
            }
            else
            {
                await Toast.Make("يرجى الاتصال بالانترنت 📶", ToastDuration.Short, 14).Show(new CancellationToken());
            }
        }
        catch (Exception ex)
        {
            await Toast.Make(ex.Message).Show();
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
    }
}