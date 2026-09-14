namespace Shared.Models
{
    public class GeminiKeyModel
    {
        public int Id { get; set; }

        public string ApiKey { get; set; } = "";

        public int RequestCount { get; set; }

        public DateTime WindowStart { get; set; }

        public bool IsActive { get; set; }
    }
}
