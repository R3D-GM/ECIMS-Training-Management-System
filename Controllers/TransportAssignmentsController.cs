using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;

namespace TCS.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.TransportManager)]
public class TransportAssignmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    public TransportAssignmentsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var list = await _db.TransportAssignments
            .Include(t => t.TrainingSession).ThenInclude(s => s!.Company)
            .Include(t => t.SessionRequests).ThenInclude(r => r.Company)
            .Include(t => t.Vehicle)
            .Where(t => t.ApprovalStatus == TransportApprovalStatus.Approved)
            .OrderByDescending(t => t.Id)
            .ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new TransportAssignment());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TransportAssignment model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(model);
        }
        // Created directly by the Transport Manager (day-of vehicle assignment against an
        // already-scheduled session) — no separate approval loop needed here.
        model.ApprovalStatus = TransportApprovalStatus.Approved;
        model.ApprovedDate = DateTime.Now;
        model.AssignedByRole = Roles.TransportManager;
        _db.TransportAssignments.Add(model);
        await _db.SaveChangesAsync();

        var session = await _db.TrainingSessions.Include(s => s.Company).FirstOrDefaultAsync(s => s.Id == model.TrainingSessionId);
        TCS.Services.Notifier.NotifyRole(_db, Roles.Admin,
            $"Transport assigned for {session?.Module} ({session?.Company?.Name})",
            "/TransportAssignments");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Transport assigned.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordDeparture(int id)
    {
        var assignment = await _db.TransportAssignments.FindAsync(id);
        if (assignment == null) return NotFound();
        assignment.DepartureTime = DateTime.Now;
        assignment.Status = TransportStatus.Departed;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordReturn(int id)
    {
        var assignment = await _db.TransportAssignments.FindAsync(id);
        if (assignment == null) return NotFound();
        assignment.ReturnTime = DateTime.Now;
        assignment.Status = TransportStatus.Returned;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ---- Transport Manager: approving requests the PM has assigned a vehicle to ----

    public async Task<IActionResult> PendingApprovals()
    {
        var list = await _db.TransportAssignments
            .Include(t => t.Vehicle)
            .Include(t => t.SessionRequests).ThenInclude(r => r.Company)
            .Where(t => t.ApprovalStatus == TransportApprovalStatus.PendingApproval)
            .OrderBy(t => t.Id)
            .ToListAsync();
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveTransport(int id)
    {
        var assignment = await _db.TransportAssignments
            .Include(t => t.SessionRequests)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (assignment == null) return NotFound();

        assignment.ApprovalStatus = TransportApprovalStatus.Approved;
        assignment.ApprovedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        foreach (var request in assignment.SessionRequests)
        {
            TCS.Services.Notifier.NotifyRole(_db, Roles.ProjectManager,
                $"Transport approved for {request.Location} — you can now approve that session request",
                "/SessionRequests/Review/" + request.Id);
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = "Transport assignment approved.";
        return RedirectToAction(nameof(PendingApprovals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectTransport(int id, string? notes)
    {
        var assignment = await _db.TransportAssignments
            .Include(t => t.SessionRequests)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (assignment == null) return NotFound();

        assignment.ApprovalStatus = TransportApprovalStatus.Rejected;
        assignment.RejectionNotes = notes;
        await _db.SaveChangesAsync();

        foreach (var request in assignment.SessionRequests)
        {
            TCS.Services.Notifier.NotifyRole(_db, Roles.ProjectManager,
                $"Transport rejected for {request.Location}: {notes}. Please reassign.",
                "/SessionRequests/Review/" + request.Id);
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = "Transport assignment rejected. It will sit pending until the Project Manager reassigns it.";
        return RedirectToAction(nameof(PendingApprovals));
    }

    // Transport Manager groups several outdoor requests going to the same site into one
    // vehicle instead of the PM assigning one vehicle per department. Self-approved since
    // the Transport Manager is creating it directly.
    public async Task<IActionResult> CreateConsolidated()
    {
        var unassigned = await _db.SessionRequests
            .Include(r => r.Company)
            .Where(r => r.LocationType == LocationType.Outdoor
                        && r.TransportAssignmentId == null
                        && r.Status == RequestStatus.Pending)
            .OrderBy(r => r.Location)
            .ToListAsync();

        ViewBag.Vehicles = await _db.Vehicles.Where(v => v.Status == "Available").ToListAsync();
        return View(unassigned);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateConsolidated(int[] sessionRequestIds, int vehicleId, string? notes)
    {
        if (sessionRequestIds == null || sessionRequestIds.Length == 0)
        {
            TempData["Error"] = "Select at least one session request to group together.";
            return RedirectToAction(nameof(CreateConsolidated));
        }

        var assignment = new TransportAssignment
        {
            VehicleId = vehicleId,
            ApprovalStatus = TransportApprovalStatus.Approved,
            ApprovedDate = DateTime.Now,
            AssignedByRole = Roles.TransportManager,
            Notes = notes
        };
        _db.TransportAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        var requests = await _db.SessionRequests
            .Include(r => r.Company)
            .Where(r => sessionRequestIds.Contains(r.Id) && r.TransportAssignmentId == null)
            .ToListAsync();

        foreach (var request in requests)
        {
            request.TransportAssignmentId = assignment.Id;
            TCS.Services.Notifier.NotifyRole(_db, Roles.ProjectManager,
                $"Transport Manager grouped {request.Company?.Name} ({request.Location}) into a shared vehicle — approved, you can now approve the session",
                "/SessionRequests/Review/" + request.Id);
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{requests.Count} session request(s) grouped into one transport assignment.";
        return RedirectToAction(nameof(PendingApprovals));
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Sessions = await _db.TrainingSessions.Include(s => s.Company)
            .OrderByDescending(s => s.TrainingDate).ToListAsync();
        ViewBag.Vehicles = await _db.Vehicles.Where(v => v.Status == "Available").ToListAsync();
    }
}
