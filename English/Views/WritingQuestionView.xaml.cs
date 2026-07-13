using CommunityToolkit.Maui.Views;
using English.Models;
using English.Pages;
using English.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace English.Views;

public partial class WritingQuestionView : ContentView, INotifyPropertyChanged
{
    // قائمة الاسئلة
    private readonly List<QuestionModel> questions;
    private int currentQuestionIndex = 0;
    private int wrongAnswerCount = 0;
    private QuestionModel? currentQuestion;
    private readonly ExamPage examPage;

    public QuestionModel? CurrentQuestion
    {
        get => currentQuestion;
        set
        {
            currentQuestion = value;
            OnPropertyChanged(nameof(CurrentQuestion));
        }
    }

    public WritingQuestionView(ExamPage examPage)
    {
        InitializeComponent();

        this.examPage = examPage;

        // اختيار الاسئلة حسب المجموعة
        questions = Questions.GetQuestions();

        CurrentQuestion = questions.FirstOrDefault();

        BindingContext = this;
    }

    private void Button_Clicked(object sender, EventArgs e)
    {
        string answer = AnswerEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(answer))
            return;

        bool isCorrect = string.Equals(answer, CurrentQuestion?.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
        if (isCorrect)
        {
            mediaElement.Source = MediaSource.FromResource(Sounds.Correct());
        }
        else
        {
            wrongAnswerCount++;
            mediaElement.Source = MediaSource.FromResource(Sounds.Wrong());
        }

        currentQuestionIndex++;

        if (isCorrect || wrongAnswerCount < 2)
        {
            mediaElement.Play();

            if (currentQuestionIndex < questions.Count)
            {
                CurrentQuestion = questions[currentQuestionIndex];
            }
            else if (examPage.timerView != null)
            {
                Win();
            }
            else
            {
                mediaElement.Stop();
                var contentView = examPage.FindByName<ContentView>("MainContentHolder");
                contentView.Content = new ListeningQuestionView(examPage);
            }
        }
        else
        {
            Lose();
        }

        AnswerEntry.Text = string.Empty;
    }

    private void Lose()
    {
        mediaElement.Stop();
        examPage.timerView?.StopTimer();
        examPage.Content = new LoseView();
        mediaElement.Source = MediaSource.FromResource(Sounds.GameOver());
        mediaElement.Play();
    }

    private void Win()
    {
        mediaElement.Stop();
        examPage.timerView?.StopTimer();
        examPage.Content = new WinView(mediaElement);
    }

    private void ContentView_Unloaded(object sender, EventArgs e)
    {
        mediaElement?.Stop();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
