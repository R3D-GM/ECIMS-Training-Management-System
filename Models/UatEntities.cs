using System.ComponentModel.DataAnnotations;

namespace TCS.Models;

// ---- UAT module entities ----
// Ported from the standalone UAT-Checklist (ECIMS) system. Renamed
// "Project" -> "UatProject" to keep it unambiguous inside the merged app.
// Every FK that used to point at UAT's own int-keyed User table now points
// at ApplicationUser (string Id), so a single Identity login covers both
// the Training and UAT sides of the system.

public class UatProject
{
    public int Id { get; set; }

    [Required, Display(Name = "Project Name")]
    public string ProjectName { get; set; } = "";

    public int CompanyBranchId { get; set; }
    public CompanyBranch? CompanyBranch { get; set; }

    [Display(Name = "Project Manager")]
    public string? ProjectManagerId { get; set; }
    public ApplicationUser? ProjectManager { get; set; }

    [Display(Name = "Consultant")]
    public string? ConsultantId { get; set; }
    public ApplicationUser? Consultant { get; set; }

    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Pending;

    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? PmReceivedDate { get; set; }
    public DateTime? SentToCustomerServiceDate { get; set; }
    public string? SentToCustomerServiceById { get; set; }
    public string? CsDeclineComment { get; set; }
    public DateTime? CsDeclineDate { get; set; }

    // The Id the instructor's external system assigned to this project as
    // a Voucher, once synced.
    public int? ExternalVoucherId { get; set; }

    public ICollection<ProjectUatAttempt> Attempts { get; set; } = new List<ProjectUatAttempt>();
}

// The reusable question bank - shared across every UAT project. Managed by
// Admin under Settings, same pattern as everything else in the app.
public class UatSection
{
    public int Id { get; set; }

    [Required, Display(Name = "Section Name")]
    public string SectionName { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public ICollection<UatMasterItem> MasterItems { get; set; } = new List<UatMasterItem>();
}

public class UatMasterItem
{
    public int Id { get; set; }

    public int SectionId { get; set; }
    public UatSection? Section { get; set; }

    [Required, Display(Name = "Test Description")]
    public string TestDescription { get; set; } = "";

    public AnswerFormat AnswerFormat { get; set; } = AnswerFormat.PassFailNA;
    public string? Options { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

// One "attempt" = one pass through the whole question bank for a project.
// A project can have several attempts if a prior one gets declined.
public class ProjectUatAttempt
{
    public int Id { get; set; }

    public int UatProjectId { get; set; }
    public UatProject? UatProject { get; set; }

    public int AttemptNumber { get; set; } = 1;

    public DateTime StartedDate { get; set; } = DateTime.Now;
    public DateTime? SubmittedDate { get; set; }
    public DateTime? DecidedDate { get; set; }

    public string? DecidedById { get; set; }
    public ApplicationUser? DecidedBy { get; set; }

    public AttemptOverallStatus OverallStatus { get; set; } = AttemptOverallStatus.InProgress;

    public string? InitiatedById { get; set; }
    public ApplicationUser? InitiatedBy { get; set; }

    // The Id the instructor's external system assigned to this attempt as
    // a Voucher (submitted / signed), once synced.
    public int? ExternalVoucherId { get; set; }

    public ICollection<ProjectUatResult> Results { get; set; } = new List<ProjectUatResult>();
    public ICollection<DigitalSignature> Signatures { get; set; } = new List<DigitalSignature>();
    public AcceptanceCert? Certificate { get; set; }
}

// One answered row (one master item, answered for one attempt).
public class ProjectUatResult
{
    public int Id { get; set; }

    public int AttemptId { get; set; }
    public ProjectUatAttempt? Attempt { get; set; }

    public int MasterItemId { get; set; }
    public UatMasterItem? MasterItem { get; set; }

    public PassStatus PassStatus { get; set; }
    public string? Comment { get; set; }
    public string? EvidencePath { get; set; }

    public string? ExecutedById { get; set; }
    public ApplicationUser? ExecutedBy { get; set; }
    public DateTime ExecutedDate { get; set; } = DateTime.Now;

    public string? LastModifiedById { get; set; }
    public ApplicationUser? LastModifiedBy { get; set; }
    public SignatoryRole? LastModifiedByRole { get; set; }

    public bool IsFlagged { get; set; }

    public ICollection<ProjectUatResultHistory> History { get; set; } = new List<ProjectUatResultHistory>();
}

// Audit trail whenever a result gets edited after the fact.
public class ProjectUatResultHistory
{
    public int Id { get; set; }

    public int ResultId { get; set; }
    public ProjectUatResult? Result { get; set; }

    public PassStatus PreEditPassStatus { get; set; }
    public PassStatus PostEditPassStatus { get; set; }
    public string EditComment { get; set; } = "";

    public string? EditedById { get; set; }
    public ApplicationUser? EditedBy { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.Now;
}

// The final signed-off acceptance certificate PDF for an attempt.
public class AcceptanceCert
{
    public int Id { get; set; }

    public int AttemptId { get; set; }
    public ProjectUatAttempt? Attempt { get; set; }

    public DateTime GeneratedDate { get; set; } = DateTime.Now;
    public string PdfFilePath { get; set; } = "";
}

public class DigitalSignature
{
    public int Id { get; set; }

    public int AttemptId { get; set; }
    public ProjectUatAttempt? Attempt { get; set; }

    public SignatoryRole SignatoryRole { get; set; }

    public string? SignedById { get; set; }
    public ApplicationUser? SignedBy { get; set; }

    // Data URL (base64 PNG) - same signature-pad approach TCS already uses
    // for trainer/contact-person signatures.
    public string OriginalSignatureBlob { get; set; } = "";

    public DateTime DateStamped { get; set; } = DateTime.Now;
    public string? UploadedFilePath { get; set; }
}

// Company stamp image used when generating acceptance certificate PDFs.
public class CompanyStampAsset
{
    public int Id { get; set; }

    public string ImagePath { get; set; } = "";
    public string? UploadedById { get; set; }
    public DateTime UploadedDate { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;
}
