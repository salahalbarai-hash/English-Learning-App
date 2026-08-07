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

    private async void OnHeaderTapped(object sender, EventArgs e)
    {
        await Toast.Make("هذه الميزة قيد التطوير، وستكون متاحة قريبًا لمساعدتك في تثبيت ما تعلمته 📚").Show();
        //try
        //{
        //    int memorizedWords = Preferences.Get("MemorizedWords", 0);
        //    if (memorizedWords < 30)
        //    {
        //        await Toast.Make("احفظ 30 كلمة على الأقل لفتح فيديوهات التعلم 🎬").Show();
        //        return;
        //    }


        //    await Shell.Current.GoToAsync(nameof(WordVideosPage));

        //}
        //catch (Exception ex)
        //{
        //    await Toast.Make(ex.Message).Show();
        //}
    }
}