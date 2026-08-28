using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;

namespace TCS.Controllers;

[Authorize]
public class TrainingConfirmationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public TrainingConfirmationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        IQueryable<TrainingConfirmation> query = _db.TrainingConfirmations
            .Include(c => c.TrainingSession).ThenInclude(s => s!.Company)
            .Include(c => c.TrainingSession).ThenInclude(s => s!.Trainer);

        if (User.IsInRole(Roles.Trainer) && !User.IsInRole(Roles.Admin))
            query = query.Where(c => c.TrainingSession!.TrainerId == user!.TrainerId);
        else if (User.IsInRole(Roles.ContactPerson) && !User.IsInRole(Roles.Admin))
            query = query.Where(c => c.TrainingSession!.CompanyId == user!.CompanyId);

        return View(await query.OrderByDescending(c => c.SubmittedDate).ToListAsync());
    }

    // Consultant/Admin/Trainer initiates the confirmation once a session is complete
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.Trainer)]
    public async Task<IActionResult> Create(int trainingSessionId)
    {
        var session = await _db.TrainingSessions.Include(s => s.Company).Include(s => s.Trainer)
            .FirstOrDefaultAsync(s => s.Id == trainingSessionId);
        if (session == null) return NotFound();

        var existing = await _db.TrainingConfirmations.FirstOrDefaultAsync(c => c.TrainingSessionId == trainingSessionId);
        if (existing != null) return RedirectToAction(nameof(Details), new { id = existing.Id });

        var model = new TrainingConfirmation
        {
            TrainingSessionId = trainingSessionId,
            ContactPersonName = session.Company?.ContactPersonName
        };
        ViewBag.Session = session;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.Trainer)]
    public async Task<IActionResult> Create(TrainingConfirmation model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Session = await _db.TrainingSessions.FindAsync(model.TrainingSessionId);
            return View(model);
        }
        model.Status = ConfirmationStatus.Pending;
        model.SubmittedDate = DateTime.Now;
        _db.TrainingConfirmations.Add(model);
        await _db.SaveChangesAsync();

        var session = await _db.TrainingSessions.Include(s => s.Company).Include(s => s.Trainer)
            .FirstOrDefaultAsync(s => s.Id == model.TrainingSessionId);

        if (session?.Trainer != null)
        {
            var trainerUser = await _userManager.Users.FirstOrDefaultAsync(u => u.TrainerId == session.TrainerId);
            if (trainerUser != null)
                TCS.Services.Notifier.NotifyUser(_db, trainerUser.Id,
                    $"Please sign the training confirmation for {session.Module} ({session.Company?.Name})",
                    "/TrainingConfirmations/Details/" + model.Id);
        }
        if (session?.Company != null)
        {
            var contactUser = await _userManager.Users.FirstOrDefaultAsync(u => u.CompanyId == session.CompanyId);
            if (contactUser != null)
                TCS.Services.Notifier.NotifyUser(_db, contactUser.Id,
                    $"A training confirmation for {session.Module} is awaiting your signature",
                    "/TrainingConfirmations/Details/" + model.Id);
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = "Confirmation created. Awaiting signatures.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var confirmation = await _db.TrainingConfirmations
            .Include(c => c.TrainingSession).ThenInclude(s => s!.Company)
            .Include(c => c.TrainingSession).ThenInclude(s => s!.Trainer)
            .Include(c => c.TrainingSession).ThenInclude(s => s!.Trainees)
            .Include(c => c.ManagerApproval)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (confirmation == null) return NotFound();
        return View(confirmation);
    }

    // Matches the printable "CLIENT TRAINING CONFIRMATION" PDF layout
    public async Task<IActionResult> Certificate(int id)
    {
        var confirmation = await _db.TrainingConfirmations
            .Include(c => c.TrainingSession).ThenInclude(s => s!.Company)
            .Include(c => c.TrainingSession).ThenInclude(s => s!.Trainer)
            .Include(c => c.TrainingSession).ThenInclude(s => s!.Trainees)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (confirmation == null) return NotFound();
        return View(confirmation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Trainer)]
    public async Task<IActionResult> SignTrainer(int id, string signatureData)
    {
        var confirmation = await _db.TrainingConfirmations.FindAsync(id);
        if (confirmation == null) return NotFound();
        confirmation.TrainerSignaturePath = signatureData;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Trainer signature captured.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.ContactPerson)]
    public async Task<IActionResult> SignContact(int id, string signatureData, string contactPersonName, string? remarks, ConfirmationStatus decision)
    {
        var confirmation = await _db.TrainingConfirmations.FindAsync(id);
        if (confirmation == null) return NotFound();

        confirmation.ContactPersonSignaturePath = signatureData;
        confirmation.ContactPersonName = contactPersonName;
        confirmation.Remarks = remarks;
        confirmation.Status = decision; // Confirmed or Rejected
        confirmation.DecidedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        if (decision == ConfirmationStatus.Confirmed)
        {
            TCS.Services.Notifier.NotifyRole(_db, Roles.ProjectManager,
                $"A confirmation from {contactPersonName} is ready for your batch approval",
                "/TrainingConfirmations/PendingApproval");
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = $"Confirmation {decision} by client.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---- Project Manager batch approval workflow ----

    [Authorize(Roles = Roles.Admin + "," + Roles.ProjectManager)]
    public async Task<IActionResult> PendingApproval()
    {
        var list = await _db.TrainingConfirmations
            .Include(c => c.TrainingSession).ThenInclude(s => s!.Company)
            .Where(c => c.Status == ConfirmationStatus.Confirmed && c.ManagerApprovalId == null)
            .ToListAsync();
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.ProjectManager)]
    public async Task<IActionResult> ApproveBatch(int[] confirmationIds, string? notes)
    {
        if (confirmationIds == null || confirmationIds.Length == 0)
        {
            TempData["Error"] = "Select at least one confirmation to approve.";
            return RedirectToAction(nameof(PendingApproval));
        }

        var user = await _userManager.GetUserAsync(User);
        var approval = new ManagerApproval
        {
            ManagerName = user?.FullName,
            ApprovalDate = DateTime.Now,
            Notes = notes
        };
        _db.ManagerApprovals.Add(approval);
        await _db.SaveChangesAsync();

        var confirmations = await _db.TrainingConfirmations
            .Include(c => c.TrainingSession)
            .Where(c => confirmationIds.Contains(c.Id) && c.ManagerApprovalId == null)
            .ToListAsync();

        foreach (var c in confirmations)
        {
            c.ManagerApprovalId = approval.Id;
            _db.ApprovalPapers.Add(new ApprovalPaper
            {
                TrainingConfirmationId = c.Id,
                GeneratedDate = DateTime.Now,
                FilePath = $"/TrainingConfirmations/Certificate/{c.Id}"
            });

            if (c.TrainingSession != null)
            {
                var trainerUser = await _userManager.Users.FirstOrDefaultAsync(u => u.TrainerId == c.TrainingSession.TrainerId);
                if (trainerUser != null)
                    TCS.Services.Notifier.NotifyUser(_db, trainerUser.Id,
                        $"Your training confirmation for {c.TrainingSession.Module} was approved",
                        "/TrainingConfirmations/Certificate/" + c.Id);

                var contactUser = await _userManager.Users.FirstOrDefaultAsync(u => u.CompanyId == c.TrainingSession.CompanyId);
                if (contactUser != null)
                    TCS.Services.Notifier.NotifyUser(_db, contactUser.Id,
                        $"Your training confirmation for {c.TrainingSession.Module} was approved",
                        "/TrainingConfirmations/Certificate/" + c.Id);
            }
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{confirmations.Count} confirmation(s) approved in batch #{approval.Id}.";
        return RedirectToAction(nameof(PendingApproval));
    }
}
