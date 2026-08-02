using English.ViewModels;
using English.Models;

namespace English.Pages;

public partial class GuessGamePage : ContentPage
{
    private GuessGameVM _viewModel;
    private bool _isLeaving = false;

    public GuessGamePage(string selectedCategory)
    {
        InitializeComponent();
        _viewModel = new GuessGameVM(selectedCategory);
        BindingContext = _viewModel;
        _viewModel.ScrollToBottomRequested = ScrollToBottom;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await ConfirmExitAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        Dispatcher.Dispatch(async () => await ConfirmExitAsync());
        return true;
    }

    private async Task ConfirmExitAsync()
    {
        if (_isLeaving) return;

        bool confirm = await DisplayAlert("تأكيد الخروج", "هل أنت متأكد من الخروج من التحدي الحالي؟", "نعم", "لا");
        if (confirm)
        {
            _isLeaving = true;
            await Navigation.PopModalAsync();
        }
    }

   

    private void ScrollToBottom()
    {
        if (_viewModel.ChatMessages.Count > 0)
        {
            ChatCollectionView.ScrollTo(_viewModel.ChatMessages.Last(), position: ScrollToPosition.End, animate: true);
        }
    }
}

public class ChatMessage
{
    public string? Text { get; set; }
    public bool IsUser { get; set; }
    public bool IsAI { get; set; }
}

public class WordItem
{
    public int Id { get; set; }
    public string? EnglishWord { get; set; }
    public string? ArabicWord { get; set; }
    public string? Category { get; set; }
}