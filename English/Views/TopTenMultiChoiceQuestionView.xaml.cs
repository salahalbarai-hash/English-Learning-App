using CommunityToolkit.Maui.Views;
using English.Models;
using English.Services;
using English.ViewModels;
using Microsoft.Maui.Controls;

namespace English.Views;

public partial class TopTenMultiChoiceQuestionView : ContentView
{
    public TopTenMultiChoiceQuestionView()
    {
        InitializeComponent();
    }
    private async void OnOptionSelected_Tapped(object sender, TappedEventArgs e)
    {
        // الحصول على الـ ViewModel من الصفحة الأم
        var page = this.FindParent<ContentPage>();
        var vm = page?.BindingContext as QuizTopTenVM;
        if (vm == null) return;

        if (sender is Border border && border.Content is Label label)
        {
            string selectedAnswer = label.Text;
            bool isCorrect = await vm.ProcessAnswer(selectedAnswer);

            var feedbackColor = isCorrect ? Colors.Green : Colors.Red;
            border.Stroke = feedbackColor;
            border.Shadow = new Shadow { Brush = feedbackColor, Radius = 25, Opacity = 0.8f };

            await Task.Delay(400);
            ResetBorderStyle(border);
        }
    }

    private void ResetBorderStyle(Border border)
    {
        var originalColor = Color.FromArgb("#A855F7");
        border.Stroke = originalColor;
        border.Shadow = new Shadow { Brush = originalColor, Radius = 10, Opacity = 0.4f };
    }
}