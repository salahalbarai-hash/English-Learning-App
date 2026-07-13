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

        await Task.Delay(200);

        if (ViewModel.TopTenLeaders.Count == 0)
        {
            await ViewModel.LoadLeadersAsync();
        }
    }

    private async void RefreshView_Refreshing(object sender, EventArgs e)
    {
        await ViewModel.LoadLeadersAsync();
    }
}