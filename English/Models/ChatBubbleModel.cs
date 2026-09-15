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

    // لون أزرق نيلي للمرسل (أو بنفسجي داكن)، كحلي غامق للمستقبل
    public Color BubbleColor => IsMine
    ? Color.FromArgb("#3730A3")
    : Color.FromArgb("#1E293B");

    // لون نص أبيض ناصع أو مائل للرمادي لضمان الوضوح التام
    public Color TextColor => IsMine
    ? Color.FromArgb("#FFFFFF")
    : Color.FromArgb("#F8FAFC");

    // لون هادئ للوقت
    public Color TimeColor => IsMine
    ? Color.FromArgb("#C7D2FE")
    : Color.FromArgb("#94A3B8");

    // 🟢 تعديل الوقت ليصبح بنظام 12 ساعة مع (ص / م) باللغة العربية
    public string TimeString => Timestamp.ToString("hh:mm tt", new System.Globalization.CultureInfo("ar-SA"));

    // أيقونات الحالة (الساعة والصح والصحين)
    public string StatusIconText => Status switch
    {
        MessageStatus.Pending => "🕒",
        MessageStatus.Sent => "✓✓",
        MessageStatus.Delivered => "✓✓",
        MessageStatus.Read => "✓✓",
        _ => ""
    };

    // لون علامة الصح (سماوي فاتح عند القراءة، رمادي للحالات الأخرى)
    public Color StatusIconColor => Status == MessageStatus.Read ? Color.FromArgb("#38BDF8") : Color.FromArgb("#94A3B8");
}