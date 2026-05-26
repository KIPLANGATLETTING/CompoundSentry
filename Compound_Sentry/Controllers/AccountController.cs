using Compound_Sentry.Data;
using Compound_Sentry.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace Compound_Sentry.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<AdminUser> _signInManager;
        private readonly UserManager<AdminUser> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<AdminUser> signInManager,
            UserManager<AdminUser> userManager,
            ApplicationDbContext dbContext,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Username and password are required";
                return View();
            }

            Microsoft.AspNetCore.Identity.SignInResult result;
            try
            {
                result = await _signInManager.PasswordSignInAsync(username, password, false, false);
            }
            catch (Exception ex)
            {
                // Log full exception for diagnostics and show a friendly message to the user
                _logger?.LogError(ex, "Error while attempting to sign in user {Username}", username);
                ViewBag.Error = "Unable to connect to authentication database. Please try again later.";
                return View();
            }
            if (result.Succeeded)
            {
                // Update last login time
                var user = await _userManager.FindByNameAsync(username);
                if (user != null)
                {
                    user.LastLoginAt = DateTime.Now;
                    await _userManager.UpdateAsync(user);
                }

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.Error = "Invalid username or password";
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string fullName, string username, string email, string password, string confirmPassword, string? phoneNumber = null)
        {
            // Validation
            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match";
                return View();
            }

            if (password.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters";
                return View();
            }

            // Check if username exists
            var existingUser = await _userManager.FindByNameAsync(username);
            if (existingUser != null)
            {
                ViewBag.Error = "Username already exists";
                return View();
            }

            // Check if email exists
            var existingEmail = await _userManager.FindByEmailAsync(email);
            if (existingEmail != null)
            {
                ViewBag.Error = "Email already registered";
                return View();
            }

            var user = new AdminUser
            {
                UserName = username,
                Email = email,
                FullName = fullName,
                PhoneNumber = phoneNumber,
                CreatedAt = DateTime.Now,
                IsActive = true,
                LoginAttempts = 0
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // Auto login after registration
                await _signInManager.SignInAsync(user, false);
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}