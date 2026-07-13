namespace English.Pages;

public partial class VideoPage : ContentPage
{
    public VideoPage()
    {
        InitializeComponent();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // إيقاف الفيديو تماماً عند مغادرة الصفحة لتوفير موارد الجهاز
        if (mediaPlayer != null)
        {
            mediaPlayer.Stop();
        }
    }
}