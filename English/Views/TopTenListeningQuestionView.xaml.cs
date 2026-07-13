using English.Models;
using English.ViewModels;
using Microsoft.Maui.Controls;
using System.ComponentModel;

namespace English.Views;

public partial class TopTenListeningQuestionView : ContentView
{
    private QuizTopTenVM _vm;
    private bool _isSubscribed = false;

    public TopTenListeningQuestionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, EventArgs e)
    {
        // الحصول على الـ ViewModel من الصفحة الأم
        var page = this.FindParent<ContentPage>();
        _vm = page?.BindingContext as QuizTopTenVM;

        if (_vm != null && !_isSubscribed)
        {
            // الاشتراك في حدث تغيير الخصائص
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _isSubscribed = true;

            // نطق أول سؤال فوراً
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (_vm != null && _vm.CurrentStage == ExamStage.Listening)
                        await _vm.SpeakCurrentWord();
                });
            });
        }
    }

    private void OnUnloaded(object sender, EventArgs e)
    {
        if (_vm != null && _isSubscribed)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _isSubscribed = false;
        }
    }

    private async void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // عندما تتغير الكلمة الحالية (CurrentWord) ونحن في مرحلة الاستماع، انطق تلقائياً
        if (e.PropertyName == nameof(QuizTopTenVM.CurrentWord))
        {
            // تأخير بسيط لضمان اكتمال تحديث الواجهة
            await Task.Delay(200);
            if (_vm != null && _vm.CurrentStage == ExamStage.Listening)
                await _vm.SpeakCurrentWord();
        }
    }

    private async void OnListenClicked(object sender, EventArgs e)
    {
        if (_vm != null) await _vm.SpeakCurrentWord();
    }

    private async void OnOptionSelected_Tapped(object sender, TappedEventArgs e)
    {
        if (_vm == null) return;

        if (sender is Border border && border.Content is Label label)
        {
            string selectedText = label.Text;
            bool isCorrect = await _vm.ProcessAnswer(selectedText);

            border.Stroke = isCorrect ? Colors.Green : Colors.Red;
            await Task.Delay(400);
            border.Stroke = Color.FromArgb("#FF3D00");
        }
    }
}