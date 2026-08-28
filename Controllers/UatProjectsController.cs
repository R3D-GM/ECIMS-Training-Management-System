using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.External;
using TCS.Models;
using TCS.Services;

namespace TCS.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.ProjectManager + "," + Roles.ContactPerson + "," + Roles.CustomerService)]
public class UatProjectsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ExternalSyncClient _sync;

    public UatProjectsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ExternalSyncClient sync)
    {
        _db = db;
        _userManager = userManager;
        _sync = sync;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = await _userManager.GetRolesAsync(user!);
        var role = roles.FirstOrDefault() ?? "";

        var query = _db.UatProjects
            .Include(p => p.CompanyBranch).ThenInclude(b => b!.Company)
            .Include(p => p.Consultant)
            .Include(p => p.ProjectManager)
            .AsQueryable();

        if (role == Roles.ContactPerson && user?.CompanyId != null)
            query = query.Where(p => p.CompanyBranch!.CompanyId == user.CompanyId);
        else if (role == Roles.CustomerService)
            query = query.Where(p => p.SentToCustomerServiceDate != null);

        ViewBag.Role = role;
        return View(await query.OrderByDescending(p => p.CreatedAt).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var project = await _db.UatProjects
            .Include(p => p.CompanyBranch).ThenInclude(b => b!.Company)
            .Include(p => p.Consultant)
            .Include(p => p.ProjectManager)
            .Include(p => p.Attempts)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return NotFound();

        if (User.IsInRole(Roles.ContactPerson) && !User.IsInRole(Roles.Admin))
        {
            var user = await _userManager.GetUserAsync(User);
            if (project.CompanyBranch!.CompanyId != user?.CompanyId) return Forbid();
        }
        if (User.IsInRole(Roles.CustomerService) && !User.IsInRole(Roles.Admin) && project.SentToCustomerServiceDate is null)
        {
            return Forbid();
        }

        return View(project);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.ProjectManager)]
    public async Task<IActionResult> Create(int? companyId)
    {
        // A UAT project can only be opened for a company once every training
        // booked for it has finished - see UatWorkflow.
        var companies = new List<Company>();
        foreach (var c in await _db.Companies.Include(c => c.Branches).ToListAsync())
        {
            if (await UatWorkflow.IsCompanyReadyForUatAsync(_db, c.Id))
                companies.Add(c);
        }

        ViewBag.Companies = companies;
        ViewBag.SelectedCompanyId = companyId;
        ViewBag.ProjectManagers = await _userManager.GetUsersInRoleAsync(Roles.ProjectManager);
        ViewBag.Consultants = await _userManager.GetUsersInRoleAsync(Roles.Consultant);

        if (!companies.Any())
        {
            TempData["Error"] = "No company is ready for UAT yet - every training for a company must be completed first.";
        }

        return View(new UatProject { StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(30) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.ProjectManager)]
    public async Task<IActionResult> Create(UatProject model, int companyId)
    {
        var branch = await _db.CompanyBranches.Include(b => b.Company).FirstOrDefaultAsync(b => b.CompanyId == companyId);
        if (branch == null || !await UatWorkflow.IsCompanyReadyForUatAsync(_db, companyId))
        {
            ModelState.AddModelError("", "This company is not ready for UAT yet - finish its trainings first.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Companies = await _db.Companies.ToListAsync();
            ViewBag.SelectedCompanyId = companyId;
            ViewBag.ProjectManagers = await _userManager.GetUsersInRoleAsync(Roles.ProjectManager);
            ViewBag.Consultants = await _userManager.GetUsersInRoleAsync(Roles.Consultant);
            return View(model);
        }

        model.CompanyBranchId = branch!.Id;
        var user = await _userManager.GetUserAsync(User);
        model.CreatedById = user?.Id;
        model.Status = ProjectStatus.Active;
        _db.UatProjects.Add(model);
        await _db.SaveChangesAsync();

        // Mirror this UAT project as a Voucher (Type = UatProject).
        var consultant = model.ConsultantId != null ? await _userManager.FindByIdAsync(model.ConsultantId) : null;
        var projectManager = model.ProjectManagerId != null ? await _userManager.FindByIdAsync(model.ProjectManagerId) : null;
        var contactPerson = await _userManager.Users.FirstOrDefaultAsync(u => u.CompanyId == branch.CompanyId);
        var voucherDto = ExternalMapper.ToVoucher(model, ConstantCodes.VoucherType_UatProject, consultant?.ExternalConsigneeId,
            branch.Company?.ExternalConsigneeId, contactPerson?.ExternalConsigneeId, projectManager?.ExternalConsigneeId,
            user?.ExternalConsigneeId, user?.ExternalUserId ?? 0);
        var voucherId = await _sync.SyncVoucherAndGetIdAsync(voucherDto);
        if (voucherId is not null)
        {
            model.ExternalVoucherId = voucherId;
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "UAT project created.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // Consultant/PM forwards a signed-off project to Customer Service for
    // final oversight/archival - a visibility step, not an approval gate.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Consultant + "," + Roles.ProjectManager)]
    public async Task<IActionResult> SendToCustomerService(int id)
    {
        var project = await _db.UatProjects.FindAsync(id);
        if (project == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        project.SentToCustomerServiceDate = DateTime.Now;
        project.SentToCustomerServiceById = user?.Id;

        TCS.Services.Notifier.NotifyRole(_db, Roles.CustomerService,
            $"{project.ProjectName} was sent to Customer Service for final review.",
            $"/UatProjects/Details/{id}");

        await _db.SaveChangesAsync();
        TempData["Success"] = "Sent to Customer Service.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
