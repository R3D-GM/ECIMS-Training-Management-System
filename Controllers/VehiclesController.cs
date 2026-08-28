using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;

namespace TCS.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.TransportManager)]
public class VehiclesController : Controller
{
    private readonly ApplicationDbContext _db;
    public VehiclesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index() => View(await _db.Vehicles.OrderBy(v => v.PlateNumber).ToListAsync());

    public IActionResult Create() => View(new Vehicle());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vehicle vehicle)
    {
        if (!ModelState.IsValid) return View(vehicle);
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Vehicle added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var vehicle = await _db.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();
        return View(vehicle);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Vehicle vehicle)
    {
        if (id != vehicle.Id) return NotFound();
        if (!ModelState.IsValid) return View(vehicle);
        _db.Vehicles.Update(vehicle);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Vehicle updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var vehicle = await _db.Vehicles.FindAsync(id);
        if (vehicle != null)
        {
            _db.Vehicles.Remove(vehicle);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
