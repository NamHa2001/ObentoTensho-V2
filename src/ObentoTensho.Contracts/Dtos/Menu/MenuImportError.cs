namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Lỗi của một ô: <c>Column</c> là tiêu đề cột trong mẫu, <c>Code</c> lấy từ <c>FieldErrorCodes</c>.</summary>
public sealed record MenuImportError(string Column, string Code);
