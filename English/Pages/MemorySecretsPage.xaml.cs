namespace English.Pages;

public partial class MemorySecretsPage : ContentPage
{
    public MemorySecretsPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}

