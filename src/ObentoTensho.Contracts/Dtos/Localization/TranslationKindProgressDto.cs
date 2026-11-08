namespace ObentoTensho.Contracts.Dtos.Localization;

/// <summary>Đếm theo loại nội dung đang hiển thị cho khách: đã duyệt, lỗi thời, nháp, chưa có bản dịch.</summary>
public sealed record TranslationKindProgressDto(string Kind, int Total, int Approved, int Outdated, int Draft, int Missing);
