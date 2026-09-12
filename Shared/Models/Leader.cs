namespace Shared.Models
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

        public bool IsNotTopThree => Rank > 3 && Rank < 11;

        public string RankColor => Rank switch
        {
            1 => "#FFD700", // ذهبي
            2 => "#C0C0C0", // فضي
            3 => "#CD7F32", // برونزي
            _ => "#68A5E9"  // أزرق
        };
    }
}