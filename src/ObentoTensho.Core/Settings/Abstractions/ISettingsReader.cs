namespace ObentoTensho.Core.Settings.Abstractions;

// BR-RPT-22: đọc từ bản chụp trong bộ nhớ, không truy vấn mỗi lần; khóa chưa có thì lấy mặc định của danh mục
public interface ISettingsReader
{
    string Get(string key);

    int GetInt(string key);
    decimal GetDecimal(string key);

    bool GetBool(string key);

    IReadOnlyDictionary<string, string> Public();
}
