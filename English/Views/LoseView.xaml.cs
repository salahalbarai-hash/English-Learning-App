using CommunityToolkit.Maui.Views;
using English.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using System;
using System.Threading.Tasks;

namespace English.Views;

public partial class LoseView : ContentView
{

    public LoseView()
    {
        InitializeComponent();
    }

    private void RepeatImgBtn_Clicked(object sender, EventArgs e)
    {
        Shell.Current.GoToAsync("..");
    }

    private void HomeImgBtn_Clicked(object sender, EventArgs e)
    {
        Application.Current.Windows[0].Page = new AppShell();
    }
}
