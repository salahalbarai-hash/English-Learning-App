using English.ViewModels;
using Microsoft.Maui.Controls;

namespace English.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsVM vm;

    public SettingsPage()
    {
        InitializeComponent();

        // ربط الـ ViewModel
        vm = new SettingsVM();
        BindingContext = vm;
    }
    private async void OnNavigateToFriendsClicked(object sender, EventArgs e)
    {
        // الانتقال إلى شاشة الأصدقاء
        await Navigation.PushModalAsync(new FriendsPage());
    }
}
