using System.ComponentModel.DataAnnotations;

namespace InternHub.Models
{
    public enum UserRole
    {
        Admin,
        Supervisor,
        Student
    }

    public class User
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; }

        // Only meaningful when Role == Student — which Supervisor they're paired with.
        public int? SupervisorId { get; set; }
        public User? Supervisor { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string FullName => $"{FirstName} {LastName}";

        // Populated only when Role == Supervisor.
        public ICollection<User> AssignedStudents { get; set; } = new List<User>();

        public ICollection<InternTask> TasksAsStudent { get; set; } = new List<InternTask>();
        public ICollection<InternTask> TasksAsSupervisor { get; set; } = new List<InternTask>();

        // ---- Settings page fields ----
        public string? ProfilePicturePath { get; set; }

        [MaxLength(500)]
        public string? Bio { get; set; }

        public bool EmailNotifyOnTaskAssigned { get; set; } = true;
        public bool EmailNotifyOnTaskReviewed { get; set; } = true;
    }
}