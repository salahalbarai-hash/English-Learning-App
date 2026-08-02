using English.ViewModels;
using English.Hubs;
using Microsoft.AspNetCore.SignalR.Client;

namespace English.Pages;

public partial class DuelGamePage : ContentPage
{
    private readonly DuelGameVM _viewModel;

    public DuelGamePage(HubConnection hubConnection, string roomName, string currentUserName, string opponentName, string category, bool isFirstPlayer)
    {
        InitializeComponent();

        // 🟢 جلب كائن GameHub من AppShell لتمريره للـ ViewModel
        GameHub? gameHub = null;
        if (Shell.Current is AppShell appShell)
        {
            gameHub = appShell.GameHub;
        }

        _viewModel = new DuelGameVM(hubConnection, gameHub!, roomName, currentUserName, opponentName, category, isFirstPlayer);
        BindingContext = _viewModel;
        _viewModel.ScrollToBottomRequested = ScrollToBottom;

        // 🟢 تخصيص سلوك زر الرجوع في الشريط العلوي (Shell Back Button)
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            Command = new Command(async () => await ConfirmExitAsync())
        });
    }

    private bool _isLeaving = false;

    // 🟢 اعتراض زر الرجوع الفعلي في الهاتف (Hardware Back Button)
    protected override bool OnBackButtonPressed()
    {
        Dispatcher.Dispatch(async () => await ConfirmExitAsync());
        return true; // منع الخروج المباشر
    }

    // 🟢 دالة تأكيد الانسحاب والخروج
    private async Task ConfirmExitAsync()
    {
        if (_isLeaving) return;

        // إذا انتهت اللعبة مسبقاً، اخرج مباشرة بدون تأكيد
        if (_viewModel.IsGameOver)
        {
            _isLeaving = true;
            await SafePopAsync();
            return;
        }

        bool confirm = await DisplayAlert("تأكيد الانسحاب", "هل أنت متأكد أنك تريد الانسحاب؟ سيتم إنهاء اللعبة واحتساب فوز للخصم.", "نعم، انسحب", "إلغاء");

        if (confirm)
        {
            _isLeaving = true;
            // إرسال طلب الانسحاب للخصم عبر الـ ViewModel
            await _viewModel.SurrenderAsync();
            // العودة للصفحة السابقة
            await SafePopAsync();
        }
    }

    private async Task SafePopAsync()
    {
        try
        {
            if (Navigation.ModalStack.Count > 0)
            {
                await Navigation.PopModalAsync();
            }
            else if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
        }
        catch { }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is DuelGameVM vm)
        {
            vm.Dispose();
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