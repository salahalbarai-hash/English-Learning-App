using CommunityToolkit.Maui.Alerts;
using English.Services;

namespace English.Pages;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void Button_Clicked(object sender, EventArgs e)
    {
        if (Navigation.ModalStack.Any(p => p is GroupPage))
            return;

        GlobalVariables.CurrentGroup = ((Button)sender).Text;

        if (((Button)sender).Text == "Final Exam")
        {
            GlobalVariables.CurrentTitle = "Final Exam";

            if (Words.AllValuesAreFalse())
                await Navigation.PushModalAsync(new ExamPage());
            else
                await Toast.Make("يجب إكمال جميع المجموعات").Show();
        }
        else
        {
            await Navigation.PushModalAsync(new GroupPage());
        }
    }

    private async void ImageButton_Clicked(object sender, EventArgs e)
    {
         await Navigation.PushModalAsync(new SettingsPage());
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        TimeFinalExam.Text = Preferences.Get("TimeFinalExam", "00:00:00");
    }
}
