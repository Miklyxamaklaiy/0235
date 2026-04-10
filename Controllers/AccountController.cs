using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlechPomoshchi.Data;
using PlechPomoshchi.Models;
using PlechPomoshchi.ViewModels;

namespace PlechPomoshchi.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PasswordHasher<AppUser> _hasher = new();

    public AccountController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
            return View(vm);

        var email = vm.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email);

        if (user == null)
        {
            ModelState.AddModelError("", "Неверный email или пароль.");
            return View(vm);
        }

        var res = _hasher.VerifyHashedPassword(user, user.PasswordHash, vm.Password);
        if (res == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Неверный email или пароль.");
            return View(vm);
        }

        await SignInAsync(user);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var email = vm.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(x => x.Email.ToLower() == email);
        if (exists)
        {
            ModelState.AddModelError(nameof(vm.Email), "Пользователь с таким email уже существует.");
            return View(vm);
        }

        var fullName = string.Join(" ", new[] { vm.LastName?.Trim(), vm.FirstName?.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = vm.Email.Trim();

        var user = new AppUser
        {
            Email = vm.Email.Trim(),
            FullName = fullName,
            Phone = null,
            Role = "Requester",
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, vm.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SignInAsync(user);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var email = vm.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email);

        if (user != null)
        {
            user.ResetToken = Guid.NewGuid().ToString("N");
            user.ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
            await _db.SaveChangesAsync();
            vm.GeneratedLink = Url.Action("ResetPassword", "Account", new { token = user.ResetToken }, Request.Scheme);
        }

        ModelState.Clear();
        ViewBag.Done = true;
        return View(new ForgotPasswordVm { GeneratedLink = vm.GeneratedLink });
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction(nameof(ForgotPassword));

        var user = await _db.Users.FirstOrDefaultAsync(x => x.ResetToken == token && x.ResetTokenExpiresAt > DateTime.UtcNow);
        if (user == null)
        {
            TempData["error"] = "Ссылка для восстановления недействительна или истекла.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordVm { Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.ResetToken == vm.Token && x.ResetTokenExpiresAt > DateTime.UtcNow);
        if (user == null)
        {
            TempData["error"] = "Ссылка для восстановления недействительна или истекла.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        user.PasswordHash = _hasher.HashPassword(user, vm.Password);
        user.ResetToken = null;
        user.ResetTokenExpiresAt = null;
        await _db.SaveChangesAsync();

        TempData["ok"] = "Пароль успешно изменён. Теперь можно войти.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Denied()
    {
        return View();
    }

    private async Task SignInAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });
    }
}
