namespace ObentoTensho.Contracts.Dtos.Account;

public sealed record UpdateProfileRequest(string FullName, string PhoneNumber, DateOnly? DateOfBirth, string? PreferredLang);
