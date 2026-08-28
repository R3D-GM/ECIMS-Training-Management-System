using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;

namespace TCS.Services;

// Implements the single business rule that links the two modules together:
// a company only moves into the UAT phase once every training session
// booked for it has actually finished. Used by the dashboard tab and by
// UatProjectsController to decide whether a new UAT project can be opened.
public static class UatWorkflow
{
    public static async Task<bool> IsCompanyReadyForUatAsync(ApplicationDbContext db, int companyId)
    {
        var sessions = await db.TrainingSessions
            .Where(s => s.CompanyId == companyId)
            .Select(s => s.Status)
            .ToListAsync();

        if (sessions.Count == 0) return false; // no training run yet - nothing to hand off
        return !sessions.Any(s => s is SessionStatus.Scheduled or SessionStatus.InProgress);
    }

    public static async Task<int> PendingTrainingCountAsync(ApplicationDbContext db, int companyId)
    {
        return await db.TrainingSessions
            .CountAsync(s => s.CompanyId == companyId && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.InProgress));
    }
}
