namespace ObentoTensho.Core.Auth.Abstractions;

// Mã tạm giữa các bước đăng nhập nhân viên: được ký và mã hóa, không lưu CSDL
public interface IStaffChallengeTokens
{
    string Protect(StaffChallenge challenge);

    // Null khi mã bị sửa, sai khóa hoặc sai định dạng
    StaffChallenge? Unprotect(string token);
}
