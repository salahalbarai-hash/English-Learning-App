using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using English.Models;
using English.Pages;
using English.Services;

namespace English.Views;

public partial class WinView : ContentView
{
    private readonly MediaElement mediaElement;

    public WinView(MediaElement mediaElement, bool showSaveButton = false)
    {
        InitializeComponent();
        this.mediaElement = mediaElement;

        // التحكم في ظهور زر الحفظ
        SaveBtn.IsVisible = showSaveButton;
    }

    private async void ContentView_Loaded(object sender, EventArgs e)
    {
        await Task.Delay(500);

        string key = $"{GlobalVariables.CurrentGroup}.{GlobalVariables.CurrentTitle}";
        if (await Service.IsLock(key))
        {
            await Service.UnLock(key);
            UnlockWord();
        }

        await Task.Delay(500);

        // تشغيل الصوت عند الفوز
        mediaElement.Source = MediaSource.FromResource(Sounds.Win());
        mediaElement.Play();
    }

    private void UnlockWord()
    {
        // البحث عن الصفحة المفتوحة وإلغاء قفل الكلمة
        if (Navigation.ModalStack.FirstOrDefault(p => p is GroupPage) is GroupPage groupPage)
        {
            var word = groupPage.vm.Titles.FirstOrDefault(t => t.EnglishWord.Contains(GlobalVariables.CurrentTitle));
            if (word != null)
            {
                word.Locked = false;
                groupPage.ReloadBinding();
            }
        }
    }

    private void ContentView_Unloaded(object sender, EventArgs e)
    {
        // إيقاف تشغيل الصوت عند مغادرة الصفحة
        mediaElement?.Stop();
    }

    private async void SaveBtn_Clicked(object sender, EventArgs e)
    {
        string id = Preferences.Get("ID", "");

        string time = ExamPage.liveTimerView != null ? ExamPage.liveTimerView.GetTime : "";

        ActivityIndicator.IsRunning = true;

        if (await Service.HasActiveInternetAsync(5))
        {
            string result = await Service.UpdateTimeFinalExam(new TimeFinalExamModel
            {
                Id = id,
                Time = time
            });

            string message = "تم الحفظ بنجاح :)";
            if (result == "1")
            {
                Preferences.Set("TimeFinalExam", time);
            }
            else 
            {
                message = "حدث خطا :(";
            }
            await Toast.Make(message, ToastDuration.Short, 14).Show(new CancellationToken());
        }
        else
        {
            await Toast.Make("يرجى الاتصال بالانترنت", ToastDuration.Short, 14).Show(new CancellationToken());
        }

        ActivityIndicator.IsRunning = false;
    }
}
