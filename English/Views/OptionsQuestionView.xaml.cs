using CommunityToolkit.Maui.Views;
using English.Pages;
using English.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Linq;

namespace English.Views;

public partial class OptionsQuestionView : ContentView
{
    private readonly OptionsQuestionsViewVM vm;

    public OptionsQuestionView(ExamPage examPage)
    {
        InitializeComponent();

        // ربط ViewModel بالصفحة
        vm = new OptionsQuestionsViewVM(examPage, mediaElement);
        BindingContext = vm;

        // إخفاء الوسائط بشكل افتراضي إذا لم يكن هناك مصدر
        mediaElement.IsVisible = vm.MediaSource != null;

        // تشغيل الوسائط تلقائيًا إذا كان موجودًا
        if (mediaElement.IsVisible)
            mediaElement.Play();
    }

    private Button? lastSelectedButton = null;

    private void Button_Clicked(object sender, EventArgs e)
    {
        // 1. إعادة الزر السابق لحالته الطبيعية (ابيض مع حدود رمادية باهتة)
        if (lastSelectedButton != null)
        {
            lastSelectedButton.BackgroundColor = Colors.White;
            lastSelectedButton.BorderColor = Color.FromArgb("#E2E8F0");
            lastSelectedButton.TextColor = Color.FromArgb("#475569"); // اللون الافتراضي للنص
        }

        // 2. تمييز الزر الجديد الذي تم الضغط عليه
        if (sender is Button clickedBtn)
        {
            clickedBtn.BackgroundColor = Color.FromArgb("#F0F9FF");
            clickedBtn.BorderColor = Color.FromArgb("#0EA5E9");
            clickedBtn.TextColor = Color.FromArgb("#0284C7");
            clickedBtn.ScaleTo(0.95, 50, Easing.Linear).ContinueWith(t => clickedBtn.ScaleTo(1, 50));
            lastSelectedButton = clickedBtn;
        }
    }



    // عند الضغط على زر الإرسال
    private void Button_Send(object sender, EventArgs e)
    {
        if (lastSelectedButton != null)
            lastSelectedButton.BackgroundColor = Colors.White;
        vm.NextCommand?.Execute(null);
    }

    // عند مغادرة الصفحة
    private void ContentView_Unloaded(object sender, EventArgs e)
    {
        // إيقاف تشغيل الوسائط
        mediaElement?.Stop();
    }
}
