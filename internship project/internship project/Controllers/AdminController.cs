using InternHub.Data;
using InternHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

        // GET: /Admin/AllUsers?role=Student&search=sara&sortBy=name_asc&assignment=unassigned
        public async Task<IActionResult> AllUsers(string? role, string? search, string sortBy = "created_desc", string? assignment = null)
        {
            var query = _db.Users
                .Include(u => u.Supervisor)
                .Include(u => u.AssignedStudents)
                .AsQueryable();

            if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, out var roleEnum))
            {
                query = query.Where(u => u.Role == roleEnum);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u =>
                    u.FirstName.ToLower().Contains(s) ||
                    u.LastName.ToLower().Contains(s) ||
                    u.Email.ToLower().Contains(s));
            }

            if (assignment == "assigned")
            {
                query = query.Where(u => u.Role == UserRole.Student && u.SupervisorId != null);
            }
            else if (assignment == "unassigned")
            {
                query = query.Where(u => u.Role == UserRole.Student && u.SupervisorId == null);
            }

            query = sortBy switch
            {
                "name_asc" => query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName),
                "name_desc" => query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName),
                "created_asc" => query.OrderBy(u => u.CreatedAt),
                "role" => query.OrderBy(u => u.Role).ThenBy(u => u.FirstName),
                _ => query.OrderByDescending(u => u.CreatedAt) // created_desc, default
            };

            var unassignedStudents = await _db.Users
                .Where(u => u.Role == UserRole.Student && u.SupervisorId == null)
                .ToListAsync();
            var allSupervisors = await _db.Users
                .Where(u => u.Role == UserRole.Supervisor)
                .ToListAsync();

            ViewBag.UnassignedStudentsList = unassignedStudents;
            ViewBag.AllSupervisorsList = allSupervisors;
            ViewBag.Role = role;
            ViewBag.Search = search;
            ViewBag.SortBy = sortBy;
            ViewBag.Assignment = assignment;
            ViewBag.TotalUsers = await _db.Users.CountAsync();
            ViewBag.AdminCount = await _db.Users.CountAsync(u => u.Role == UserRole.Admin);
            ViewBag.SupervisorCount = await _db.Users.CountAsync(u => u.Role == UserRole.Supervisor);
            ViewBag.StudentCount = await _db.Users.CountAsync(u => u.Role == UserRole.Student);

            return View(await query.ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, string firstName, string lastName, string email, UserRole role)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            bool emailTaken = await _db.Users.AnyAsync(u => u.Id != id && u.Email == email);
            if (emailTaken)
            {
                ModelState.AddModelError(string.Empty, "That email is already used by another account.");
                user.FirstName = firstName;
                user.LastName = lastName;
                user.Email = email;
                user.Role = role;
                return View(user);
            }

            // Demoting a Supervisor unassigns their students, rather than leaving
            // them pointed at someone who's no longer a supervisor.
            if (user.Role == UserRole.Supervisor && role != UserRole.Supervisor)
            {
                var assignedStudents = await _db.Users.Where(u => u.SupervisorId == user.Id).ToListAsync();
                foreach (var s in assignedStudents) s.SupervisorId = null;
            }

            // Only Students carry a SupervisorId.
            if (role != UserRole.Student)
            {
                user.SupervisorId = null;
            }

            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;
            user.Role = role;

            await _db.SaveChangesAsync();
            TempData["Success"] = $"{user.FullName}'s account was updated.";
            return RedirectToAction("AllUsers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (id == CurrentUserId)
            {
                TempData["Error"] = "You can't delete your own account while logged in.";
                return RedirectToAction("AllUsers");
            }

            var user = await _db.Users.FindAsync(id);
            if (user == null) return RedirectToAction("AllUsers");

            bool hasTasks = await _db.Tasks.AnyAsync(t => t.StudentId == id || t.SupervisorId == id);
            bool hasStudents = await _db.Users.AnyAsync(u => u.SupervisorId == id);

            if (hasTasks || hasStudents)
            {
                TempData["Error"] = $"Can't delete {user.FullName} — they have tasks or assigned students on record. Reassign or reassign those first.";
                return RedirectToAction("AllUsers");
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{user.FullName}'s account was deleted.";
            return RedirectToAction("AllUsers");
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