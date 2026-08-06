using InternHub.Data;
using InternHub.Helpers;
using InternHub.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }
    }
}