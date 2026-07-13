namespace English.Models
{
    public class User
    {
        public long ID { get; set; }

        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string? PhoneNumber { get; set; }

        public string? YER { get; set; }

        public string? TimeFinalExam { get; set; }
        public int MemorizedWords { get; set; }
    }

}