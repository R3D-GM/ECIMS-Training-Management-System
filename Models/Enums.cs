namespace TCS.Models;

public enum SessionStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public enum RequestStatus
{
    Pending,
    Approved,
    Rejected
}

public enum ConfirmationStatus
{
    Pending,
    Confirmed,
    Rejected
}

public enum AttendanceStatus
{
    Present,
    Absent
}

public enum TransportStatus
{
    Assigned,
    Departed,
    Returned,
    Cancelled
}

public enum LocationType
{
    Indoor,
    Outdoor
}

public enum TransportApprovalStatus
{
    PendingApproval,
    Approved,
    Rejected
}

// ---- UAT module enums (ported from the ECIMS/UAT-Checklist system) ----

public enum ProjectStatus
{
    Pending = 0,
    Active = 1,
    AwaitingCustomerReview = 2,
    Declined = 3,
    AwaitingConsultantSignature = 4,
    AwaitingPmSignature = 5,
    Completed = 6,
    DeclinedByCustomerService = 7
}

public enum AttemptOverallStatus
{
    InProgress = 1,
    SubmittedForReview = 2,
    Declined = 3,
    Accepted = 4
}

public enum PassStatus
{
    Pending = 0,
    Pass = 1,
    Fail = 2,
    NA = 3
}

public enum AnswerFormat
{
    PassFailNA = 0,
    PassFailNAWithComment = 1,
    TextInput = 2
}

public enum SignatoryRole
{
    CustomerRepresentative = 1,
    Consultant = 2,
    ProjectManager = 3
}
