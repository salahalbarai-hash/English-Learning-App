using CommunityToolkit.Maui.Views;
using English.DesignControls;
using English.Models;
using English.Services;
using English.Views;
using Microsoft.Maui.Controls;
using System;

#nullable enable
namespace English.Pages
{
    public partial class ExamPage : ContentPage
    {
        // مؤشرات المؤقتات
        public TimerView? timerView;
        public static LiveTimerView? liveTimerView;

        public ExamPage()
        {
            InitializeComponent();

            // إعداد المحتوى حسب نوع الاختبار
            switch (GlobalVariables.CurrentTitle)
            {
                case "Quiz Options":
                    MainContentHolder.Content = new OptionsQuestionView(this);
                    timerView = new TimerView(30);
                    break;

                case "Quiz Writing":
                    MainContentHolder.Content = new WritingQuestionView(this);
                    timerView = new TimerView(100);
                    break;

                case "Quiz Listening":
                    MainContentHolder.Content = new ListeningQuestionView(this);
                    timerView = new TimerView(30);
                    break;

                case "Final Exam":
                    MainContentHolder.Content = new OptionsQuestionView(this);
                    liveTimerView = new LiveTimerView();
                    break;
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // إضافة TimerView إذا كان موجودًا
            if (timerView != null)
            {
                timerView.TimerFinished += TimerView_TimerFinished;
                grid.Children.Add(timerView);
            }

            // إضافة LiveTimerView إذا كان موجودًا
            if (liveTimerView != null)
            {
                var layout = new VerticalStackLayout
                {
                    Margin = new Thickness(40)
                };
                layout.Children.Add(liveTimerView);
                grid.Children.Add(layout);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // إزالة TimerView
            if (timerView != null)
            {
                timerView.TimerFinished -= TimerView_TimerFinished;
                timerView.StopTimer();
                grid.Children.Remove(timerView);
                timerView = null;
            }

            // إزالة LiveTimerView
            if (liveTimerView != null)
            {
                grid.Children.Remove(liveTimerView);
                liveTimerView = null;
            }

            // تنظيف محتوى MainContentHolder
            if (MainContentHolder.Content is IDisposable disposable)
                disposable.Dispose();

            MainContentHolder.Content = null;
        }

        private void TimerView_TimerFinished(object? sender, EventArgs e)
        {
            timerView?.StopTimer();
            mediaElement.Source = MediaSource.FromResource(Sounds.GameOver());
            Content = new LoseView();
        }
    }
}
