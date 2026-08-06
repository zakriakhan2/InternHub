using InternHub.Data;
using InternHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InternHub.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _db;

        public StudentController(ApplicationDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task<IActionResult> Dashboard()
        {
            int studentId = CurrentUserId;

            ViewBag.Student = await _db.Users
                .Include(u => u.Supervisor)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            var tasks = await _db.Tasks
                .Where(t => t.StudentId == studentId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tasks);
        }

        public async Task<IActionResult> Tasks()
        {
            var tasks = await _db.Tasks
                .Where(t => t.StudentId == CurrentUserId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tasks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShowcaseTask(int taskId, string note, IFormFile? attachment)
        {
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.StudentId == CurrentUserId);
            if (task == null) return NotFound();

            string? path = null;
            if (attachment != null && attachment.Length > 0)
            {
                var uploadsDir = Path.Combine("wwwroot", "uploads");
                Directory.CreateDirectory(uploadsDir);
                var fileName = $"{Guid.NewGuid()}_{attachment.FileName}";
                await using var stream = new FileStream(Path.Combine(uploadsDir, fileName), FileMode.Create);
                await attachment.CopyToAsync(stream);
                path = $"/uploads/{fileName}";
            }

            _db.TaskSubmissions.Add(new TaskSubmission
            {
                TaskId = task.Id,
                Note = note,
                AttachmentPath = path
            });

            task.Status = InternTaskStatus.InReview;
            await _db.SaveChangesAsync();

            return RedirectToAction("Tasks");
        }
    }
}