using InternHub.Data;
using InternHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternHub.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AdminController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalUsers = await _db.Users.CountAsync();
            ViewBag.TotalStudents = await _db.Users.CountAsync(u => u.Role == UserRole.Student);
            ViewBag.TotalSupervisors = await _db.Users.CountAsync(u => u.Role == UserRole.Supervisor);
            ViewBag.UnassignedCount = await _db.Users.CountAsync(u => u.Role == UserRole.Student && u.SupervisorId == null);
            ViewBag.ActivePairings = await _db.Users.CountAsync(u => u.Role == UserRole.Student && u.SupervisorId != null);

            ViewBag.UnassignedStudentsList = await _db.Users
                .Where(u => u.Role == UserRole.Student && u.SupervisorId == null)
                .ToListAsync();
            ViewBag.AllSupervisors = await _db.Users
                .Where(u => u.Role == UserRole.Supervisor)
                .ToListAsync();

            var recentUsers = await _db.Users
                .Include(u => u.Supervisor)
                .OrderByDescending(u => u.CreatedAt)
                .Take(10)
                .ToListAsync();

            return View(recentUsers);
        }

        public async Task<IActionResult> AllUsers()
        {
            var users = await _db.Users
                .Include(u => u.Supervisor)
                .OrderBy(u => u.Role).ThenBy(u => u.FirstName)
                .ToListAsync();

            return View(users);
        }

        public async Task<IActionResult> Assignments()
        {
            var supervisors = await _db.Users
                .Where(u => u.Role == UserRole.Supervisor)
                .Include(u => u.AssignedStudents)
                .ToListAsync();

            ViewBag.Tasks = await _db.Tasks
                .Include(t => t.Student)
                .Include(t => t.Supervisor)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.UnassignedCount = await _db.Users.CountAsync(u => u.Role == UserRole.Student && u.SupervisorId == null);

            return View(supervisors);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignSupervisor(int studentId, int supervisorId)
        {
            var student = await _db.Users.FirstOrDefaultAsync(u => u.Id == studentId && u.Role == UserRole.Student);
            var supervisor = await _db.Users.FirstOrDefaultAsync(u => u.Id == supervisorId && u.Role == UserRole.Supervisor);

            if (student != null && supervisor != null)
            {
                student.SupervisorId = supervisor.Id;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PromoteToSupervisor(int userId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Student);

            if (user != null)
            {
                user.Role = UserRole.Supervisor;
                user.SupervisorId = null;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Dashboard");
        }
    }
}