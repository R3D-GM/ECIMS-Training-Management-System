namespace TCS.External;

// Bound from appsettings.json "ExternalSystem" section. Ask your instructor
// for these three things and this whole integration is ready to go:
//   1. BaseUrl - the root URL of his system's API
//   2. The endpoint path for each object (Consignee, User, UserRoleMapper,
//      Voucher, Activity, SystemConstant) - defaults below are guesses,
//      change them to whatever he actually gave you
//   3. How to authenticate the call (ApiKey below is a placeholder - it
//      might instead be a Bearer token, Basic auth, or nothing at all)
public class ExternalSystemOptions
{
    public const string SectionName = "ExternalSystem";

    // Leave blank to disable syncing entirely (nothing will be sent, and
    // nothing will break - every sync call fails silently and just logs).
    public string BaseUrl { get; set; } = "";

    public string? ApiKey { get; set; }

    public string ConsigneeEndpoint { get; set; } = "/api/consignees";
    public string UserEndpoint { get; set; } = "/api/users";
    public string UserRoleMapperEndpoint { get; set; } = "/api/user-role-mappers";
    public string VoucherEndpoint { get; set; } = "/api/vouchers";
    public string ActivityEndpoint { get; set; } = "/api/activities";
    public string SystemConstantEndpoint { get; set; } = "/api/system-constants";
}
