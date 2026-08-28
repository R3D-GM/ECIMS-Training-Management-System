using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TCS.External;
using TCS.Models;
using TCS.Models.ViewModels;

namespace TCS.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ExternalSyncClient _sync;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ExternalSyncClient sync)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _sync = sync;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            // Safety net: if this user was created before the external
            // system was configured (or a previous sync attempt failed),
            // this makes sure they still get saved over there - Consignee
            // (keyed by username) -> User -> UserRoleMapper - the next
            // time they log in, instead of never getting synced at all.
            if (user.ExternalConsigneeId is null || user.ExternalUserId is null)
            {
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "";
                await UserSyncCoordinator.EnsureUserSyncedAsync(user, role, _sync, _userManager);
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Don't reveal whether the account exists.
            ViewBag.NotFound = true;
            return View();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        // No mail server is configured for this deployment, so instead of emailing a link
        // we take the user straight to the reset form with the token pre-filled.
        return RedirectToAction(nameof(ResetPassword), new { userId = user.Id, token });
    }

    [HttpGet]
    public IActionResult ResetPassword(string userId, string token)
    {
        return View(new ResetPasswordViewModel { UserId = userId, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid reset request.");
            return View(model);
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        TempData["Success"] = "Password reset successfully. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }
}
