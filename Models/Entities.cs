using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TCS.Models;

public class Company
{
    public int Id { get; set; }

    [Required, Display(Name = "Company Name")]
    public string Name { get; set; } = "";

    public string? Branch { get; set; }
    public string? Address { get; set; }

    [Display(Name = "Contact Person")]
    public string? ContactPersonName { get; set; }

    public string? Phone { get; set; }

    [Display(Name = "TIN")]
    public string? TIN { get; set; }

    // Added when merging in the UAT module - Company doubles as the UAT
    // "Customer" record, so a ContactPerson login also acts as that
    // company's Customer Representative for UAT sign-off.
    [Display(Name = "Contact Email")]
    public string? ContactEmail { get; set; }

    // The Id the instructor's external system assigned to this company as
    // a Consignee, once synced. Null until the first successful sync.
    public int? ExternalConsigneeId { get; set; }

    public ICollection<TrainingSession> TrainingSessions { get; set; } = new List<TrainingSession>();
    public ICollection<CompanyBranch> Branches { get; set; } = new List<CompanyBranch>();
}

// UAT site/branch under a Company - ported from UAT's CustomerBranch.
// A Company can have multiple branches, each with its own UAT projects.
public class CompanyBranch
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required, Display(Name = "Branch Name")]
    public string BranchName { get; set; } = "";

    public string? Address { get; set; }

    [Display(Name = "Site Contact Name")]
    public string? SiteContactName { get; set; }

    [Display(Name = "Site Contact Phone")]
    public string? SiteContactPhone { get; set; }

    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<UatProject> Projects { get; set; } = new List<UatProject>();
}

public class Trainer
{
    public int Id { get; set; }

    [Required, Display(Name = "Trainer Name")]
    public string Name { get; set; } = "";

    public string? Position { get; set; }
    public string? Department { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    // Data URL (base64 PNG) captured once via signature pad, reused as the default trainer signature.
    public string? SignaturePath { get; set; }

    // The Id the instructor's external system assigned to this trainer as
    // a Consignee, once synced.
    public int? ExternalConsigneeId { get; set; }

    public ICollection<TrainingSession> TrainingSessions { get; set; } = new List<TrainingSession>();
}

public class Trainee
{
    public int Id { get; set; }

    public int TrainingSessionId { get; set; }
    public TrainingSession? TrainingSession { get; set; }

    [Required, Display(Name = "Trainee Name")]
    public string Name { get; set; } = "";

    public string? Position { get; set; }
    public string? Phone { get; set; }

    public AttendanceStatus Attendance { get; set; } = AttendanceStatus.Present;

    // The Id the instructor's external system assigned to this trainee as
    // a Consignee, once synced.
    public int? ExternalConsigneeId { get; set; }

    // Data URL (base64 PNG)
    public string? SignaturePath { get; set; }
}

public class TrainingSession
{
    public int Id { get; set; }

    [Required, Display(Name = "Company")]
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Display(Name = "Consultant")]
    public string? ConsultantName { get; set; }

    [Required, Display(Name = "Training Module")]
    public string Module { get; set; } = "";

    [Display(Name = "Training Location")]
    public string? Location { get; set; }

    [Required, Display(Name = "Training Date")]
    [DataType(DataType.Date)]
    public DateTime TrainingDate { get; set; } = DateTime.Today;

    [Display(Name = "Duration (hrs)")]
    [Column(TypeName = "decimal(5,2)")]
    public decimal Duration { get; set; }

    [Display(Name = "Start Time")]
    [DataType(DataType.Time)]
    public TimeSpan? StartTime { get; set; }

    [Display(Name = "End Time")]
    [DataType(DataType.Time)]
    public TimeSpan? EndTime { get; set; }

    [Display(Name = "Trainer")]
    public int? TrainerId { get; set; }
    public Trainer? Trainer { get; set; }

    [Display(Name = "Departure Time")]
    public DateTime? DepartureTime { get; set; }

    [Display(Name = "Return Time")]
    public DateTime? ReturnTime { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    public int AttemptNumber { get; set; } = 1;
    public int? PreviousSessionId { get; set; }

    // The Id the instructor's external system assigned to this training as
    // a Voucher, once synced.
    public int? ExternalVoucherId { get; set; }

    public ICollection<Trainee> Trainees { get; set; } = new List<Trainee>();
    public TrainingConfirmation? Confirmation { get; set; }
}

public class SessionRequest
{
    public int Id { get; set; }

    [Required, Display(Name = "Company")]
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required, Display(Name = "Requested Module")]
    public string RequestedModule { get; set; } = "";

