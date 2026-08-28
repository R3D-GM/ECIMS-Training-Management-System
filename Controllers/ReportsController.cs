using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;
using TCS.Models.ViewModels;

namespace TCS.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.ProjectManager + "," + Roles.TransportManager)]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ReportsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        // Pull the raw rows first and aggregate in memory. GroupBy-over-navigation-property
        // queries can be finicky to translate correctly across EF Core/SQLite versions, so
        // this sidesteps that entirely rather than risking a runtime translation error.
        var sessions = await _db.TrainingSessions.Include(s => s.Company).AsNoTracking().ToListAsync();
        var confirmations = await _db.TrainingConfirmations.AsNoTracking().ToListAsync();
        var transportAssignments = await _db.TransportAssignments.AsNoTracking().ToListAsync();

        var vm = new ReportsViewModel
        {
            TotalSessions = sessions.Count,
            CompletedSessions = sessions.Count(s => s.Status == SessionStatus.Completed),
            ScheduledSessions = sessions.Count(s => s.Status == SessionStatus.Scheduled),
            CancelledSessions = sessions.Count(s => s.Status == SessionStatus.Cancelled),

            PendingConfirmations = confirmations.Count(c => c.Status == ConfirmationStatus.Pending),
            ConfirmedConfirmations = confirmations.Count(c => c.Status == ConfirmationStatus.Confirmed),
            RejectedConfirmations = confirmations.Count(c => c.Status == ConfirmationStatus.Rejected),

            TotalTrainingHours = sessions.Sum(s => s.Duration),

            SessionsByModule = sessions
                .GroupBy(s => s.Module)
                .Select(g => new ModuleCount { Module = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList(),

            SessionsByCompany = sessions
                .GroupBy(s => s.Company?.Name ?? "Unknown")
                .Select(g => new CompanyCount { Company = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList(),

            TransportAssignments = transportAssignments.Count,
            TransportCompleted = transportAssignments.Count(t => t.Status == TransportStatus.Returned)
        };

        return View(vm);
    }
}
