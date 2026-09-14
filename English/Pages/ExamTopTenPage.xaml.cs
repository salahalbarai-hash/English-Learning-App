using English.DesignControls;
using English.Models;
using English.Services;
using English.ViewModels;

namespace English.Pages
{
    public partial class ExamTopTenPage : ContentPage
    {
        private readonly QuizTopTenVM _vm;
        private readonly TimerView _masterTimer;

        public ExamTopTenPage(List<WordModel> words)
        {
            InitializeComponent();

            _vm = new QuizTopTenVM(words);
            BindingContext = _vm;

            // إعداد التايمر يدوياً
            _masterTimer = new TimerView(100);
            _masterTimer.TimerFinished += (s, e) =>
                HandleGameOver("⏱️ انتهى الوقت! حاول أن تكون أسرع.");

            MasterTimerContainer.Content = _masterTimer;

            // أحداث الـ VM
            _vm.OnGameOver = () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    HandleGameOver("انتهت المحاولات! عُد أقوى في المرة القادمة 🦾"));
            };

            _vm.OnWin = (seconds) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    HandleWin(seconds));
            };
        }

        private async void HandleGameOver(string reason)
        {
            _masterTimer?.StopTimer();
            await Navigation.PushModalAsync(new GameOverPage(reason));
        }

        private async void HandleWin(int elapsedSeconds)
        {
            _masterTimer?.StopTimer();

            int finalTime =
                elapsedSeconds > 0
                ? elapsedSeconds
                : (100 - _masterTimer.GetRemainingTime());

            await Navigation.PushModalAsync(new WinPage(finalTime), true);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            _masterTimer?.StopTimer();

            _vm.OnGameOver = null;
            _vm.OnWin = null;
        }
    }
}