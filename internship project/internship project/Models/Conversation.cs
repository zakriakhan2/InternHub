namespace InternHub.Models
{
    public class Conversation
    {
        public int Id { get; set; }
        public bool IsGroup { get; set; }
        public string? Name { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }

    public class ConversationParticipant
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public bool IsHidden { get; set; }
    }

    public class Message
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;

        public int SenderId { get; set; }
        public User Sender { get; set; } = null!;

        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool IsEdited { get; set; }
        public bool IsDeleted { get; set; }

        public ICollection<MessageDeletion> DeletionsFor { get; set; } = new List<MessageDeletion>();
    }

    public class MessageDeletion
    {
        public int Id { get; set; }

        public int MessageId { get; set; }
        public Message Message { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;
    }
}