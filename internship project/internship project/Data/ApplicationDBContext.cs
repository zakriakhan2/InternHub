using InternHub.Models;
using Microsoft.EntityFrameworkCore;

namespace InternHub.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<InternTask> Tasks => Set<InternTask>();
        public DbSet<TaskSubmission> TaskSubmissions => Set<TaskSubmission>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
        public DbSet<Message> Messages => Set<Message>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Self-referencing FK (Supervisor -> AssignedStudents) and the two
            // FKs from InternTask to User all need Restrict, or SQL Server refuses
            // to create the schema ("may cause cycles or multiple cascade paths").
            modelBuilder.Entity<User>()
                .HasOne(u => u.Supervisor)
                .WithMany(u => u.AssignedStudents)
                .HasForeignKey(u => u.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InternTask>()
                .HasOne(t => t.Student)
                .WithMany(u => u.TasksAsStudent)
                .HasForeignKey(t => t.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InternTask>()
                .HasOne(t => t.Supervisor)
                .WithMany(u => u.TasksAsSupervisor)
                .HasForeignKey(t => t.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ConversationParticipant>()
                .HasIndex(cp => new { cp.ConversationId, cp.UserId })
                .IsUnique();
        }
    }
}