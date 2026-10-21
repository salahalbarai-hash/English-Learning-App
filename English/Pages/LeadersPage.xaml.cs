using English.Helpers;
using English.ViewModels;

namespace English.Pages;

public partial class LeadersPage : ContentPage
{
    private LeadersPageVM ViewModel => (LeadersPageVM)BindingContext;

    public LeadersPage()
    {
        InitializeComponent();
        BindingContext = new LeadersPageVM();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ = this.AnimatePageInAsync();

        if (ViewModel.TopTenLeaders.Count == 0)
        {
            await ViewModel.LoadLeadersAsync(isManualRefresh: false);
        }
    }

    private async void RefreshButton_Clicked(object sender, EventArgs e)
    {
        await ViewModel.LoadLeadersAsync(isManualRefresh: true);
    }
}