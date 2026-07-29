using English.ViewModels;
using Microsoft.Maui.Controls;

namespace English.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsVM vm;

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = vm = new SettingsVM();
    }
}
