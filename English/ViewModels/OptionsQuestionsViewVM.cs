using CommunityToolkit.Maui.Views;
using English.Models;
using English.Pages;
using English.Services;
using English.Views;
using Microsoft.Maui.Controls;
using System.ComponentModel;
using System.Windows.Input;

#nullable enable
namespace English.ViewModels
{
    public class OptionsQuestionsViewVM : INotifyPropertyChanged
    {
        private MediaSource? mediaSource;
        private QuestionModel? currentQuestion;
        private int currentQuestionIndex = 0;
        private int wrongAnswer = 0;

        private readonly ExamPage examPage;
        private readonly MediaElement mediaElement;

        public List<QuestionModel> Questions { get; }

        public MediaSource? MediaSource
        {
            get => mediaSource;
            set
            {
                mediaSource = value;
                OnPropertyChanged(nameof(MediaSource));
            }
        }

        public QuestionModel? CurrentQuestion
        {
            get => currentQuestion;
            set
            {
                currentQuestion = value;
                OnPropertyChanged(nameof(CurrentQuestion));
            }
        }

        public ICommand NextCommand { get; }
        public ICommand OptionSelectedCommand { get; }

        public OptionsQuestionsViewVM(ExamPage examPage, MediaElement mediaElement)
        {
            this.examPage = examPage;
            this.mediaElement = mediaElement;

            Questions = Services.Questions.GetQuestions();
            CurrentQuestion = Questions[0];

            NextCommand = new Command(OnNextQuestion);
            OptionSelectedCommand = new Command<string>(OnOptionSelected);
        }

        private void OnOptionSelected(string selectedAnswer)
        {
            if (CurrentQuestion == null)
                return;

            CurrentQuestion.SelectedAnswer = selectedAnswer;
            OnPropertyChanged(nameof(CurrentQuestion));
        }

        private void OnNextQuestion()
        {
            if (string.IsNullOrWhiteSpace(CurrentQuestion?.SelectedAnswer))
                return;

            bool isCorrect = CurrentQuestion.SelectedAnswer == CurrentQuestion.CorrectAnswer;
            bool hasNext = ++currentQuestionIndex < Questions.Count;

            if (isCorrect)
            {
                MediaSource = MediaSource.FromResource(Sounds.Correct());
            }
            else if (wrongAnswer++ < 1)
            {
                MediaSource = MediaSource.FromResource(Sounds.Wrong());
            }
            else
            {
                mediaElement.Stop();
                examPage.timerView?.StopTimer();
                MediaSource = MediaSource.FromResource(Sounds.GameOver());
                examPage.Content = new LoseView();
                return;
            }

            if (hasNext)
            {
                CurrentQuestion = Questions[currentQuestionIndex];
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
                    var contentView = examPage.FindByName<ContentView>("MainContentHolder");
                    contentView.Content = new WritingQuestionView(examPage);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
