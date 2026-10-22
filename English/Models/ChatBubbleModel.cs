using Microsoft.Maui.Controls.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json.Serialization; // 🟢 1. أضفنا مكتبة الـ JSON

namespace English.Models;

public enum MessageStatus
{
    Pending,
    Sent,
    Delivered,
    Read
}

public partial class ChatBubbleModel : ObservableObject
{
    private static readonly PathGeometryConverter _converter = new();

    [ObservableProperty]
    [property: JsonIgnore] // تجاهل عند الحفظ
    private bool isSelected;

    [ObservableProperty]
    [property: JsonIgnore] // تجاهل عند الحفظ
    private bool isSelectionMode;

    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsMine { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusIconPath))]
    [NotifyPropertyChangedFor(nameof(StatusIconGeometry))]
    [NotifyPropertyChangedFor(nameof(StatusIconColor))]
    [NotifyPropertyChangedFor(nameof(StatusIconWidth))]
    private MessageStatus status = MessageStatus.Pending;



    // 🟢 3. تجاهل جميع الألوان والأشكال والخصائص التجميلية
    [JsonIgnore]
    public LayoutOptions BubbleAlignment => IsMine ? LayoutOptions.End : LayoutOptions.Start;

    [JsonIgnore]
    public Color BubbleColor => IsMine
        ? Color.FromArgb("#1E40AF")
        : Color.FromArgb("#1E293B");

    [JsonIgnore]
    public Color TextColor => IsMine
        ? Color.FromArgb("#FFFFFF")
        : Color.FromArgb("#F8FAFC");

    [JsonIgnore]
    public Color TimeColor => IsMine
        ? Color.FromArgb("#C7D2FE")
        : Color.FromArgb("#94A3B8");

    [JsonIgnore]
    public string TimeString => Timestamp.ToString("hh:mm tt", new System.Globalization.CultureInfo("ar-SA"));

    [JsonIgnore]
    public string StatusIconPath => Status switch
    {
        MessageStatus.Pending => "M12 2A10 10 0 1 0 12 22A10 10 0 1 0 12 2 M12 7v5l3 3",
        MessageStatus.Sent => "M2 13l4 4L15 7",
        MessageStatus.Delivered or MessageStatus.Read => "M2 13l4 4L15 7 M9 13l4 4L22 7",
        _ => ""
    };

    [JsonIgnore]
    public double StatusIconWidth => Status switch
    {
        MessageStatus.Pending => 14,
        MessageStatus.Sent => 11,
        MessageStatus.Delivered or MessageStatus.Read => 16,
        _ => 16
    };

    [JsonIgnore]
    public Geometry StatusIconGeometry
    {
        get
        {
            var path = StatusIconPath;
            if (string.IsNullOrEmpty(path))
                return new PathGeometry();

            try
            {
                return (Geometry)_converter.ConvertFromInvariantString(path);
            }
            catch
            {
                return new PathGeometry();
            }
        }
    }

    [JsonIgnore]
    public Color StatusIconColor => Status == MessageStatus.Read ? Color.FromArgb("#38BDF8") : Color.FromArgb("#94A3B8");
}