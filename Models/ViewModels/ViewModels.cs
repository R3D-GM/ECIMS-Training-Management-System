using System.ComponentModel.DataAnnotations;

namespace TCS.Models.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class ResetPasswordViewModel
{
    [Required]
    public string UserId { get; set; } = "";

    [Required]
    public string Token { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "New Password")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Confirm New Password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

public class UserFormViewModel
{
    public string? Id { get; set; }

    [Required, Display(Name = "Full Name")]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Required, Display(Name = "Role")]
    public string Role { get; set; } = "";

    public int? TrainerId { get; set; }
    public int? CompanyId { get; set; }

    public bool IsEdit { get; set; }
}

public class UserListItemViewModel
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

public class ProfileSettingsViewModel
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

public class TrainingAssignmentViewModel
{
    public int OnTrainingNow { get; set; }
    public int DepartureToday { get; set; }
    public int ReturningToday { get; set; }
    public int UpcomingAssignments { get; set; }
    public List<TrainingSession> ActiveAssignments { get; set; } = new();
}

public class ModuleCount
{
    public string Module { get; set; } = "";
    public int Count { get; set; }
}

public class CompanyCount
{
    public string Company { get; set; } = "";
    public int Count { get; set; }
}

public class ReportsViewModel
{
    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int ScheduledSessions { get; set; }
    public int CancelledSessions { get; set; }

    public int PendingConfirmations { get; set; }
    public int ConfirmedConfirmations { get; set; }
    public int RejectedConfirmations { get; set; }

    public decimal TotalTrainingHours { get; set; }

    public List<ModuleCount> SessionsByModule { get; set; } = new();
    public List<CompanyCount> SessionsByCompany { get; set; } = new();

    public int TransportAssignments { get; set; }
    public int TransportCompleted { get; set; }
}

public class DashboardViewModel
{
    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int UpcomingSessions { get; set; }
    public int PendingConfirmations { get; set; }
    public int PendingSessionRequests { get; set; }
    public int PendingManagerApprovals { get; set; }
    public int TotalTrainers { get; set; }
    public int TotalCompanies { get; set; }
    public int TotalTransportAssignments { get; set; }
    public List<TrainingSession> UpcomingList { get; set; } = new();
    public List<TrainingConfirmation> RecentConfirmations { get; set; } = new();

    // ---- UAT tab ----
    public int TotalUatProjects { get; set; }
    public int UatInReview { get; set; }
    public int UatCompleted { get; set; }
    public int CompaniesReadyForUat { get; set; }
    public List<UatProject> RecentUatProjects { get; set; } = new();
    public List<CompanyUatReadinessViewModel> CompanyReadiness { get; set; } = new();
}

// One row per company on the UAT tab: whether Training has cleared the way
// for UAT yet, and how many trainings are still blocking it if not.
public class CompanyUatReadinessViewModel
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public bool IsReady { get; set; }
    public int PendingTrainings { get; set; }
    public int UatProjectCount { get; set; }
}

// ---- UAT checklist execution (ported from AttemptExecuteViewModel.cs) ----

public class AttemptExecuteViewModel
{
    public string LockBannerCssClass { get; set; } = "";
    public bool IsReadOnly { get; set; }
    public string StatusLabel { get; set; } = "";
    public string StatusCssClass { get; set; } = "";
    public string LockBannerText { get; set; } = "";
    public int AttemptId { get; set; }
    public int UatProjectId { get; set; }
    public int AttemptNumber { get; set; }
    public string ProjectName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string? ProjectManagerName { get; set; }
    public DateTime StartDate { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public DateTime? SentDate { get; set; }
    public List<ChecklistSectionGroup> Sections { get; set; } = new();
}

public class ChecklistSectionGroup
{
    public string SectionName { get; set; } = "";
    public List<ChecklistItemInput> Items { get; set; } = new();
}

public class ChecklistItemInput
{
    public bool WasChanged { get; set; }
    public string? CommentAuthorName { get; set; }
    public int ResultId { get; set; }
    public string TestDescription { get; set; } = "";
    public PassStatus PassStatus { get; set; }
    public string? Comment { get; set; }
    public string? EvidencePath { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? EvidenceFile { get; set; }
    public bool IsFlagged { get; set; }
    public bool ResolveFlag { get; set; }
}

// ---- Customer decide / accept / decline / sign-off ----

public class CustomerDecideViewModel
{
    public int AttemptId { get; set; }
    public string CompanyName { get; set; } = "";
    public string BranchName { get; set; } = "";
    public int TotalItems { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int NAItems { get; set; }
    public List<CustomerChangeRow> Changes { get; set; } = new();
}

public class CustomerChangeRow
{
    public string TestDescription { get; set; } = "";
    public bool IsResultChanged { get; set; }
    public string? ChangeLabel { get; set; }
    public bool IsFlagged { get; set; }
    public string? Comment { get; set; }
}

public class SignoffViewModel
{
    public int AttemptId { get; set; }
    public int UatProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string BranchName { get; set; } = "";
    public int TotalItems { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int NAItems { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsSigned { get; set; }
    public string? SignerName { get; set; }
    public DateTime? SignedDate { get; set; }
    public bool CanCurrentUserSign { get; set; }
    public string? SignatureImagePath { get; set; }
}

// ---- Customer Service (forwarded projects, view-only oversight) ----

public class CsProjectListItem
{
    public int UatProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string? ConsultantName { get; set; }
    public string? ProjectManagerName { get; set; }
    public DateTime ReceivedDate { get; set; }
    public string StatusLabel { get; set; } = "";
    public string StatusCssClass { get; set; } = "";
}
