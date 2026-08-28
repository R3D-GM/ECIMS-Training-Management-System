using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.External;
using TCS.Models;

namespace TCS.Controllers;

[Authorize]
public class TrainingSessionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ExternalSyncClient _sync;

    public TrainingSessionsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ExternalSyncClient sync)
    {
        _db = db;
        _userManager = userManager;
        _sync = sync;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        IQueryable<TrainingSession> query = _db.TrainingSessions.Include(s => s.Company).Include(s => s.Trainer);

        if (User.IsInRole(Roles.Trainer) && !User.IsInRole(Roles.Admin))
            query = query.Where(s => s.TrainerId == user!.TrainerId);
        else if (User.IsInRole(Roles.ContactPerson) && !User.IsInRole(Roles.Admin))
            query = query.Where(s => s.CompanyId == user!.CompanyId);

        return View(await query.OrderByDescending(s => s.TrainingDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var session = await _db.TrainingSessions
            .Include(s => s.Company)
            .Include(s => s.Trainer)
            .Include(s => s.Trainees)
            .Include(s => s.Confirmation)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (session == null) return NotFound();
        return View(session);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new TrainingSession { TrainingDate = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Create(TrainingSession model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(model);
        }
        _db.TrainingSessions.Add(model);
        await _db.SaveChangesAsync();

        // Mirror this training as a Voucher (Type = TrainingSession) on
        // his system. Trainer/Company should already have ExternalConsigneeId
        // from when they were created, if syncing is turned on.
        await SyncTrainingVoucher(model, isNew: true);

        // Notify the assigned trainer and the company's contact person that a
        // training was scheduled for them - this was missing before.
        await NotifySessionParticipants(model, "A new training session was scheduled for you");

        TempData["Success"] = "Training session scheduled.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Edit(int id)
    {
        var session = await _db.TrainingSessions.FindAsync(id);
        if (session == null) return NotFound();
        await PopulateDropdowns();
        return View(session);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Edit(int id, TrainingSession model)
    {
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(model);
        }
        _db.TrainingSessions.Update(model);
        await _db.SaveChangesAsync();

        // "Save and update": since this training was already synced once,
        // this pushes the change to his system as an update, not a new record.
        await SyncTrainingVoucher(model, isNew: false);

        // If it was rescheduled or reassigned, let the trainer/company know again.
        await NotifySessionParticipants(model, "A training session assigned to you was updated");

        TempData["Success"] = "Training session updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var session = await _db.TrainingSessions.Include(s => s.Company).FirstOrDefaultAsync(s => s.Id == id);
        if (session == null) return NotFound();
        return View(session);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var session = await _db.TrainingSessions.FindAsync(id);
        if (session != null)
        {
            _db.TrainingSessions.Remove(session);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    // ---- Trainee roster (added inline on the Details page) ----

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.Trainer)]
    public async Task<IActionResult> AddTrainee(int trainingSessionId, string name, string? position, string? phone)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var trainee = new Trainee
            {
                TrainingSessionId = trainingSessionId,
                Name = name,
                Position = position,
                Phone = phone
            };
            _db.Trainees.Add(trainee);
            await _db.SaveChangesAsync();

            var externalId = await _sync.SyncConsigneeAndGetIdAsync(ExternalMapper.ToConsignee(trainee));
            if (externalId is not null)
            {
                trainee.ExternalConsigneeId = externalId;
                await _db.SaveChangesAsync();
            }
        }
        return RedirectToAction(nameof(Details), new { id = trainingSessionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Trainer)]
    public async Task<IActionResult> MarkAttendance(int traineeId, AttendanceStatus attendance)
    {
        var trainee = await _db.Trainees.FindAsync(traineeId);
        if (trainee == null) return NotFound();
        trainee.Attendance = attendance;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = trainee.TrainingSessionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Trainer)]
    public async Task<IActionResult> CompleteSession(int id)
    {
        var session = await _db.TrainingSessions.Include(s => s.Company).FirstOrDefaultAsync(s => s.Id == id);
        if (session == null) return NotFound();
        session.Status = SessionStatus.Completed;
        await _db.SaveChangesAsync();

        // If every training for this company is now done, let Consultants know
        // the company has unlocked for UAT.
        if (await TCS.Services.UatWorkflow.IsCompanyReadyForUatAsync(_db, session.CompanyId))
        {
            TCS.Services.Notifier.NotifyRole(_db, Roles.Consultant,
                $"{session.Company?.Name} has finished all trainings and is now ready for UAT",
                "/UatProjects/Create?companyId=" + session.CompanyId);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // Mirrors a training session as a Voucher: creates it the first time,
    // updates the same record (via ExternalVoucherId) on every later save.
    private async Task SyncTrainingVoucher(TrainingSession model, bool isNew)
    {
        var trainer = model.TrainerId != null ? await _db.Trainers.FindAsync(model.TrainerId) : null;
        var company = await _db.Companies.FindAsync(model.CompanyId);
        var currentUser = await _userManager.GetUserAsync(User);
        var contactPerson = await _userManager.Users.FirstOrDefaultAsync(u => u.CompanyId == model.CompanyId);

        var dto = ExternalMapper.ToVoucher(model, trainer?.ExternalConsigneeId, company?.ExternalConsigneeId,
            contactPerson?.ExternalConsigneeId, currentUser?.ExternalConsigneeId, currentUser?.ExternalUserId ?? 0);

        if (!isNew && model.ExternalVoucherId is not null)
        {
            await _sync.UpdateVoucherAsync(model.ExternalVoucherId.Value, dto);
        }
        else
        {
            var externalId = await _sync.SyncVoucherAndGetIdAsync(dto);
            if (externalId is not null)
            {
                model.ExternalVoucherId = externalId;
                await _db.SaveChangesAsync();
            }
        }
    }

    private async Task NotifySessionParticipants(TrainingSession model, string message)
    {
        if (model.TrainerId != null)
        {
            var trainerUser = await _userManager.Users.FirstOrDefaultAsync(u => u.TrainerId == model.TrainerId);
            if (trainerUser != null)
                TCS.Services.Notifier.NotifyUser(_db, trainerUser.Id,
                    $"{message}: {model.Module} on {model.TrainingDate:MMM dd, yyyy}",
                    "/TrainingSessions/Details/" + model.Id);
        }

        var contactUser = await _userManager.Users.FirstOrDefaultAsync(u => u.CompanyId == model.CompanyId);
        if (contactUser != null)
            TCS.Services.Notifier.NotifyUser(_db, contactUser.Id,
                $"{message}: {model.Module} on {model.TrainingDate:MMM dd, yyyy}",
                "/TrainingSessions/Details/" + model.Id);

        await _db.SaveChangesAsync();
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Companies = await _db.Companies.OrderBy(c => c.Name).ToListAsync();
        ViewBag.Trainers = await _db.Trainers.OrderBy(t => t.Name).ToListAsync();
    }
}
