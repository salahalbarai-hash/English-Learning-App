using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using English.Services;
using English.ViewModels;
using English.Helpers;

namespace English.Pages;

public partial class GroupPage : ContentPage
{
    public WordsVM vm;

    public GroupPage()
    {
        InitializeComponent();

        // إخفاء العناصر لتفادي الوميض (Flashing)
        if (this.Content is Layout rootLayout)
        {
            foreach (var child in rootLayout.Children.OfType<VisualElement>())
            {
                child.Opacity = 0;
                child.TranslationY = 40;
            }
        }

        vm = new WordsVM();
        BindingContext = vm;
        // إعداد غير متزامن لتحميل الكلمات
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        await vm.LoadAsync();
    }

    /// <summary>
    /// فتح كلمة مغلقة او الانتقال للاختبار
    /// </summary>
    private async Task UnlockWordAsync(Label lockLbl, Label titleLbl)
    {
        if (GlobalVariables.CurrentTitle.Contains("Quiz"))
        {
            // إذا كانت كلمة اختبار، افتح صفحة الاختبار
            await Shell.Current.GoToAsync(nameof(ExamPage));
        }
        else
        {
            // إلغاء قفل الكلمة العادية
            await Service.UnLock(GlobalVariables.CurrentTitle);
            titleLbl.Text = GlobalVariables.CurrentTitle;
            lockLbl.IsVisible = false;

            var word = vm.Titles.FirstOrDefault(t => t.ArabicWord == titleLbl.ClassId);
            if (word != null)
            {
                word.EnglishWord = titleLbl.Text;
                word.Locked = false;
            }
        }
    }

    /// <summary>
    /// التعامل مع الضغط على الكلمة
    /// </summary>
    private async void OnWordTapped(object sender, TappedEventArgs e)
    {
        if (ActivityIndicator.IsRunning)
            return;

        var titleLbl = sender as Label;
        if (titleLbl == null) return;

        var wordModel = vm.Titles.FirstOrDefault(t => t.ArabicWord == titleLbl.ClassId);
        if (wordModel == null) return;

        GlobalVariables.CurrentTitle = wordModel.EnglishWord ?? "";

        // إيجاد الـ Label المقفل في الـ Grid
        var grid = titleLbl.Parent?.Parent as Grid;
        var lockLbl = grid?.Children.OfType<Label>().FirstOrDefault(c => c != titleLbl);

        if (lockLbl != null && lockLbl.IsVisible)
        {
            if (GlobalVariables.CurrentTitle.Contains("Quiz") &&
                vm.Titles.Any(t => t.Locked && !t.EnglishWord.Contains("Quiz")))
            {
                await Toast.Make("يرجى حفظ جميع الكلمات المغلقة قبل الدخول للإختبار", ToastDuration.Short, 14).Show();
            }
            else
            {
                await UnlockWordAsync(lockLbl, titleLbl);
            }
        }
        else if (GlobalVariables.CurrentTitle.Contains("Quiz"))
        {
            await Navigation.PushModalAsync(new ExamPage());
        }
        else
        {
            // قراءة الكلمة صوتيًا
            await TextToSpeech.SpeakAsync(GlobalVariables.CurrentTitle, new CancellationToken());
        }
    }

    /// <summary>
    /// تبديل لغة الكلمة عند الضغط
    /// </summary>
    private async void OnWordLanguageTapped(object sender, TappedEventArgs e)
    {
        if (ActivityIndicator.IsRunning)
            return;

        var titleLbl = sender as Label;
        if (titleLbl == null) return;

        var wordModel = vm.Titles.FirstOrDefault(w => w.ArabicWord == titleLbl.ClassId);
        if (wordModel == null) return;

        // تعيين الكلمة الإنجليزية إذا كانت مجهولة
        if (wordModel.EnglishWord == "?")
        {
            var words = await Words.TagAsync(GlobalVariables.CurrentGroup);
            wordModel.EnglishWord = words
                .FirstOrDefault(i => i.ArabicWord == titleLbl.ClassId)?.EnglishWord;
        }

        // تبديل اللغة
        if (wordModel.CurrentLanguage == "en")
        {
            if (titleLbl.Text != "?")
            {
                titleLbl.Text = wordModel.ArabicWord;
                wordModel.CurrentLanguage = "ar";
            }
        }
        else
        {
            titleLbl.Text = wordModel.EnglishWord;
            wordModel.CurrentLanguage = "en";
        }
    }

    /// <summary>
    /// إعادة ربط الـ BindingContext لتحديث الـ CollectionView
    /// </summary>
    public void ReloadBinding()
    {
        BindingContext = null;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.AnimatePageInAsync();
    }
}
