using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;
using TCS.Models.ViewModels;
using TCS.Services;

namespace TCS.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = await _userManager.GetRolesAsync(user!);
        var role = roles.FirstOrDefault() ?? "";
        ViewBag.Role = role;
        ViewBag.CurrentUser = user;

        IQueryable<TrainingSession> sessions = _db.TrainingSessions.Include(s => s.Company).Include(s => s.Trainer);
        IQueryable<TrainingConfirmation> confirmations = _db.TrainingConfirmations.Include(c => c.TrainingSession).ThenInclude(s => s!.Company);

        // Scope data down for the two "personal" roles
        if (role == Roles.Trainer && user?.TrainerId != null)
        {
            sessions = sessions.Where(s => s.TrainerId == user.TrainerId);
            confirmations = confirmations.Where(c => c.TrainingSession!.TrainerId == user.TrainerId);
        }
        else if (role == Roles.ContactPerson && user?.CompanyId != null)
        {
            sessions = sessions.Where(s => s.CompanyId == user.CompanyId);
            confirmations = confirmations.Where(c => c.TrainingSession!.CompanyId == user.CompanyId);
        }

        var vm = new DashboardViewModel
        {
            TotalSessions = await sessions.CountAsync(),
            CompletedSessions = await sessions.CountAsync(s => s.Status == SessionStatus.Completed),
            UpcomingSessions = await sessions.CountAsync(s => s.TrainingDate >= DateTime.Today && s.Status == SessionStatus.Scheduled),
            PendingConfirmations = await confirmations.CountAsync(c => c.Status == ConfirmationStatus.Pending),
            PendingSessionRequests = await _db.SessionRequests.CountAsync(r => r.Status == RequestStatus.Pending),
            PendingManagerApprovals = await confirmations.CountAsync(c => c.Status == ConfirmationStatus.Confirmed && c.ManagerApprovalId == null),
            TotalTrainers = await _db.Trainers.CountAsync(),
            TotalCompanies = await _db.Companies.CountAsync(),
            TotalTransportAssignments = await _db.TransportAssignments.CountAsync(),
            UpcomingList = await sessions.Where(s => s.TrainingDate >= DateTime.Today).OrderBy(s => s.TrainingDate).Take(5).ToListAsync(),
            RecentConfirmations = await confirmations.OrderByDescending(c => c.SubmittedDate).Take(5).ToListAsync()
        };

        // ---- UAT tab ----
        if (Roles.UatModuleRoles.Contains(role))
        {
            IQueryable<UatProject> uatProjects = _db.UatProjects
                .Include(p => p.CompanyBranch).ThenInclude(b => b!.Company);

            if (role == Roles.ContactPerson && user?.CompanyId != null)
                uatProjects = uatProjects.Where(p => p.CompanyBranch!.CompanyId == user.CompanyId);
            else if (role == Roles.CustomerService)
                uatProjects = uatProjects.Where(p => p.SentToCustomerServiceDate != null);

            vm.TotalUatProjects = await uatProjects.CountAsync();
            vm.UatInReview = await uatProjects.CountAsync(p => p.Status == ProjectStatus.AwaitingCustomerReview
                || p.Status == ProjectStatus.AwaitingConsultantSignature
                || p.Status == ProjectStatus.AwaitingPmSignature);
            vm.UatCompleted = await uatProjects.CountAsync(p => p.Status == ProjectStatus.Completed);
            vm.RecentUatProjects = await uatProjects.OrderByDescending(p => p.CreatedAt).Take(5).ToListAsync();

            var companies = role == Roles.ContactPerson && user?.CompanyId != null
                ? await _db.Companies.Where(c => c.Id == user.CompanyId).ToListAsync()
                : await _db.Companies.ToListAsync();

            foreach (var c in companies)
            {
                var ready = await UatWorkflow.IsCompanyReadyForUatAsync(_db, c.Id);
                vm.CompanyReadiness.Add(new CompanyUatReadinessViewModel
                {
                    CompanyId = c.Id,
                    CompanyName = c.Name,
                    IsReady = ready,
                    PendingTrainings = ready ? 0 : await UatWorkflow.PendingTrainingCountAsync(_db, c.Id),
                    UatProjectCount = await _db.UatProjects.CountAsync(p => p.CompanyBranch!.CompanyId == c.Id)
                });
            }
            vm.CompaniesReadyForUat = vm.CompanyReadiness.Count(c => c.IsReady);
        }

        return View(vm);
    }

    public IActionResult Error() => View();
}
