using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Media.Abstractions;

namespace ObentoTensho.Core.Media.Services;

// BR-CNT-30: nội dung chỉ trỏ tới ảnh đã tải qua pipeline, không nhận URL hay mã lạ
public static class ImageReferences
{
    public static async Task<Result?> CheckAsync(IImageRepository images, Guid? imageId, string field, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (imageId is not { } id || (await images.LoadAsync([id], cancellationToken)).ContainsKey(id))
        {
            return null;
        }

        return Result.Failure(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [FieldErrorCodes.NotFound] });
    }
}
