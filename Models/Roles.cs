namespace TCS.Models;

// Unified role set for the merged Training + UAT system.
//
// Merge notes (see /Docs/MERGE-NOTES.md for the full writeup):
//  - Consultant ("Functional Consultant") is shared by both modules: a
//    Consultant sees the Training menu AND the UAT menu.
//  - ContactPerson is the same person/role as the old UAT "Customer
//    Representative" - one role, one login, used by both modules.
//  - CustomerService is new (UAT-only) - reviews/forwards UAT submissions.
public static class Roles
{
    public const string Admin = "Admin";
    public const string Consultant = "Consultant";
    public const string ProjectManager = "ProjectManager";
    public const string TransportManager = "TransportManager";
    public const string ContactPerson = "ContactPerson";
    public const string Trainer = "Trainer";
    public const string CustomerService = "CustomerService";

    public static readonly string[] All =
    {
        Admin, Consultant, ProjectManager, TransportManager, ContactPerson, Trainer, CustomerService
    };

    // Roles that get the Training module's menu items.
    public static readonly string[] TrainingModuleRoles =
    {
        Admin, Consultant, ProjectManager, TransportManager, ContactPerson, Trainer
    };

    // Roles that get the UAT module's menu items.
    public static readonly string[] UatModuleRoles =
    {
        Admin, Consultant, ProjectManager, ContactPerson, CustomerService
    };

    public static string Display(string role) => role switch
    {
        Admin => "System Admin",
        Consultant => "Functional Consultant",
        ProjectManager => "Project Manager",
        TransportManager => "Transport Manager",
        ContactPerson => "Contact Person",
        Trainer => "Trainer",
        CustomerService => "Customer Service",
        _ => role
    };
}
