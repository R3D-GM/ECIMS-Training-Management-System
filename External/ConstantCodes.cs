namespace TCS.External;

// The instructor's SystemConstant table is a generic lookup - every "kind"
// of thing (a voucher type, a role, an activity type...) is a row in that
// table with an int Id, and other objects reference it by that Id.
//
// We don't have his actual Ids yet. Ask him for the SystemConstant rows (or
// an export of that table) for at least these groups, then replace the 0s
// below with the real numbers:
//
//   - Voucher.Type / Voucher.Definition codes for: "Training Session",
//     "UAT Project", "UAT Attempt Submitted", "UAT Signed Certificate"
//   - Role codes for each of our 7 roles (Admin, Consultant, ProjectManager,
//     TransportManager, ContactPerson, Trainer, CustomerService)
//   - ActivityDefinition codes for: "Created", "Updated", "Login"
//   - Consignee.GslType codes for "Company/Business" vs "Person"
//
// Until these are filled in, every sync call sends 0 for these fields,
// which his system will very likely reject - that's expected and fine
// while this is unfinished.
public static class ConstantCodes
{
    // ---- Voucher.Type / Voucher.Definition ----
    public const int VoucherType_TrainingSession = 0;   // TODO: ask instructor
    public const int VoucherType_UatProject = 0;         // TODO: ask instructor
    public const int VoucherDefinition_UatSubmitted = 0; // TODO: ask instructor
    public const int VoucherDefinition_UatSigned = 0;    // TODO: ask instructor

    // ---- Roles (our string role name -> his int Role code) ----
    public static readonly Dictionary<string, int> RoleCodes = new()
    {
        ["Admin"] = 0,            // TODO: ask instructor
        ["Consultant"] = 0,       // TODO: ask instructor
        ["ProjectManager"] = 0,   // TODO: ask instructor
        ["TransportManager"] = 0, // TODO: ask instructor
        ["ContactPerson"] = 0,    // TODO: ask instructor
        ["Trainer"] = 0,          // TODO: ask instructor
        ["CustomerService"] = 0,  // TODO: ask instructor
    };

    // ---- Activity definitions ----
    public const int Activity_Created = 0; // TODO: ask instructor
    public const int Activity_Updated = 0; // TODO: ask instructor
    public const int Activity_Login = 0;   // TODO: ask instructor

    // ---- Consignee.GslType ----
    public const int GslType_Business = 0; // TODO: ask instructor
    public const int GslType_Person = 0;   // TODO: ask instructor
}
