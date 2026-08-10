using InternHub.Data;
using InternHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InternHub.Controllers
{
    [Authorize(Roles = "Supervisor")]
    public class SupervisorController : Controller
    {
        private readonly ApplicationDbContext _db;

        public SupervisorController(ApplicationDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.Students = await _db.Users
                .Where(u => u.SupervisorId == CurrentUserId)
                .ToListAsync();

            ViewBag.TaskCount = await _db.Tasks.CountAsync(t => t.SupervisorId == CurrentUserId);
            ViewBag.ReviewCount = await _db.Tasks.CountAsync(t => t.SupervisorId == CurrentUserId && t.Status == InternTaskStatus.InReview);

            return View();
        }

        public async Task<IActionResult> Tasks()
        {
            var tasks = await _db.Tasks
                .Where(t => t.SupervisorId == CurrentUserId)
                .Include(t => t.Student)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.Students = await _db.Users
                .Where(u => u.SupervisorId == CurrentUserId)
                .ToListAsync();

            return View(tasks);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTask(int studentId, string title, string description, DateTime? dueDate)
        {
            _db.Tasks.Add(new InternTask
            {
                Title = title,
                Description = description,
                DueDate = dueDate,
                StudentId = studentId,
                SupervisorId = CurrentUserId,
                Status = InternTaskStatus.Assigned
            });

            await _db.SaveChangesAsync();
            return RedirectToAction("Tasks");
        }
    }
}