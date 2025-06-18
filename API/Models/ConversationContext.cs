namespace API.Models
{
    public class ConversationContext
    {
        public int Id { get; set; }
     
        public string UserMessage { get; set; }
        public string BotResponse { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
