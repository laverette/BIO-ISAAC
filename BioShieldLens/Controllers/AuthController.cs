using Microsoft.AspNetCore.Mvc;
using BioShieldLens.Models;
using BioShieldLens.Data;
using Microsoft.EntityFrameworkCore;

namespace BioShieldLens.Controllers;

public class AuthController : Controller
{
    private readonly BioShieldDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        BioShieldDbContext context,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // If already logged in, redirect
        if (HttpContext.Session.GetString("UserEmail") != null)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Verify invitation code
        var invitationCode = _configuration["Auth:InvitationCode"];
        if (string.IsNullOrEmpty(invitationCode) || model.InvitationCode != invitationCode)
        {
            ModelState.AddModelError("", "Invalid invitation code. Please contact your administrator.");
            return View(model);
        }

        // Check email whitelist
        var allowedEmails = _configuration.GetSection("Auth:AllowedEmails").Get<List<string>>() ?? new List<string>();
        var emailDomain = model.Email.Split('@').LastOrDefault();
        var isEmailAllowed = allowedEmails.Contains(model.Email, StringComparer.OrdinalIgnoreCase) ||
                             allowedEmails.Contains($"@{emailDomain}", StringComparer.OrdinalIgnoreCase);

        if (!isEmailAllowed && allowedEmails.Any())
        {
            ModelState.AddModelError("", "Your email is not authorized. Please contact your administrator.");
            _logger.LogWarning($"Unauthorized login attempt from: {model.Email}");
            return View(model);
        }

        // Check or create user
        var user = await _context.AuthUsers.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null)
        {
            // Create new user
            user = new AuthUser
            {
                Email = model.Email,
                Name = model.Name,
                Role = "Viewer", // Default role
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.AuthUsers.Add(user);
        }
        else if (!user.IsActive)
        {
            ModelState.AddModelError("", "Your account has been deactivated. Please contact your administrator.");
            return View(model);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Set session
        HttpContext.Session.SetString("UserEmail", user.Email);
        HttpContext.Session.SetString("UserName", user.Name);
        HttpContext.Session.SetString("UserRole", user.Role);
        HttpContext.Session.SetInt32("UserId", user.Id);

        _logger.LogInformation($"User logged in: {user.Email}");

        // Redirect to return URL or home
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}


