using English.Models;
using English.ViewModels;
using Microsoft.Maui.Controls;

namespace English.Views;

public partial class TopTenWritingQuestionView : ContentView
{
    public TopTenWritingQuestionView()
    {
        InitializeComponent();
        Loaded += (s, e) => WordEntry.Focus();
    }

    private async void OnCheckAnswer(object sender, EventArgs e)
    {
        var page = this.FindParent<ContentPage>();
        var vm = page?.BindingContext as QuizTopTenVM;
        if (vm == null) return;

        bool isCorrect = await vm.CheckWritingAnswerAsync();

        var feedbackColor = isCorrect ? Colors.Green : Colors.Red;
        AnswerBorder.Stroke = feedbackColor;
        AnswerBorder.Shadow = new Shadow { Brush = feedbackColor, Radius = 20, Opacity = 0.7f };

        await Task.Delay(400);
        AnswerBorder.Stroke = Color.FromArgb("#1E293B");
        AnswerBorder.Shadow = null;
        WordEntry.Focus();
    }
}