using InternHub.Data;
using InternHub.Helpers;
using InternHub.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InternHub.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AccountController(ApplicationDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public IActionResult Login() => View();

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || !PasswordHasher.Verify(password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Incorrect email or password.");
                return View();
            }

            await SignInUser(user);

            return user.Role switch
            {
                UserRole.Admin => RedirectToAction("Dashboard", "Admin"),
                UserRole.Supervisor => RedirectToAction("Dashboard", "Supervisor"),
                _ => RedirectToAction("Dashboard", "Student"),
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string firstName, string lastName, string email, string password)
        {
            if (await _db.Users.AnyAsync(u => u.Email == email))
            {
                ModelState.AddModelError(string.Empty, "An account with that email already exists.");
                return View();
            }

            bool isFirstUser = !await _db.Users.AnyAsync();

            var user = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PasswordHash = PasswordHasher.Hash(password),
                Role = isFirstUser ? UserRole.Admin : UserRole.Student
            };

            _db.Users.Add(user);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "An account with that email already exists.");
                return View();
            }

            await SignInUser(user);

            return user.Role == UserRole.Admin
                ? RedirectToAction("Dashboard", "Admin")
                : RedirectToAction("Dashboard", "Student");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(string firstName, string lastName, string email, string? bio,
            bool emailNotifyOnTaskAssigned, bool emailNotifyOnTaskReviewed, IFormFile? avatar)
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();

            bool emailTaken = await _db.Users.AnyAsync(u => u.Id != user.Id && u.Email == email);
            if (emailTaken)
            {
                ModelState.AddModelError(string.Empty, "That email is already used by another account.");
                return View(user);
            }

            if (avatar != null && avatar.Length > 0)
            {
                if (!avatar.ContentType.StartsWith("image/"))
                {
                    ModelState.AddModelError(string.Empty, "Profile picture must be an image file.");
                    return View(user);
                }
                if (avatar.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError(string.Empty, "Profile picture must be under 5MB.");
                    return View(user);
                }

                var avatarsDir = Path.Combine("wwwroot", "uploads", "avatars");
                Directory.CreateDirectory(avatarsDir);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(avatar.FileName)}";
                await using (var stream = new FileStream(Path.Combine(avatarsDir, fileName), FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }
                user.ProfilePicturePath = $"/uploads/avatars/{fileName}";
            }

            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;
            user.Bio = bio;
            user.EmailNotifyOnTaskAssigned = emailNotifyOnTaskAssigned;
            user.EmailNotifyOnTaskReviewed = emailNotifyOnTaskReviewed;

            await _db.SaveChangesAsync();

            // Re-issue the auth cookie so the sidebar (name/avatar) reflects changes immediately.
            await SignInUser(user);

            TempData["Success"] = "Your profile was updated.";
            return RedirectToAction("Settings");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();

            if (!PasswordHasher.Verify(currentPassword, user.PasswordHash))
            {
                TempData["PasswordError"] = "Current password is incorrect.";
                return RedirectToAction("Settings");
            }

            if (newPassword != confirmPassword)
            {
                TempData["PasswordError"] = "New password and confirmation don't match.";
                return RedirectToAction("Settings");
            }

            if (newPassword.Length < 8)
            {
                TempData["PasswordError"] = "New password must be at least 8 characters.";
                return RedirectToAction("Settings");
            }

            user.PasswordHash = PasswordHasher.Hash(newPassword);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Password changed.";
            return RedirectToAction("Settings");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetTheme(string theme, string? returnUrl)
        {
            theme = theme == "dark" ? "dark" : "light";
            Response.Cookies.Append("theme", theme, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true
            });

            return (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                ? LocalRedirect(returnUrl)
                : RedirectToAction("Login");
        }

        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.ToString()),
                new("Avatar", user.ProfilePicturePath ?? "")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }
    }
}   