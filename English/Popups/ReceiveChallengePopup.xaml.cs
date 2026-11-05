using CommunityToolkit.Maui.Views;
using System.Text.Json;

namespace English.Popups;

public partial class ReceiveChallengePopup : Popup
{
    private string _selectedBirdColor = "#F43F5E";
    private string _senderBirdColor = "#38BDF8";

    public ReceiveChallengePopup(string senderName, string category, string? word = null, string? arabic = null)
    {
        InitializeComponent();

        bool isSkyward = category.Contains("الطائر") || category.Contains("Skyward");

        if (isSkyward)
        {
            BirdColorContainer.IsVisible = true;
            WordPreviewContainer.IsVisible = false;
            MessageLabel.Text = $"تلقيت طلب تحدٍ من الصديق ({senderName}) في سباق الطائر الصاعد 🚀!";

            // استخراج لون طائر الصديق والألوان المتاحة للمستقبل من الحمولة
            _senderBirdColor = ExtractSenderColor(word);
            var availableColors = ExtractAvailableColors(word, _senderBirdColor);

            SenderBirdColorHintLabel.Text = $"طائر {senderName}: 🦅 {GetColorTitle(_senderBirdColor)}";
            SenderBirdColorHintLabel.TextColor = Color.FromArgb(_senderBirdColor);

            // إخفاء اللون الذي اختاره الصديق وعرض جميع الألوان الأخرى حصراً
            ConfigureAvailableColors(availableColors);

            // اختيار لون افتراضي للمستقبل من بين الألوان المتاحة
            string userPreferredColor = Preferences.Get("MyBirdColor", "");
            if (!string.IsNullOrEmpty(userPreferredColor) && availableColors.Contains(userPreferredColor, StringComparer.OrdinalIgnoreCase))
            {
                _selectedBirdColor = userPreferredColor;
            }
            else
            {
                _selectedBirdColor = availableColors.FirstOrDefault() ?? "#F43F5E";
            }

            UpdateColorSelectionUI(_selectedBirdColor);
        }
        else if (!string.IsNullOrEmpty(word) && !word.TrimStart().StartsWith("{") && !word.TrimStart().StartsWith("["))
        {
            MessageLabel.Text = $"{senderName} أرسل تحديًا: ({category})\nالكلمة: {word} - {arabic}";
            WordLabel.Text = word;
            ArabicLabel.Text = arabic ?? string.Empty;
            WordPreviewContainer.IsVisible = true;
            BirdColorContainer.IsVisible = false;
        }
        else
        {
            MessageLabel.Text = $"تلقيت طلب تحدٍ من الصديق ({senderName}) في قسم ({category})!";
            WordPreviewContainer.IsVisible = false;
            BirdColorContainer.IsVisible = false;
        }
    }

    private void ConfigureAvailableColors(List<string> availableColors)
    {
        ColorCyanBorder.IsVisible = availableColors.Contains("#38BDF8", StringComparer.OrdinalIgnoreCase);
        ColorRoseBorder.IsVisible = availableColors.Contains("#F43F5E", StringComparer.OrdinalIgnoreCase);
        ColorGoldBorder.IsVisible = availableColors.Contains("#F59E0B", StringComparer.OrdinalIgnoreCase);
        ColorEmeraldBorder.IsVisible = availableColors.Contains("#10B981", StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> ExtractAvailableColors(string? payload, string senderColor)
    {
        var allColors = new List<string> { "#38BDF8", "#F43F5E", "#F59E0B", "#10B981" };
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("AvailableColors", out var availProp) && availProp.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in availProp.EnumerateArray())
                    {
                        var col = item.GetString();
                        if (!string.IsNullOrEmpty(col))
                            list.Add(col);
                    }
                    if (list.Count > 0)
                        return list;
                }
            }
            catch { }
        }

        // في حال عدم توفر القائمة مباشرة، استبعد لون المرسل تلقائياً
        return allColors.Where(c => !c.Equals(senderColor, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnColorCyanTapped(object sender, TappedEventArgs e) => SelectColor("#38BDF8");
    private void OnColorRoseTapped(object sender, TappedEventArgs e) => SelectColor("#F43F5E");
    private void OnColorGoldTapped(object sender, TappedEventArgs e) => SelectColor("#F59E0B");
    private void OnColorEmeraldTapped(object sender, TappedEventArgs e) => SelectColor("#10B981");

    private void SelectColor(string hex)
    {
        _selectedBirdColor = hex;
        Preferences.Set("MyBirdColor", hex);
        UpdateColorSelectionUI(hex);
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
    }

    private void UpdateColorSelectionUI(string selectedHex)
    {
        SetBorderSelection(ColorCyanBorder, ColorCyanCheck, selectedHex.Equals("#38BDF8", StringComparison.OrdinalIgnoreCase), "#38BDF8");
        SetBorderSelection(ColorRoseBorder, ColorRoseCheck, selectedHex.Equals("#F43F5E", StringComparison.OrdinalIgnoreCase), "#F43F5E");
        SetBorderSelection(ColorGoldBorder, ColorGoldCheck, selectedHex.Equals("#F59E0B", StringComparison.OrdinalIgnoreCase), "#F59E0B");
        SetBorderSelection(ColorEmeraldBorder, ColorEmeraldCheck, selectedHex.Equals("#10B981", StringComparison.OrdinalIgnoreCase), "#10B981");
    }

    private static void SetBorderSelection(Border border, Label check, bool isSelected, string baseHex)
    {
        if (isSelected)
        {
            border.Stroke = Colors.White;
            border.StrokeThickness = 3;
            border.Scale = 1.12;
            check.IsVisible = true;
        }
        else
        {
            border.Stroke = Color.FromArgb(baseHex);
            border.StrokeThickness = 1;
            border.Scale = 1.0;
            check.IsVisible = false;
        }
    }

    private static string ExtractSenderColor(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return "#38BDF8";
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("SenderColor", out var colProp))
            {
                return colProp.GetString() ?? "#38BDF8";
            }
        }
        catch { }
        return "#38BDF8";
    }

    private static string GetColorTitle(string hex) => hex.ToUpperInvariant() switch
    {
        "#38BDF8" => "أزرق نيون",
        "#F43F5E" => "وردي ناري",
        "#F59E0B" => "ذهبي متوهج",
        "#10B981" => "أخضر زمردي",
        _ => "نيون مميز"
    };

    private void OnDeclineClicked(object sender, EventArgs e)
    {
        Close(false); // إرجاع false يعني رفض التحدي
    }

    private void OnAcceptClicked(object sender, EventArgs e)
    {
        if (BirdColorContainer.IsVisible)
        {
            Preferences.Set("MyBirdColor", _selectedBirdColor);
        }
        Close(true); // إرجاع true يعني قبول التحدي
    }
}
