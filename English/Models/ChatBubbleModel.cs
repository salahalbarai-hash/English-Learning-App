namespace English.Models;

public enum MessageStatus
{
    Pending,   // 🕒 قيد الانتظار (بدون نت)
    Sent,      // ✓ أرسلت للسيرفر
    Delivered, // ✓✓ استلمها الطرف الآخر
    Read       // ✓✓ قرأها الطرف الآخر (أزرق)
}

public class ChatBubbleModel
{
    // 🟢 تعديل الـ Id ليصبح int ليتطابق مع قاعدة البيانات و ChatMessageDto
    public int Id { get; set; }

    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsMine { get; set; }

    // حالة الرسالة
    public MessageStatus Status { get; set; } = MessageStatus.Pending;

    // خصائص التصميم المتناسقة مع الخلفية الفاتحة
    public LayoutOptions BubbleAlignment => IsMine ? LayoutOptions.End : LayoutOptions.Start;

    // أخضر فاتح للمرسل، أبيض للمستقبل
    public Color BubbleColor => IsMine ? Color.FromArgb("#E7FFDB") : Color.FromArgb("#FFFFFF");

    // لون نص أسود/رمادي داكن لضمان الوضوح التام
    public Color TextColor => Color.FromArgb("#111B21");

    // لون هادئ للوقت
    public Color TimeColor => Color.FromArgb("#667781");

    // 🟢 تعديل الوقت ليصبح بنظام 12 ساعة مع (ص / م) باللغة العربية
    public string TimeString => Timestamp.ToString("hh:mm tt", new System.Globalization.CultureInfo("ar-SA"));

    // أيقونات الحالة (الساعة والصح والصحين)
    public string StatusIconText => Status switch
    {
        MessageStatus.Pending => "🕒",
        MessageStatus.Sent => "✓",
        MessageStatus.Delivered => "✓✓",
        MessageStatus.Read => "✓✓",
        _ => ""
    };

    // لون علامة الصح (أزرق فاتح عند القراءة، رمادي للحالات الأخرى)
    public Color StatusIconColor => Status == MessageStatus.Read ? Color.FromArgb("#53BDEB") : Color.FromArgb("#8696A0");
}