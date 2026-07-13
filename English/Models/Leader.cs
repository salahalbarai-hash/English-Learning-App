using Microsoft.Maui.Graphics;

namespace English.Models
{
    public class Leader
    {
        public required string UserName { get; set; }
        public string? TimeFinalExam { get; set; }
        public int MemorizedWords { get; set; }
        public int Rank { get; set; }
        public bool IsFirst => Rank == 1;
        public bool IsSecond => Rank == 2;
        public bool IsThird => Rank == 3;
        public bool IsTopThree => Rank >= 1 && Rank <= 3;
        public bool IsNotTopThree => Rank > 3 & Rank < 11;

        public Color RankColor => Rank switch
        {
            1 => Color.FromArgb("#FFD700"), // ذهبي ملكي
            2 => Color.FromArgb("#C0C0C0"), // فضي لامع
            3 => Color.FromArgb("#CD7F32"), // برونزي
            _ => Color.FromArgb("#68A5E9")  // أزرق سماوي (هوية تطبيق انجليش) للبقية
        };
    }
}