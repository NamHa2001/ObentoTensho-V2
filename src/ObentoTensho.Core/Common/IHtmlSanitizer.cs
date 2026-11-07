namespace ObentoTensho.Core.Common;

// SEC-08: HTML do nhân viên nhập (mô tả món, tin tức…) được làm sạch trước khi lưu
public interface IHtmlSanitizer
{
    string Sanitize(string html);
}
