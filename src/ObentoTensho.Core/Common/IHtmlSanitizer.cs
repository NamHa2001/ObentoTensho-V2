namespace ObentoTensho.Core.Common;

// SEC-08: HTML do nhân viên nhập (mô tả món, tin tức…) được làm sạch trước khi lưu
public interface IHtmlSanitizer
{
    string Sanitize(string html);

    // Bài viết, trang tĩnh: thêm tiêu đề lớn và ảnh (src http/https/đường dẫn tương đối), vẫn bỏ script và thuộc tính sự kiện
    string SanitizeRich(string html);
}
