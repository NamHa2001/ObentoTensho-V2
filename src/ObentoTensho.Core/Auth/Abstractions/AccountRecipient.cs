namespace ObentoTensho.Core.Auth.Abstractions;

// Người nhận thư tài khoản; IsStaff quyết định liên kết trỏ về trang khách hay trang quản trị
public sealed record AccountRecipient(Guid Id, string Email, string FullName, string Language, bool IsStaff);
