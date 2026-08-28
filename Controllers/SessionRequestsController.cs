using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;

namespace TCS.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.ProjectManager)]
public class SessionRequestsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public SessionRequestsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var list = await _db.SessionRequests
            .Include(r => r.Company)
            .Include(r => r.TransportAssignment)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();
        return View(list);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Create()
    {
        ViewBag.Companies = await _db.Companies.OrderBy(c => c.Name).ToListAsync();
        return View(new SessionRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Create(SessionRequest model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Companies = await _db.Companies.OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        model.RequestedBy = user?.FullName;
        model.Status = RequestStatus.Pending;
        _db.SessionRequests.Add(model);
        await _db.SaveChangesAsync();

        var company = await _db.Companies.FindAsync(model.CompanyId);
        var locationNote = model.LocationType == LocationType.Outdoor
            ? " — OUTDOOR, transport needs to be assigned"
            : " (indoor, no transport needed)";
        TCS.Services.Notifier.NotifyRole(_db, Roles.ProjectManager,
            $"New session request from {user?.FullName} for {company?.Name} ({model.RequestedModule}){locationNote}",
            "/SessionRequests/Review/" + model.Id);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Session request submitted for approval.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Review(int id)
    {
        var request = await _db.SessionRequests
            .Include(r => r.Company)
            .Include(r => r.TransportAssignment).ThenInclude(t => t!.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();

        ViewBag.Vehicles = await _db.Vehicles.Where(v => v.Status == "Available").ToListAsync();
        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.ProjectManager)]
    public async Task<IActionResult> Decide(int id, RequestStatus decision, string? notes)
    {
        var request = await _db.SessionRequests.Include(r => r.TransportAssignment).FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();

        // Outdoor sessions cannot be approved until the Transport Manager has approved
        // the transport assignment for them.
        if (decision == RequestStatus.Approved
            && request.LocationType == LocationType.Outdoor
            && request.TransportAssignment?.ApprovalStatus != TransportApprovalStatus.Approved)
        {
            TempData["Error"] = "This is an outdoor session — transport must be assigned and approved by the Transport Manager before you can approve it.";
            return RedirectToAction(nameof(Review), new { id });
        }

        var user = await _userManager.GetUserAsync(User);
        request.Status = decision;
        request.Notes = notes;
        request.DecidedBy = user?.FullName;
        request.DecidedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        var company = await _db.Companies.FindAsync(request.CompanyId);
        TCS.Services.Notifier.NotifyRole(_db, Roles.Consultant,
            $"Session request for {company?.Name} was {decision} by {user?.FullName}",
            "/SessionRequests");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Session request {decision}.";
        return RedirectToAction(nameof(Index));
    }

    // Project Manager assigns (or re-assigns after a Transport Manager rejection) a single
    // vehicle to one outdoor session request. This always starts life as PendingApproval —
    // the Transport Manager has to sign off before the PM can approve the session itself.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.ProjectManager)]
    public async Task<IActionResult> AssignTransport(int sessionRequestId, int vehicleId, string? notes)
    {
        var request = await _db.SessionRequests
            .Include(r => r.Company)
            .Include(r => r.TransportAssignment)
            .FirstOrDefaultAsync(r => r.Id == sessionRequestId);
        if (request == null) return NotFound();

        if (request.LocationType != LocationType.Outdoor)
        {
            TempData["Error"] = "Transport is only needed for outdoor sessions.";
            return RedirectToAction(nameof(Review), new { id = sessionRequestId });
        }

        if (request.TransportAssignment != null && request.TransportAssignment.ApprovalStatus != TransportApprovalStatus.Rejected)
        {
            TempData["Error"] = "This request already has a transport assignment.";
            return RedirectToAction(nameof(Review), new { id = sessionRequestId });
        }

        if (request.TransportAssignment != null)
        {
            // Re-assigning after a rejection — reuse the same record.
            var existing = request.TransportAssignment;
            existing.VehicleId = vehicleId;
            existing.ApprovalStatus = TransportApprovalStatus.PendingApproval;
            existing.RejectionNotes = null;
            existing.Notes = notes;
            existing.AssignedByRole = Roles.ProjectManager;
        }
        else
        {
            var assignment = new TransportAssignment
            {
                VehicleId = vehicleId,
                ApprovalStatus = TransportApprovalStatus.PendingApproval,
                AssignedByRole = Roles.ProjectManager,
                Notes = notes
            };
            _db.TransportAssignments.Add(assignment);
            await _db.SaveChangesAsync();
            request.TransportAssignmentId = assignment.Id;
        }
        await _db.SaveChangesAsync();

        TCS.Services.Notifier.NotifyRole(_db, Roles.TransportManager,
            $"Transport assignment awaiting your approval — {request.Company?.Name}, {request.Location}",
            "/TransportAssignments/PendingApprovals");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Transport assigned. Waiting for Transport Manager approval.";
        return RedirectToAction(nameof(Review), new { id = sessionRequestId });
    }
}
