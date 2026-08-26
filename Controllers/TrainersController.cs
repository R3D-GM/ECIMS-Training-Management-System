using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.External;
using TCS.Models;

namespace TCS.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.ProjectManager + "," + Roles.Trainer)]
public class TrainersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ExternalSyncClient _sync;

    public TrainersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ExternalSyncClient sync)
    {
        _db = db;
        _userManager = userManager;
        _sync = sync;
    }

    public async Task<IActionResult> Index()
    {
        // Trainer role only sees their own profile card
        if (User.IsInRole(Roles.Trainer) && !User.IsInRole(Roles.Admin))
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var mine = await _db.Trainers.Where(t => t.Id == currentUser!.TrainerId).ToListAsync();
            return View(mine);
        }

        return View(await _db.Trainers.OrderBy(t => t.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var trainer = await _db.Trainers
            .Include(t => t.TrainingSessions)
            .ThenInclude(s => s.Company)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (trainer == null) return NotFound();
        return View(trainer);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public IActionResult Create() => View(new Trainer());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Create(Trainer model)
    {
        if (!ModelState.IsValid) return View(model);
        _db.Trainers.Add(model);
        await _db.SaveChangesAsync();

        var externalId = await _sync.SyncConsigneeAndGetIdAsync(ExternalMapper.ToConsignee(model));
        if (externalId is not null)
        {
            model.ExternalConsigneeId = externalId;
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Trainer added.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Edit(int id)
    {
        var trainer = await _db.Trainers.FindAsync(id);
        if (trainer == null) return NotFound();
        return View(trainer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Edit(int id, Trainer model)
    {
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid) return View(model);
        _db.Trainers.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Trainer updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var trainer = await _db.Trainers.FindAsync(id);
        if (trainer == null) return NotFound();
        return View(trainer);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var trainer = await _db.Trainers.FindAsync(id);
        if (trainer != null)
        {
            _db.Trainers.Remove(trainer);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    // Trainer signs up a reusable default signature (captured once via pad)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Trainer)]
    public async Task<IActionResult> SaveSignature(int id, string signatureData)
    {
        var trainer = await _db.Trainers.FindAsync(id);
        if (trainer == null) return NotFound();
        trainer.SignaturePath = signatureData;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }
}
