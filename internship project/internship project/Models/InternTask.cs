using System.ComponentModel.DataAnnotations;

namespace InternHub.Models
{
    // Named InternTaskStatus (not TaskStatus) to avoid colliding with
    // System.Threading.Tasks.TaskStatus, which every controller already uses.
    public enum InternTaskStatus
    {
        Assigned,
        InProgress,
        InReview,
        ChangesRequested,
        Approved
    }

    public class InternTask
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public DateTime? DueDate { get; set; }

        public InternTaskStatus Status { get; set; } = InternTaskStatus.Assigned;

        public int StudentId { get; set; }
        public User Student { get; set; } = null!;

        public int SupervisorId { get; set; }
        public User Supervisor { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<TaskSubmission> Submissions { get; set; } = new List<TaskSubmission>();
    }
}