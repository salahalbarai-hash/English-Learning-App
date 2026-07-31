namespace English.DataTransferObject
{
    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string Sender { get; set; } = "";
        public string Receiver { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsDelivered { get; set; }
        public bool IsRead { get; set; }
    }
}
