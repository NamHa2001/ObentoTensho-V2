using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Media.Abstractions;

// SEC-10: chỉ tin kết quả giải mã thật, không tin tên tệp hay Content-Type
public interface IImageProcessor
{
    // Lỗi FILE_INVALID (không phải JPEG/PNG/WebP, hỏng) hoặc FILE_TOO_LARGE (quá số pixel); không ném ngoại lệ với tệp hỏng
    Result<ProbedImage> Probe(byte[] content);

    // Sinh biến thể từ ảnh gốc: xoay đúng chiều, bỏ siêu dữ liệu; tối đa 2 tác vụ đồng thời (FR-IMG-13)
    ProcessedImage Process(byte[] content);
}
