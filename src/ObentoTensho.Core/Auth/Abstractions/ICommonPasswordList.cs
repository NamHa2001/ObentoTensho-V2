namespace ObentoTensho.Core.Auth.Abstractions;

// BR-AUTH-01: danh sách mật khẩu phổ biến/đã lộ
public interface ICommonPasswordList
{
    bool Contains(string password);
}
