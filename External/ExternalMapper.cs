using TCS.Models;

namespace TCS.External;

public static class ExternalMapper
{
    // A Company becomes a business Consignee.
    public static ConsigneeDTO ToConsignee(Company company) => new()
    {
        Id = company.Id,
        Code = company.TIN,
        GslType = ConstantCodes.GslType_Business,
        Tin = company.TIN,
        IsPerson = false,
        FirstName = company.Name,
        Note = company.Address,
        IsActive = true,
        CreatedOn = DateTime.Now,
        LastModified = DateTime.Now
    };

    // An ApplicationUser (any role) becomes a person Consignee.
    public static ConsigneeDTO ToConsignee(ApplicationUser user)
    {        var nameParts = (user.FullName ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return new ConsigneeDTO
        {
            Id = 0, // his system assigns its own Id on create - leave 0
            GslType = ConstantCodes.GslType_Person,
            IsPerson = true,
            FirstName = nameParts.Length > 0 ? nameParts[0] : user.FullName ?? "",
            SecondName = nameParts.Length > 1 ? nameParts[1] : null,
            IsActive = true,
            CreatedOn = DateTime.Now,
            LastModified = DateTime.Now
        };
    }

    // A Trainer roster entry becomes a person Consignee (used even before
    // that trainer has a login account).
    public static ConsigneeDTO ToConsignee(Trainer trainer)
    {
        var nameParts = trainer.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return new ConsigneeDTO
        {
            Id = 0,
            GslType = ConstantCodes.GslType_Person,
            IsPerson = true,
            FirstName = nameParts.Length > 0 ? nameParts[0] : trainer.Name,
            SecondName = nameParts.Length > 1 ? nameParts[1] : null,
            IsActive = true,
            CreatedOn = DateTime.Now,
            LastModified = DateTime.Now
        };
    }

    // A Trainee (added under a training session) becomes a person Consignee.
    public static ConsigneeDTO ToConsignee(Trainee trainee)
    {
        var nameParts = trainee.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return new ConsigneeDTO
        {
            Id = 0,
            GslType = ConstantCodes.GslType_Person,
            IsPerson = true,
            FirstName = nameParts.Length > 0 ? nameParts[0] : trainee.Name,
            SecondName = nameParts.Length > 1 ? nameParts[1] : null,
            IsActive = true,
            CreatedOn = DateTime.Now,
            LastModified = DateTime.Now
        };
    }

    // A login account, paired with the Consignee.Id his system returned
    // when you synced the person above.
    public static UserDTO ToUser(ApplicationUser user, int consigneeId) => new()
    {
        Id = 0,
        Person = consigneeId,
        UserName = user.Email ?? user.UserName ?? "",
        // NEVER send our real password hash - his system should issue its
        // own credential (e.g. a reset link) rather than receive one from us.
        Password = "",
        Salt = "",
        IsActive = true,
        CreatedAt = DateTime.Now
    };

    // Our role name -> his UserRoleMapper row. userId/roleId are the Ids
    // HIS system gave back after you synced the User and looked up the role
    // in ConstantCodes.RoleCodes.
    public static UserRoleMapperDTO ToUserRoleMapper(int userId, string ourRoleName)
    {
        ConstantCodes.RoleCodes.TryGetValue(ourRoleName, out var roleCode);
        return new UserRoleMapperDTO
        {
            Id = 0,
            User = userId,
            Role = roleCode,
            ExpiryDate = DateTime.Now.AddYears(10)
        };
    }

    // A generic "something happened" record - used for both Training and
    // UAT events. voucherTypeCode/definitionCode come from ConstantCodes.
    public static VoucherDTO ToVoucher(int referenceId, string code, int typeCode, int definitionCode, int lastUserExternalId) => new()
    {
        Id = 0,
        Code = code,
        Type = typeCode,
        Definition = definitionCode,
        IssuedDate = DateTime.Now,
        IsIssued = true,
        CreatedOn = DateTime.Now,
        LastModified = DateTime.Now,
        LastUser = lastUserExternalId,
        LastState = 1
    };

    // A Training Session becomes a Voucher (Type = TrainingSession). The
    // Trainer and Company must already be synced (have ExternalConsigneeId)
    // for Consignee1/2 to be meaningful - if either is null, we still send
    // the voucher but those fields come through as null.
    // Consignee3-6 = Trainees (as their own Consignee records, synced
    // separately via ToConsignee(Trainee) - confirmed with the instructor:
    // Trainer and Trainee both go in as Consignees on the Training voucher).
    // Only the first 4 trainees fit; if a session has more, the rest won't
    // appear on the voucher itself (still synced as their own Consignee
    // records, just not attached to this voucher).
    public static VoucherDTO ToVoucher(TrainingSession session, int? trainerConsigneeId, int? companyConsigneeId, int lastUserExternalId, IReadOnlyList<int>? traineeConsigneeIds = null)
    {
        var t = traineeConsigneeIds ?? Array.Empty<int>();
        return new()
        {
            Id = 0,
            Code = $"TRN-{session.Id}",
            Type = ConstantCodes.VoucherType_TrainingSession,
            Definition = ConstantCodes.VoucherType_TrainingSession,
            Consignee1 = trainerConsigneeId,   // the trainer
            Consignee2 = companyConsigneeId,   // the company being trained
            Consignee3 = t.Count > 0 ? t[0] : null,
            Consignee4 = t.Count > 1 ? t[1] : null,
            Consignee5 = t.Count > 2 ? t[2] : null,
            Consignee6 = t.Count > 3 ? t[3] : null,
            IssuedDate = session.TrainingDate,
            StartDate = session.TrainingDate,
            IsIssued = true,
            CreatedOn = DateTime.Now,
            LastModified = DateTime.Now,
            LastUser = lastUserExternalId,
            LastState = (int)session.Status,
            Note = session.Module
        };
    }

    // A UAT project becomes a Voucher (Type = UatProject). Definition
    // changes as the project moves through its lifecycle - pass the right
    // ConstantCodes value in (e.g. Submitted vs Signed) when you update it.
    // NOTE: slots 3-6 below are our best-guess assignment, not confirmed by
    // the instructor - flag this before it hits production.
    // Consignee3 = Project Manager.
    // Consignee4 = whoever created the project (always set, deterministic).
    // Consignee5 = Contact Person - only ever passed from the Sign-off flow,
    // where the signer IS a specific, known Contact Person. Everywhere else
    // (create, submit) a Company can have multiple Contact Person logins
    // with no single correct one, so callers pass null there rather than
    // us guessing.
    public static VoucherDTO ToVoucher(UatProject project, int definitionCode, int? consultantConsigneeId, int? companyConsigneeId, int lastUserExternalId, int? projectManagerConsigneeId = null, int? createdByConsigneeId = null, int? contactPersonConsigneeId = null) => new()
    {
        Id = 0,
        Code = $"UAT-{project.Id}",
        Type = ConstantCodes.VoucherType_UatProject,
        Definition = definitionCode,
        Consignee1 = consultantConsigneeId,
        Consignee2 = companyConsigneeId,
        Consignee3 = projectManagerConsigneeId,
        Consignee4 = createdByConsigneeId,
        Consignee5 = contactPersonConsigneeId,
        IssuedDate = DateTime.Now,
        StartDate = project.StartDate,
        EndDate = project.EndDate,
        IsIssued = true,
        CreatedOn = DateTime.Now,
        LastModified = DateTime.Now,
        LastUser = lastUserExternalId,
        LastState = (int)project.Status,
        Note = project.ProjectName
    };

    public static ActivityDTO ToActivity(int referenceId, int activityDefinitionCode, int userExternalId) => new()
    {
        Id = 0,
        Pointer = referenceId,
        Reference = referenceId,
        ActivityDefinition = activityDefinitionCode,
        TimeStamp = DateTime.Now,
        Platform = "TCS-Web",
        User = userExternalId,
        Day = DateTime.Now.Day,
        Month = DateTime.Now.Month,
        Year = DateTime.Now.Year
    };
}
