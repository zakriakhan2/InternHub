using InternHub.Data;
using InternHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InternHub.Controllers
{
    [Authorize]
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _db;

        public TasksController(ApplicationDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task<IActionResult> Details(int id)
        {
            var task = await _db.Tasks
                .Include(t => t.Student)
                .Include(t => t.Supervisor)
                .Include(t => t.Submissions)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return NotFound();

            bool allowed = User.IsInRole("Admin")
                         || (User.IsInRole("Student") && task.StudentId == CurrentUserId)
                         || (User.IsInRole("Supervisor") && task.SupervisorId == CurrentUserId);

            if (!allowed) return Forbid();

            return View(task);
        }

        [HttpPost]
        [Authorize(Roles = "Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int taskId)
        {
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.SupervisorId == CurrentUserId);
            if (task != null)
            {
                task.Status = InternTaskStatus.Approved;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Details", new { id = taskId });
        }
    }
}