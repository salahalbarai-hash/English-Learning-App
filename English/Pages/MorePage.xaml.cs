using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace English.Pages;

public partial class MorePage : ContentPage
{
    private bool _isNavigating = false;

    public MorePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isNavigating = false;
    }

    private async void MenuClicked(object sender, TappedEventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        try
        {
            if (sender is not Border card)
                return;

            // تأثير الضغط
            await card.ScaleTo(0.97, 60);
            await card.ScaleTo(1, 60);

            switch (card.ClassId)
            {
                case "Profile":
                    await Navigation.PushModalAsync(new SettingsPage());
                    break;
                case "Friends":
                    await Navigation.PushModalAsync(new FriendsPage());
                    break;
                case "Messages":
                    await Navigation.PushModalAsync(new MessagesPage());
                    break;
                case "Share":
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Title = "مشاركة التطبيق",
                        Text = "جرب تطبيق تعلم الإنجليزية بطريقة ممتعة"
                    });
                    break;
                case "About":
                    await Navigation.PushModalAsync(new AboutPage());
                    break;
                case "Logout":
                    bool result = await DisplayAlert("تسجيل الخروج", "هل تريد تسجيل الخروج من الحساب؟", "نعم", "إلغاء");
                    if (result)
                    {
                        Preferences.Set("IsLogin", "0");
                        Application.Current!.Windows[0].Page = new LoginPage();
                    }
                    break;
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }
}