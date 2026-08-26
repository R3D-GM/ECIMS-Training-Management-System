using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;

namespace TCS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class TraineesController : Controller
{
    private readonly ApplicationDbContext _db;
    public TraineesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q, AttendanceStatus? status)
    {
        var query = _db.Trainees.Include(t => t.TrainingSession).ThenInclude(s => s!.Company).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.Name.Contains(q) || (t.Position != null && t.Position.Contains(q)));
        if (status.HasValue)
            query = query.Where(t => t.Attendance == status);

        ViewBag.Query = q;
        ViewBag.Status = status;

        return View(await query.OrderByDescending(t => t.Id).ToListAsync());
    }

    public async Task<IActionResult> Edit(int id)
    {
        var trainee = await _db.Trainees.FindAsync(id);
        if (trainee == null) return NotFound();
        return View(trainee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Trainee model)
    {
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid) return View(model);

        var trainee = await _db.Trainees.FindAsync(id);
        if (trainee == null) return NotFound();

        trainee.Name = model.Name;
        trainee.Position = model.Position;
        trainee.Phone = model.Phone;
        trainee.Attendance = model.Attendance;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Trainee updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var trainee = await _db.Trainees.FindAsync(id);
        if (trainee != null)
        {
            _db.Trainees.Remove(trainee);
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "Trainee removed.";
        return RedirectToAction(nameof(Index));
    }
}
