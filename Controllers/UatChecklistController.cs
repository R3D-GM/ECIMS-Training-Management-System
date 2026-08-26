using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.External;
using TCS.Models;
using TCS.Models.ViewModels;

namespace TCS.Controllers;

// The core UAT workflow screen: fill in the question bank for one attempt,
// mark each item Pass/Fail/N-A, attach evidence, then submit for customer
// review. Consultant edits; everyone else on the project views read-only.
[Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.ProjectManager + "," + Roles.ContactPerson + "," + Roles.CustomerService)]
public class UatChecklistController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ExternalSyncClient _sync;

    public UatChecklistController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ExternalSyncClient sync)
    {
        _db = db;
        _userManager = userManager;
        _sync = sync;
    }

    // Consultant/Admin kicks off the first (or a re-attempt after decline).
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> StartAttempt(int uatProjectId)
    {
        var user = await _userManager.GetUserAsync(User);

        var project = await _db.UatProjects
            .Include(p => p.Attempts).ThenInclude(a => a.Results)
            .FirstOrDefaultAsync(p => p.Id == uatProjectId);
        if (project is null) return NotFound();

        var previousAttempt = project.Attempts.OrderByDescending(a => a.AttemptNumber).FirstOrDefault();
        if (previousAttempt is not null && previousAttempt.OverallStatus == AttemptOverallStatus.InProgress)
        {
            // Already has an open attempt - just go straight to it.
            return RedirectToAction(nameof(Attempt), new { id = previousAttempt.Id });
        }

        var nextAttemptNumber = (previousAttempt?.AttemptNumber ?? 0) + 1;

        List<UatMasterItem> masterItems;
        if (previousAttempt is not null && previousAttempt.OverallStatus == AttemptOverallStatus.Declined)
        {
            // Re-attempt: carry forward exactly the items from the declined attempt.
            var priorIds = previousAttempt.Results.Select(r => r.MasterItemId).ToList();
            masterItems = await _db.UatMasterItems
                .Where(m => priorIds.Contains(m.Id))
                .Include(m => m.Section)
                .OrderBy(m => m.Section!.DisplayOrder).ThenBy(m => m.DisplayOrder)
                .ToListAsync();
        }
        else
        {
            masterItems = await _db.UatMasterItems
                .Where(m => m.IsActive && m.Section!.IsActive)
                .Include(m => m.Section)
                .OrderBy(m => m.Section!.DisplayOrder).ThenBy(m => m.DisplayOrder)
                .ToListAsync();
        }

        var attempt = new ProjectUatAttempt
        {
            UatProjectId = project.Id,
            AttemptNumber = nextAttemptNumber,
            StartedDate = DateTime.Now,
            OverallStatus = AttemptOverallStatus.InProgress,
            InitiatedById = user?.Id
        };
        _db.ProjectUatAttempts.Add(attempt);
        await _db.SaveChangesAsync();

        var carryForward = previousAttempt is not null && previousAttempt.OverallStatus == AttemptOverallStatus.Declined
            ? previousAttempt.Results.ToDictionary(r => r.MasterItemId)
            : null;

        foreach (var item in masterItems)
        {
            ProjectUatResult? prior = null;
            carryForward?.TryGetValue(item.Id, out prior);

            _db.ProjectUatResults.Add(new ProjectUatResult
            {
                AttemptId = attempt.Id,
                MasterItemId = item.Id,
                PassStatus = prior?.PassStatus ?? PassStatus.Pending,
                Comment = prior?.Comment,
                IsFlagged = prior?.IsFlagged ?? false,
                ExecutedById = user?.Id,
                ExecutedDate = DateTime.Now
            });
        }

        project.Status = ProjectStatus.Active;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Attempt), new { id = attempt.Id });
    }

    // Consultant's editable checklist form.
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> Attempt(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var attemptRow = await _db.ProjectUatAttempts.Include(a => a.UatProject).FirstOrDefaultAsync(a => a.Id == id);
        if (attemptRow is null) return NotFound();
        if (!User.IsInRole(Roles.Admin) && attemptRow.UatProject!.ConsultantId != user?.Id) return Forbid();

        var model = await BuildViewModel(id, readOnly: false);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant)]
    public async Task<IActionResult> SaveChecklist(int attemptId, List<ChecklistItemInput> items, string action = "save")
    {
        var user = await _userManager.GetUserAsync(User);

        var attempt = await _db.ProjectUatAttempts
            .Include(a => a.UatProject)
            .Include(a => a.Results)
            .FirstOrDefaultAsync(a => a.Id == attemptId);
        if (attempt is null) return NotFound();

        foreach (var input in items)
        {
            if (input.PassStatus == PassStatus.Fail && string.IsNullOrWhiteSpace(input.Comment))
                ModelState.AddModelError("", "A comment is required for every item marked Fail.");
        }

        if (!ModelState.IsValid)
        {
            var reloaded = await BuildViewModel(attemptId, readOnly: false);
            return View("Attempt", reloaded);
        }

        foreach (var input in items)
        {
            var result = attempt.Results.First(r => r.Id == input.ResultId);
            var isResolvingFlag = result.IsFlagged && (result.PassStatus != input.PassStatus || input.ResolveFlag);
            if (isResolvingFlag) result.IsFlagged = false;

            result.PassStatus = input.PassStatus;
            result.Comment = isResolvingFlag ? null : input.Comment;

            if (input.EvidenceFile is not null && input.EvidenceFile.Length > 0)
            {
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(input.EvidenceFile.FileName)}";
                var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "evidence");
                Directory.CreateDirectory(folder);
                using var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create);
                await input.EvidenceFile.CopyToAsync(stream);
                result.EvidencePath = $"/uploads/evidence/{fileName}";
            }

            result.ExecutedById = user?.Id;
            result.ExecutedDate = DateTime.Now;
            result.LastModifiedById = user?.Id;
            result.LastModifiedByRole = SignatoryRole.Consultant;
        }

        if (action == "submit")
        {
            if (attempt.Results.Any(r => r.PassStatus == PassStatus.Pending))
            {
                ModelState.AddModelError("", "Every item must be answered before you can submit for customer review.");
                var reloaded = await BuildViewModel(attemptId, readOnly: false);
                return View("Attempt", reloaded);
            }

            attempt.OverallStatus = AttemptOverallStatus.SubmittedForReview;
            attempt.SubmittedDate = DateTime.Now;
            attempt.UatProject!.Status = ProjectStatus.AwaitingCustomerReview;

            _db.Notifications.Add(new Notification
            {
                TargetRole = Roles.ContactPerson,
                Title = "UAT checklist ready for review",
                Message = $"{attempt.UatProject.ProjectName} has been submitted for your review.",
                UatProjectId = attempt.UatProjectId,
                Link = $"/UatChecklist/View?attemptId={attempt.Id}"
            });
        }

        await _db.SaveChangesAsync();

        if (action == "submit")
        {
            // "Save and update": push the project's Voucher to Definition =
            // Submitted, since it already has an ExternalVoucherId from creation.
            var project = await _db.UatProjects.Include(p => p.CompanyBranch).ThenInclude(b => b!.Company)
                .Include(p => p.Consultant).Include(p => p.ProjectManager).FirstOrDefaultAsync(p => p.Id == attempt.UatProjectId);
            if (project?.ExternalVoucherId is not null)
            {
                var createdBy = project.CreatedById != null ? await _userManager.FindByIdAsync(project.CreatedById) : null;
                var dto = ExternalMapper.ToVoucher(project, ConstantCodes.VoucherDefinition_UatSubmitted,
                    project.Consultant?.ExternalConsigneeId, project.CompanyBranch?.Company?.ExternalConsigneeId, user?.ExternalUserId ?? 0,
                    project.ProjectManager?.ExternalConsigneeId, createdBy?.ExternalConsigneeId);
                await _sync.UpdateVoucherAsync(project.ExternalVoucherId.Value, dto);
            }
        }

        return RedirectToAction("Details", "UatProjects", new { id = attempt.UatProjectId });
    }

    // Read-only viewer for PM / Contact Person / Customer Service / Admin.
    public async Task<IActionResult> View(int attemptId)
    {        var user = await _userManager.GetUserAsync(User);
        var role = (await _userManager.GetRolesAsync(user!)).FirstOrDefault() ?? "";

        var attemptRow = await _db.ProjectUatAttempts
            .Include(a => a.UatProject).ThenInclude(p => p!.CompanyBranch).ThenInclude(b => b!.Company)
            .FirstOrDefaultAsync(a => a.Id == attemptId);
        if (attemptRow is null) return NotFound();

        var authorized = role switch
        {
            Roles.Admin => true,
            Roles.Consultant => attemptRow.UatProject!.ConsultantId == user?.Id,
            Roles.ProjectManager => attemptRow.UatProject!.ProjectManagerId == user?.Id,
            Roles.ContactPerson => attemptRow.UatProject!.CompanyBranch!.CompanyId == user?.CompanyId,
            Roles.CustomerService => attemptRow.UatProject!.SentToCustomerServiceDate is not null,
            _ => false
        };
        if (!authorized) return Forbid();

        var model = await BuildViewModel(attemptId, readOnly: true);
        if (model is null) return NotFound();
        return View("Attempt", model);
    }

    private async Task<AttemptExecuteViewModel?> BuildViewModel(int attemptId, bool readOnly)
    {
        var attempt = await _db.ProjectUatAttempts
            .Include(a => a.UatProject).ThenInclude(p => p!.CompanyBranch).ThenInclude(b => b!.Company)
            .Include(a => a.UatProject).ThenInclude(p => p!.ProjectManager)
            .Include(a => a.Results).ThenInclude(r => r.MasterItem).ThenInclude(m => m!.Section)
            .Include(a => a.Results).ThenInclude(r => r.LastModifiedBy)
            .Include(a => a.Results).ThenInclude(r => r.History)
            .Include(a => a.Signatures).ThenInclude(s => s.SignedBy)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt is null) return null;

        var signature = attempt.Signatures.FirstOrDefault(s => s.SignatoryRole == SignatoryRole.CustomerRepresentative);
        string lockText = "", lockClass = "";
        bool isReadOnly = readOnly
            || attempt.UatProject!.Status == ProjectStatus.Completed
            || attempt.OverallStatus == AttemptOverallStatus.SubmittedForReview;

        if (isReadOnly)
        {
            if (attempt.OverallStatus == AttemptOverallStatus.Declined)
            {
                var declinedOn = attempt.DecidedDate is not null ? $" on {attempt.DecidedDate:MMM d, yyyy}" : "";
                lockText = $"Declined{declinedOn}. The consultant is reviewing feedback and will resend it shortly.";
                lockClass = "lock-banner-declined";
            }
            else if (signature is not null)
            {
                lockText = $"Signed and locked. Signed by {signature.SignedBy?.FullName} on {signature.DateStamped:MMM d, yyyy, h:mm tt}.";
                lockClass = "lock-banner-signed";
            }
            else
            {
                lockText = "This checklist is locked and read-only.";
                lockClass = "lock-banner-neutral";
            }
        }

        return new AttemptExecuteViewModel
        {
            AttemptId = attempt.Id,
            UatProjectId = attempt.UatProjectId,
            AttemptNumber = attempt.AttemptNumber,
            ProjectName = attempt.UatProject.ProjectName,
            CompanyName = attempt.UatProject.CompanyBranch?.Company?.Name ?? "",
            BranchName = attempt.UatProject.CompanyBranch?.BranchName ?? "",
            ProjectManagerName = attempt.UatProject.ProjectManager?.FullName,
            StartDate = attempt.UatProject.StartDate,
            SentDate = attempt.SubmittedDate,
            TotalItems = attempt.Results.Count,
            CompletedItems = attempt.Results.Count(r => r.PassStatus != PassStatus.Pending),
            IsReadOnly = isReadOnly,
            StatusLabel = attempt.OverallStatus.ToString(),
            StatusCssClass = attempt.OverallStatus.ToString().ToLower(),
            LockBannerCssClass = lockClass,
            LockBannerText = lockText,
            Sections = attempt.Results
                .GroupBy(r => new { r.MasterItem!.SectionId, SectionName = r.MasterItem.Section!.SectionName })
                .OrderBy(g => g.Key.SectionId)
                .Select(g => new ChecklistSectionGroup
                {
                    SectionName = g.Key.SectionName,
                    Items = g.Select(r => new ChecklistItemInput
                    {
                        ResultId = r.Id,
                        TestDescription = r.MasterItem!.TestDescription,
                        PassStatus = r.PassStatus,
                        Comment = r.Comment,
                        EvidencePath = r.EvidencePath,
                        IsFlagged = r.IsFlagged,
                        CommentAuthorName = r.LastModifiedBy?.FullName,
                        WasChanged = r.History.Any(h => h.PreEditPassStatus != h.PostEditPassStatus)
                    }).ToList()
                }).ToList()
        };
    }

    // ---- Customer decide / accept / decline ----

    [Authorize(Roles = Roles.Admin + "," + Roles.ContactPerson)]
    public async Task<IActionResult> Decide(int attemptId)
    {
        var user = await _userManager.GetUserAsync(User);

        var attempt = await _db.ProjectUatAttempts
            .Include(a => a.UatProject).ThenInclude(p => p!.CompanyBranch).ThenInclude(b => b!.Company)
            .Include(a => a.Results).ThenInclude(r => r.MasterItem)
            .Include(a => a.Results).ThenInclude(r => r.History)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt is null) return NotFound();
        if (!User.IsInRole(Roles.Admin) && attempt.UatProject!.CompanyBranch!.CompanyId != user?.CompanyId) return Forbid();

        if (attempt.OverallStatus != AttemptOverallStatus.SubmittedForReview)
        {
            if (attempt.OverallStatus is AttemptOverallStatus.Declined or AttemptOverallStatus.Accepted)
                return RedirectToAction(nameof(View), new { attemptId });
            return RedirectToAction("Details", "UatProjects", new { id = attempt.UatProjectId });
        }

        var model = new CustomerDecideViewModel
        {
            AttemptId = attempt.Id,
            CompanyName = attempt.UatProject!.CompanyBranch!.Company?.Name ?? "",
            BranchName = attempt.UatProject.CompanyBranch.BranchName,
            TotalItems = attempt.Results.Count,
            PassedCount = attempt.Results.Count(r => r.PassStatus == PassStatus.Pass),
            FailedCount = attempt.Results.Count(r => r.PassStatus == PassStatus.Fail),
            NAItems = attempt.Results.Count(r => r.PassStatus == PassStatus.NA)
        };

        foreach (var r in attempt.Results.Where(r => r.PassStatus == PassStatus.Fail || r.IsFlagged))
        {
            model.Changes.Add(new CustomerChangeRow
            {
                TestDescription = r.MasterItem!.TestDescription,
                IsFlagged = r.IsFlagged,
                Comment = r.Comment
            });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.ContactPerson)]
    public async Task<IActionResult> Accept(int attemptId)
    {
        var user = await _userManager.GetUserAsync(User);
        var attempt = await _db.ProjectUatAttempts
            .Include(a => a.UatProject).ThenInclude(p => p!.CompanyBranch).ThenInclude(b => b!.Company)
            .FirstOrDefaultAsync(a => a.Id == attemptId);
        if (attempt is null) return NotFound();
        if (!User.IsInRole(Roles.Admin) && attempt.UatProject!.CompanyBranch!.CompanyId != user?.CompanyId) return Forbid();

        if (attempt.OverallStatus != AttemptOverallStatus.Accepted)
        {
            attempt.OverallStatus = AttemptOverallStatus.Accepted;
            attempt.DecidedDate = DateTime.Now;
            attempt.DecidedById = user?.Id;
            attempt.UatProject!.Status = ProjectStatus.AwaitingConsultantSignature;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Index", "UatSignoff", new { id = attemptId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.ContactPerson)]
    public async Task<IActionResult> Decline(int attemptId, string? reason)
    {
        var user = await _userManager.GetUserAsync(User);
        var attempt = await _db.ProjectUatAttempts
            .Include(a => a.UatProject).ThenInclude(p => p!.CompanyBranch).ThenInclude(b => b!.Company)
            .Include(a => a.Results)
            .FirstOrDefaultAsync(a => a.Id == attemptId);
        if (attempt is null) return NotFound();
        if (!User.IsInRole(Roles.Admin) && attempt.UatProject!.CompanyBranch!.CompanyId != user?.CompanyId) return Forbid();

        if (attempt.Results.All(r => !r.IsFlagged && r.PassStatus != PassStatus.Fail))
        {
            TempData["Error"] = "At least one item needs to be marked Fail (or flagged) before declining, so the consultant knows what to fix.";
            return RedirectToAction(nameof(Decide), new { attemptId });
        }

        attempt.OverallStatus = AttemptOverallStatus.Declined;
        attempt.DecidedDate = DateTime.Now;
        attempt.DecidedById = user?.Id;
        attempt.UatProject!.Status = ProjectStatus.Declined;

        TCS.Services.Notifier.NotifyUser(_db, attempt.UatProject.ConsultantId ?? "",
            $"{attempt.UatProject.CompanyBranch!.Company?.Name} declined the UAT checklist for {attempt.UatProject.ProjectName}. {reason}",
            $"/UatProjects/Details/{attempt.UatProjectId}");

        await _db.SaveChangesAsync();

        TempData["Success"] = "Checklist declined and sent back to the consultant.";
        return RedirectToAction("Details", "UatProjects", new { id = attempt.UatProjectId });
    }
}
