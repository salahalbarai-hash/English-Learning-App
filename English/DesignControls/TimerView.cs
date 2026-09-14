using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System;
using System.Timers;

namespace English.DesignControls
{
    public class TimerView : ContentView
    {
        private System.Timers.Timer _timer;
        private int _remainingTime;
        private int _totalTime;
        private SKCanvasView _canvasView;
        private Label _timerLabel;

        public event EventHandler TimerFinished;

        public TimerView(int time)
        {
            _remainingTime = time;
            _totalTime = time;

            _canvasView = new SKCanvasView
            {
                HeightRequest = 80,
                WidthRequest = 80
            };
            _canvasView.PaintSurface += OnCanvasViewPaintSurface;

            _timerLabel = new Label
            {
                Text = _remainingTime.ToString(),
                FontSize = 28,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                TranslationY = -2,
                FontFamily = "SevenSegment"
            };

            Content = new Grid
            {
                Children = { _canvasView, _timerLabel }
            };

            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += TimerElapsed;
            _timer.Start();
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_remainingTime > 0)
                {
                    _remainingTime--;
                    _timerLabel.Text = _remainingTime.ToString();

                    // تغيير اللون للبرتقالي النيون في آخر 15 ثانية
                    if (_remainingTime <= 15)
                    {
                        _timerLabel.TextColor = Color.FromArgb("#FF8C00");
                    }

                    _canvasView.InvalidateSurface();

                    if (_remainingTime <= 0)
                    {
                        StopTimer();
                        TimerFinished?.Invoke(this, EventArgs.Empty);
                    }
                }
            });
        }

        public void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        public int GetRemainingTime()
        {
            return _remainingTime;
        }

        private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            var canvas = args.Surface.Canvas;
            canvas.Clear();

            float width = args.Info.Width;
            float height = args.Info.Height;
            float center = width / 2f;
            float strokeWidth = 8f;
            float radius = (Math.Min(width, height) / 2f) - strokeWidth - 5f;
            float progress = (float)_remainingTime / _totalTime;

            // 1. رسم الدائرة الخلفية (الخاملة)
            using (var backgroundPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColor.Parse("#222222"),
                StrokeWidth = strokeWidth,
                IsAntialias = true
            })
            {
                canvas.DrawCircle(center, center, radius, backgroundPaint);
            }

            // 2. رسم مسار الوقت الملون (النشط)
            string strokeColor = _remainingTime <= 15 ? "#FF8C00" : "#2DD4BF";

            using (var progressPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColor.Parse(strokeColor),
                StrokeWidth = strokeWidth,
                StrokeCap = SKStrokeCap.Round,
                IsAntialias = true,
                // إضافة توهج بسيط
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f)
            })
            {
                using (var path = new SKPath())
                {
                    // رسم القوس بناءً على الوقت المتبقي
                    path.AddArc(
                        new SKRect(center - radius, center - radius, center + radius, center + radius),
                        -90,
                        360 * progress);

                    canvas.DrawPath(path, progressPaint);
                }
            }
        }
    }
}