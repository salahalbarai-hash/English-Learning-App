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
    public class LiveTimerView : ContentView
    {
        private readonly System.Timers.Timer _timer;
        private double _elapsedMilliseconds;
        private readonly SKCanvasView _canvasView;
        private readonly Label _timeLabel;

        // تغيير اللون الافتراضي ليكون متوافقاً مع الثيم الداكن (LightGray/White)
        public static readonly BindableProperty CircleColorProperty =
            BindableProperty.Create(
                nameof(CircleColor),
                typeof(Color),
                typeof(LiveTimerView),
                Colors.Cyan, // لون نيون افتراضي
                BindingMode.TwoWay);

        public static readonly BindableProperty TextColorProperty =
            BindableProperty.Create(
                nameof(TextColor),
                typeof(Color),
                typeof(LiveTimerView),
                Colors.White, // النص أبيض ليظهر على الأسود
                BindingMode.TwoWay);

        public Color CircleColor
        {
            get => (Color)GetValue(CircleColorProperty);
            set => SetValue(CircleColorProperty, value);
        }

        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public LiveTimerView()
        {
            _canvasView = new SKCanvasView
            {
                HeightRequest = 140,
                WidthRequest = 140
            };
            _canvasView.PaintSurface += OnCanvasViewPaintSurface;

            _timeLabel = new Label
            {
                Text = FormatTime(0),
                FontSize = 24,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = TextColor // مرتبط بالخاصية التي جعلناها بيضاء
            };

            // محاولة استخدام خط رقمي إذا كان متوفراً في مشروعك
            try { _timeLabel.FontFamily = "SevenSegment"; } catch { }

            var grid = new Grid
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            grid.Children.Add(_canvasView);
            grid.Children.Add(_timeLabel);

            Content = grid;

            _timer = new System.Timers.Timer(10);
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _elapsedMilliseconds += 10;
                _timeLabel.Text = FormatTime(_elapsedMilliseconds);

                // إضافة تأثير وميض بسيط عند مرور كل ثانية (اختياري)
                if (_elapsedMilliseconds % 1000 == 0)
                {
                    _canvasView.InvalidateSurface();
                }
            });
        }

        private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear();

            float width = e.Info.Width;
            float height = e.Info.Height;

            float cx = width / 2f;
            float cy = height / 2f;
            float radius = Math.Min(cx, cy) - 10f;

            // رسم الدائرة مع تأثير Glow بسيط
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = CircleColor.ToSKColor(),
                StrokeWidth = 6f,
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f) // تأثير توهج نيون
            };

            canvas.DrawCircle(cx, cy, radius, paint);

            // رسم دائرة داخلية أنحف لتعزيز شكل النيون
            paint.MaskFilter = null;
            paint.StrokeWidth = 2f;
            paint.Color = SKColors.White;
            canvas.DrawCircle(cx, cy, radius, paint);
        }

        private string FormatTime(double elapsedMilliseconds)
        {
            int totalSeconds = (int)(elapsedMilliseconds / 1000);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            int centiseconds = (int)(elapsedMilliseconds % 1000 / 10);

            return $"{minutes:D2}:{seconds:D2}:{centiseconds:D2}";
        }

        public void StopTimer()
        {
            if (_timer != null && _timer.Enabled)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            _canvasView.InvalidateSurface();
        }

        public string GetTime => _timeLabel.Text;
    }
}