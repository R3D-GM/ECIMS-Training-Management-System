using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.External;
using TCS.Models;

namespace TCS.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.ProjectManager)]
public class CompaniesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ExternalSyncClient _sync;
    public CompaniesController(ApplicationDbContext db, ExternalSyncClient sync)
    {
        _db = db;
        _sync = sync;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.Companies.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Name.Contains(q) || (c.Branch != null && c.Branch.Contains(q)));

        ViewBag.Query = q;
        return View(await query.OrderBy(c => c.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var company = await _db.Companies
            .Include(c => c.TrainingSessions)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound();
        return View(company);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public IActionResult Create() => View(new Company());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Create(Company model)
    {
        if (!ModelState.IsValid) return View(model);
        _db.Companies.Add(model);
        await _db.SaveChangesAsync();

        // Mirror this company to the instructor's system as a Consignee.
        // Does nothing yet until ExternalSystem:BaseUrl is set in appsettings.json.
        var externalId = await _sync.SyncConsigneeAndGetIdAsync(ExternalMapper.ToConsignee(model));
        if (externalId is not null)
        {
            model.ExternalConsigneeId = externalId;
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Company created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Edit(int id)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company == null) return NotFound();
        return View(company);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Edit(int id, Company model)
    {
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid) return View(model);
        _db.Companies.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Company updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company == null) return NotFound();
        return View(company);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company != null)
        {
            _db.Companies.Remove(company);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
