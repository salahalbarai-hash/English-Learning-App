using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using English.Models;
using English.Pages;
using English.Services;
using English.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
#nullable enable
namespace English.ViewModels
{
    public class ListeningQuestionsViewVM : INotifyPropertyChanged
    {
        private QuestionModel currentQuestion;
        private int currentQuestionIndex = 0;
        private int wrongAnswer = 0;
        private MediaSource? mediaSource;

        private readonly ExamPage examPage;
        private readonly MediaElement mediaElement;

        public List<QuestionModel> Questions { get; }

        public QuestionModel CurrentQuestion
        {
            get => currentQuestion;
            set
            {
                currentQuestion = value;
                OnPropertyChanged(nameof(CurrentQuestion));
            }
        }

        public MediaSource? MediaSource
        {
            get => mediaSource;
            set
            {
                mediaSource = value;
                OnPropertyChanged(nameof(MediaSource));
            }
        }

        public ICommand SpeakCommand { get; }
        public ICommand SelectedCommand { get; }

        public ListeningQuestionsViewVM(ExamPage examPage, MediaElement mediaElement)
        {
            this.examPage = examPage;
            this.mediaElement = mediaElement;

            // تحميل الاسئلة حسب النوع والمجموعة
            Questions = Services.Questions.GetQuestions(false);

            CurrentQuestion = Questions[0];

            SelectedCommand = new Command<string>(OnOptionSelected);
            SpeakCommand = new Command<object>(Speak);
        }

        private async void OnOptionSelected(string selectedAnswer)
        {
            CurrentQuestion.SelectedAnswer = selectedAnswer;
            OnPropertyChanged(nameof(CurrentQuestion));

            if (CurrentQuestion.SelectedAnswer == CurrentQuestion.CorrectAnswer)
            {
                MediaSource = MediaSource.FromResource(Sounds.Correct());
            }
            else if (wrongAnswer < 1)
            {
                MediaSource = MediaSource.FromResource(Sounds.Wrong());
                wrongAnswer++;
            }
            else
            {
                mediaElement.Stop();
                examPage.timerView?.StopTimer();
                MediaSource = MediaSource.FromResource(Sounds.GameOver());
                examPage.Content = new LoseView();
                return;
            }

            currentQuestionIndex++;

            if (currentQuestionIndex < Questions.Count)
            {
                try
                {
                    CurrentQuestion = Questions[currentQuestionIndex];
                    await Task.Delay(600);
                    await TextToSpeech.SpeakAsync(CurrentQuestion.Title, new CancellationToken());
                }
                catch (Exception ex)
                {
                    var toast = Toast.Make(ex.Message, ToastDuration.Short, 14.0);
                    await toast.Show(new CancellationToken());
                }
            }
            else
            {
                if (examPage.timerView != null)
                {
                    mediaElement.Stop();
                    examPage.timerView.StopTimer();
                    examPage.Content = new WinView(mediaElement);
                }
                else
                {
                    ExamPage.liveTimerView?.StopTimer();
                    var contentView = examPage.FindByName<ContentView>("MainContentHolder");
                    contentView.Content = new WinView(mediaElement, true);
                }
            }
        }

        private void Speak(object obj)
        {
            TextToSpeech.SpeakAsync(CurrentQuestion.Title, new CancellationToken());
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
