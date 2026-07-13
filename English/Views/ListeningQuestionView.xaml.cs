using English.Pages;
using English.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace English.Views;

public partial class ListeningQuestionView : ContentView
{
    private ListeningQuestionsViewVM vm;

    public ListeningQuestionView(ExamPage examPage)
    {
        InitializeComponent();
        BindingContext = vm = new ListeningQuestionsViewVM(examPage, mediaElement);
    }

    private async void ContentView_Loaded(object sender, EventArgs e)
    {
        await Task.Delay(500);
        await TextToSpeech.SpeakAsync(vm.CurrentQuestion.Title, new CancellationToken());
    }

    private void ContentView_Unloaded(object sender, EventArgs e)
    {
        try
        {
            mediaElement?.Stop();
        }
        catch (Exception ex)
        {
            // يمكن عرض Toast إذا احببت، او تجاهله
        }
    }
}
