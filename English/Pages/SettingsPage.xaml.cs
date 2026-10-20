using English.Helpers;
using English.ViewModels;

namespace English.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsVM vm;

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = vm = new SettingsVM();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.AnimatePageInAsync();
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
