using Microsoft.AspNetCore.Identity;

namespace TCS.Models;

// Extends Identity's user so that a login account can optionally be linked
// to a Trainer reference record (for the Trainer role) or a Company
// (for the Contact Person role).
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";

    public int? TrainerId { get; set; }
    public Trainer? Trainer { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    // Ids the instructor's external system assigned once this login was
    // synced (as a Consignee, then as a User). Null until first sync.
    public int? ExternalConsigneeId { get; set; }
    public int? ExternalUserId { get; set; }
}
