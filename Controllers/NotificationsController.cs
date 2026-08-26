using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;

namespace TCS.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var role = (await _userManager.GetRolesAsync(user!)).FirstOrDefault() ?? "";

        var list = await _db.Notifications
            .Where(n => n.UserId == user!.Id || (n.UserId == null && n.TargetRole == role))
            .OrderByDescending(n => n.CreatedDate)
            .Take(50)
            .ToListAsync();

        return View(list);
    }

    // Marks a single notification read, then redirects to its link (or Dashboard)
    public async Task<IActionResult> Open(int id)
    {
        var n = await _db.Notifications.FindAsync(id);
        if (n != null)
        {
            n.IsRead = true;
            await _db.SaveChangesAsync();
            if (!string.IsNullOrEmpty(n.Link)) return Redirect(n.Link);
        }
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var user = await _userManager.GetUserAsync(User);
        var role = (await _userManager.GetRolesAsync(user!)).FirstOrDefault() ?? "";

        var mine = await _db.Notifications
            .Where(n => !n.IsRead && (n.UserId == user!.Id || (n.UserId == null && n.TargetRole == role)))
            .ToListAsync();
        foreach (var n in mine) n.IsRead = true;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
