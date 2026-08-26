using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.External;
using TCS.Models;
using TCS.Models.ViewModels;

namespace TCS.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.ContactPerson + "," + Roles.Consultant + "," + Roles.ProjectManager)]
public class UatSignoffController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ExternalSyncClient _sync;

    public UatSignoffController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ExternalSyncClient sync)
    {
        _db = db;
        _userManager = userManager;
        _sync = sync;
    }

    public async Task<IActionResult> Index(int id)
    {
        var attempt = await _db.ProjectUatAttempts
            .Include(a => a.UatProject).ThenInclude(p => p!.CompanyBranch).ThenInclude(b => b!.Company)
            .Include(a => a.UatProject).ThenInclude(p => p!.ProjectManager)
            .Include(a => a.UatProject).ThenInclude(p => p!.Consultant)
            .Include(a => a.Results)
            .Include(a => a.Signatures).ThenInclude(s => s.SignedBy)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attempt is null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        var role = (await _userManager.GetRolesAsync(user!)).FirstOrDefault() ?? "";

        var authorized = role switch
        {
            Roles.Admin => true,
            Roles.Consultant => attempt.UatProject!.ConsultantId == user?.Id,
            Roles.ProjectManager => attempt.UatProject!.ProjectManagerId == user?.Id,
            Roles.ContactPerson => attempt.UatProject!.CompanyBranch!.CompanyId == user?.CompanyId,
            _ => false
        };
        if (!authorized) return Forbid();

        var signature = attempt.Signatures.FirstOrDefault(s => s.SignatoryRole == SignatoryRole.CustomerRepresentative);

        var model = new SignoffViewModel
        {
            AttemptId = attempt.Id,
            UatProjectId = attempt.UatProjectId,
            ProjectName = attempt.UatProject!.ProjectName,
            CompanyName = attempt.UatProject.CompanyBranch?.Company?.Name ?? "",
            BranchName = attempt.UatProject.CompanyBranch?.BranchName ?? "",
            TotalItems = attempt.Results.Count,
            PassedCount = attempt.Results.Count(r => r.PassStatus == PassStatus.Pass),
            FailedCount = attempt.Results.Count(r => r.PassStatus == PassStatus.Fail),
            NAItems = attempt.Results.Count(r => r.PassStatus == PassStatus.NA),
            IsCompleted = attempt.UatProject.Status == ProjectStatus.Completed,
            IsSigned = signature is not null,
            SignerName = signature?.SignedBy?.FullName,
            SignedDate = signature?.DateStamped,
            CanCurrentUserSign = signature is null && role == Roles.ContactPerson,
            SignatureImagePath = signature?.OriginalSignatureBlob
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.ContactPerson)]
    public async Task<IActionResult> Sign(int attemptId, string? signatureDataUrl)
    {
        var user = await _userManager.GetUserAsync(User);

        var attempt = await _db.ProjectUatAttempts
            .Include(a => a.UatProject).ThenInclude(p => p!.CompanyBranch).ThenInclude(b => b!.Company)
            .Include(a => a.UatProject).ThenInclude(p => p!.Consultant)
            .Include(a => a.UatProject).ThenInclude(p => p!.ProjectManager)
            .Include(a => a.Signatures)
            .FirstOrDefaultAsync(a => a.Id == attemptId);
        if (attempt is null) return NotFound();
        if (!User.IsInRole(Roles.Admin) && attempt.UatProject!.CompanyBranch!.CompanyId != user?.CompanyId) return Forbid();

        if (attempt.Signatures.Any(s => s.SignatoryRole == SignatoryRole.CustomerRepresentative))
            return RedirectToAction(nameof(Index), new { id = attemptId });

        var hasDrawnSignature = !string.IsNullOrWhiteSpace(signatureDataUrl) && signatureDataUrl.StartsWith("data:image/png;base64,");
        if (!hasDrawnSignature)
        {
            TempData["Error"] = "Please draw your signature before submitting.";
            return RedirectToAction(nameof(Index), new { id = attemptId });
        }

        var base64Data = signatureDataUrl!.Substring("data:image/png;base64,".Length);
        var imageBytes = Convert.FromBase64String(base64Data);
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "signatures");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid()}.png";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, fileName), imageBytes);

        _db.DigitalSignatures.Add(new DigitalSignature
        {
            AttemptId = attemptId,
            SignatoryRole = SignatoryRole.CustomerRepresentative,
            SignedById = user?.Id,
            OriginalSignatureBlob = $"/uploads/signatures/{fileName}",
            DateStamped = DateTime.Now
        });

        attempt.UatProject!.Status = ProjectStatus.Completed;

        if (!await _db.AcceptanceCerts.AnyAsync(c => c.AttemptId == attemptId))
        {
            _db.AcceptanceCerts.Add(new AcceptanceCert
            {
                AttemptId = attemptId,
                GeneratedDate = DateTime.Now,
                PdfFilePath = ""
            });
        }

        if (attempt.UatProject.ProjectManagerId != null)
            TCS.Services.Notifier.NotifyUser(_db, attempt.UatProject.ProjectManagerId,
                $"{attempt.UatProject.CompanyBranch!.Company?.Name} signed and completed {attempt.UatProject.ProjectName}.",
                $"/UatSignoff/Index/{attemptId}");
        if (attempt.UatProject.ConsultantId != null)
            TCS.Services.Notifier.NotifyUser(_db, attempt.UatProject.ConsultantId,
                $"{attempt.UatProject.CompanyBranch!.Company?.Name} signed and completed {attempt.UatProject.ProjectName}. This project is now closed.",
                $"/UatSignoff/Index/{attemptId}");

        await _db.SaveChangesAsync();

        // "Save and update": push the project's Voucher to Definition = Signed.
        if (attempt.UatProject.ExternalVoucherId is not null)
        {
            var createdBy = attempt.UatProject.CreatedById != null ? await _userManager.FindByIdAsync(attempt.UatProject.CreatedById) : null;
            var dto = ExternalMapper.ToVoucher(attempt.UatProject, ConstantCodes.VoucherDefinition_UatSigned,
                attempt.UatProject.Consultant?.ExternalConsigneeId, attempt.UatProject.CompanyBranch?.Company?.ExternalConsigneeId, user?.ExternalUserId ?? 0,
                attempt.UatProject.ProjectManager?.ExternalConsigneeId, createdBy?.ExternalConsigneeId,
                user?.ExternalConsigneeId);  // the signer here IS the Contact Person - deterministic in this one flow
            await _sync.UpdateVoucherAsync(attempt.UatProject.ExternalVoucherId.Value, dto);
        }

        TempData["Success"] = "Signed. This UAT project is now complete.";
        return RedirectToAction(nameof(Index), new { id = attemptId });
    }
}
