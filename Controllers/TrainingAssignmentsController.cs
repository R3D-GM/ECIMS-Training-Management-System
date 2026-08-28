using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;
using TCS.Models.ViewModels;

namespace TCS.Controllers;

// Tracks a trainer/consultant's on-site departure and return for a training
// session — distinct from vehicle logistics (see TransportAssignmentsController).
[Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.Trainer)]
public class TrainingAssignmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    public TrainingAssignmentsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var sessions = await _db.TrainingSessions
            .Include(s => s.Company)
            .Include(s => s.Trainer)
            .Where(s => s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.InProgress)
            .OrderBy(s => s.TrainingDate)
            .ToListAsync();

        var today = DateTime.Today;

        var vm = new TrainingAssignmentViewModel
        {
            OnTrainingNow = sessions.Count(s => s.DepartureTime != null && s.ReturnTime == null),
            DepartureToday = sessions.Count(s => s.TrainingDate.Date == today && s.DepartureTime == null),
            ReturningToday = sessions.Count(s => s.DepartureTime != null && s.ReturnTime == null && s.TrainingDate.Date == today),
            UpcomingAssignments = sessions.Count(s => s.TrainingDate.Date > today && s.DepartureTime == null),
            ActiveAssignments = sessions.Where(s => s.ReturnTime == null).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.Trainer)]
    public async Task<IActionResult> RecordDeparture(int id)
    {
        var session = await _db.TrainingSessions.FindAsync(id);
        if (session == null) return NotFound();
        session.DepartureTime = DateTime.Now;
        session.Status = SessionStatus.InProgress;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Departure recorded.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.Trainer)]
    public async Task<IActionResult> RecordReturn(int id)
    {
        var session = await _db.TrainingSessions.FindAsync(id);
        if (session == null) return NotFound();
        session.ReturnTime = DateTime.Now;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Return recorded.";
        return RedirectToAction(nameof(Index));
    }
}
