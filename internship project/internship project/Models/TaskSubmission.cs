namespace InternHub.Models
{
    public class TaskSubmission
    {
        public int Id { get; set; }

        public int TaskId { get; set; }
        public InternTask Task { get; set; } = null!;

        public string? Note { get; set; }
        public string? AttachmentPath { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}