using English.ViewModels;
using English.Models;

namespace English.Pages;

public partial class GuessGamePage : ContentPage
{
    private GuessGameVM _viewModel;

    public GuessGamePage()
    {
        InitializeComponent();
        _viewModel = new GuessGameVM();
        BindingContext = _viewModel;

        // الاشتراك في حدث التمرير لأسفل
        _viewModel.ScrollToBottomRequested = ScrollToBottom;
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
}