    [Required, Display(Name = "Training Location")]
    public string? Location { get; set; }

    [Required, Display(Name = "Location Type")]
    public LocationType LocationType { get; set; } = LocationType.Indoor;

    // Set once a Project Manager assigns (or a Transport Manager consolidates) a vehicle
    // for this request. Multiple SessionRequests can point at the same TransportAssignment
    // when several departments are going to the same site and get grouped into one vehicle.
    public int? TransportAssignmentId { get; set; }
    public TransportAssignment? TransportAssignment { get; set; }

    [Display(Name = "Requested Date")]
    [DataType(DataType.Date)]
    public DateTime RequestedDate { get; set; } = DateTime.Today;

    [Display(Name = "Requested By")]
    public string? RequestedBy { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    [Display(Name = "Decided By")]
    public string? DecidedBy { get; set; }

    public DateTime? DecidedDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

public class ManagerApproval
{
    public int Id { get; set; }

    [Display(Name = "Manager Name")]
    public string? ManagerName { get; set; }

    public DateTime ApprovalDate { get; set; } = DateTime.Now;
    public string? Notes { get; set; }

    public ICollection<TrainingConfirmation> Confirmations { get; set; } = new List<TrainingConfirmation>();
}

public class TrainingConfirmation
{
    public int Id { get; set; }

    public int TrainingSessionId { get; set; }
    public TrainingSession? TrainingSession { get; set; }

    public int? ManagerApprovalId { get; set; }
    public ManagerApproval? ManagerApproval { get; set; }

    // Data URLs (base64 PNG) captured from signature pads
    public string? TrainerSignaturePath { get; set; }
    public string? ContactPersonSignaturePath { get; set; }

    [Display(Name = "Client Authorized Signatory")]
    public string? ContactPersonName { get; set; }

    public string? Remarks { get; set; }

    public ConfirmationStatus Status { get; set; } = ConfirmationStatus.Pending;

    public DateTime SubmittedDate { get; set; } = DateTime.Now;
    public DateTime? DecidedDate { get; set; }

    public ApprovalPaper? ApprovalPaper { get; set; }
}

public class ApprovalPaper
{
    public int Id { get; set; }

    public int TrainingConfirmationId { get; set; }
    public TrainingConfirmation? TrainingConfirmation { get; set; }

    public DateTime GeneratedDate { get; set; } = DateTime.Now;
    public string? FilePath { get; set; }
}

public class Vehicle
{
    public int Id { get; set; }

    [Required, Display(Name = "Plate Number")]
    public string PlateNumber { get; set; } = "";

    public string? Model { get; set; }
    public int Capacity { get; set; }

    [Display(Name = "Driver Name")]
    public string? DriverName { get; set; }

    [Display(Name = "Driver Phone")]
    public string? DriverPhone { get; set; }

    public string Status { get; set; } = "Available";
}

public class TransportAssignment
{
    public int Id { get; set; }

    // Nullable now: an assignment can exist against one or more SessionRequests before any
    // TrainingSession has been scheduled yet. Still used later for day-of vehicle tracking
    // once a specific session exists.
    [Display(Name = "Training Session")]
    public int? TrainingSessionId { get; set; }
    public TrainingSession? TrainingSession { get; set; }

    [Required, Display(Name = "Vehicle")]
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    // The Session Requests this vehicle covers. Normally just one (Project Manager assigning
    // a single outdoor request), but the Transport Manager can group several requests going
    // to the same site into one TransportAssignment instead of one vehicle per department.
    public List<SessionRequest> SessionRequests { get; set; } = new();

    public TransportApprovalStatus ApprovalStatus { get; set; } = TransportApprovalStatus.PendingApproval;

    [Display(Name = "Assigned By")]
    public string? AssignedByRole { get; set; } // "ProjectManager" or "TransportManager"

    [Display(Name = "Rejection Notes")]
    public string? RejectionNotes { get; set; }

    public DateTime? ApprovedDate { get; set; }

    [Display(Name = "Departure Time")]
    public DateTime? DepartureTime { get; set; }

    [Display(Name = "Return Time")]
    public DateTime? ReturnTime { get; set; }

    public TransportStatus Status { get; set; } = TransportStatus.Assigned;

    public string? Notes { get; set; }
}

// Shared by both modules - a Training notification just leaves UatProjectId
// null, a UAT notification leaves nothing training-specific set.
public class Notification
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string? TargetRole { get; set; }
    public string? Title { get; set; }
    public string Message { get; set; } = "";
    public bool IsRead { get; set; } = false;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? Link { get; set; }

    public int? UatProjectId { get; set; }
    public UatProject? UatProject { get; set; }
}
