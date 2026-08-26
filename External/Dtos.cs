namespace TCS.External;

// ---- Exact DTOs as given by the instructor - do not rename fields, his
// system deserializes by name. If he ever sends an updated version of
// these, replace this whole file with the new version. ----

public class VoucherDTO
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int Type { get; set; }
    public int Definition { get; set; }
    public int? OriginConsigneeUnit { get; set; }
    public int? DestinationConsigneeUnit { get; set; }
    public int? Period { get; set; }
    public int? Shift { get; set; }
    public int? Consignee1 { get; set; }
    public int? Consignee2 { get; set; }
    public int? Consignee3 { get; set; }
    public int? Consignee4 { get; set; }
    public int? Consignee5 { get; set; }
    public int? Consignee6 { get; set; }
    public int? ConsigneeUnit1 { get; set; }
    public int? ConsigneeUnit2 { get; set; }
    public int? ConsigneeUnit3 { get; set; }
    public int? ConsigneeUnit4 { get; set; }
    public int? ConsigneeUnit5 { get; set; }
    public int? ConsigneeUnit6 { get; set; }
    public int? Article { get; set; }
    public DateTime IssuedDate { get; set; }
    public bool IsIssued { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsVoid { get; set; }
    public int? Day { get; set; }
    public int? Month { get; set; }
    public int? Year { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal AddCharge { get; set; }
    public decimal GrandTotal { get; set; }
    public int? PaymentMethod { get; set; }
    public int? PaymentProcessor { get; set; }
    public int? Payer { get; set; }
    public bool? IsIncoming { get; set; }
    public decimal? PaymentAmount { get; set; }
    public DateTime? PaymentIssueDate { get; set; }
    public DateTime? PaymentMaturityDate { get; set; }
    public string? PaymentRefNumber { get; set; }
    public int? PaymentStatus { get; set; }
    public int? Currency { get; set; }
    public decimal? ExchangeRate { get; set; }
    public decimal? Tender { get; set; }
    public string? Note { get; set; }
    public int? Purpose { get; set; }
    public string? FsNumber { get; set; }
    public string? Mrc { get; set; }
    public int? Cart { get; set; }
    public string? Extension1 { get; set; }
    public string? Extension2 { get; set; }
    public string? Extension3 { get; set; }
    public string? Extension4 { get; set; }
    public string? Extension5 { get; set; }
    public string? Extension6 { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? SourceStore { get; set; }
    public int? DestinationStore { get; set; }
    public bool? HasEffect { get; set; }
    public int? SourceBankAccount { get; set; }
    public int? DestinationBankAccount { get; set; }
    public int? LastActivity { get; set; }
    public int? DeliveryMethod { get; set; }
    public int? Count { get; set; }
    public string? Space { get; set; }
    public int? ContactPerson { get; set; }
    public int LastUser { get; set; }
    public int? LastDevice { get; set; }
    public int LastState { get; set; }
    public decimal? Latitiude { get; set; }
    public decimal? Longitude { get; set; }
    public bool? Locked { get; set; }
    public string? DefaultImageUrl { get; set; }
    public string? Remark { get; set; }
}

public class ActivityDTO
{
    public int Id { get; set; }
    public int Pointer { get; set; }
    public int Reference { get; set; }
    public int ActivityDefinition { get; set; }
    public DateTime TimeStamp { get; set; }
    public int? Period { get; set; }
    public int? ConsigneeUnit { get; set; }
    public int? Device { get; set; }
    public string Platform { get; set; } = "";
    public string? IpAdress { get; set; }
    public int User { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? Latitude { get; set; }
    public int Day { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string? Remark { get; set; }
}

public class SystemConstantDTO
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public int? Index { get; set; }
    public string Description { get; set; } = "";
    public string? Category { get; set; }
    public string? Value { get; set; }
    public int? ParentId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int? NavType { get; set; }
    public string? Remark { get; set; }
}

public class ConsigneeDTO
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public int GslType { get; set; }
    public string? Tin { get; set; }
    public string? BioId { get; set; }
    public string? NationalId { get; set; }
    public string? PassportId { get; set; }
    public bool IsPerson { get; set; }
    public int? Title { get; set; }
    public string FirstName { get; set; } = "";
    public string? SecondName { get; set; }
    public string? ThirdName { get; set; }
    public int? Gender { get; set; }
    public int? BusinessType { get; set; }
    public int Preference { get; set; }
    public DateTime? StartDate { get; set; }
    public int? Nationality { get; set; }
    public bool IsActive { get; set; }
    public int? MaritalStatus { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastModified { get; set; }
    public int? MainConsigneeUnit { get; set; }
    public string? BaseUrl { get; set; }
    public int? ParentId { get; set; }
    public int? Department { get; set; }
    public int? Branch { get; set; }
    public int? Position { get; set; }
    public int? CommunicationSource { get; set; }
    public int? DefaultLanguage { get; set; }
    public int? DefaultCurrency { get; set; }
    public string? DefaultImageUrl { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? TransactionLimit { get; set; }
    public bool Locked { get; set; }
    public string? Remark { get; set; }
}

public class UserDTO
{
    public int Id { get; set; }
    public int Person { get; set; }
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string Salt { get; set; } = "";
    public int? LoggedInStatus { get; set; }
    public bool IsActive { get; set; }
    public DateTime? FirstLoginAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? Remark { get; set; }
}

public class UserRoleMapperDTO
{
    public int Id { get; set; }
    public int Role { get; set; }
    public int User { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string? Remark { get; set; }
}
