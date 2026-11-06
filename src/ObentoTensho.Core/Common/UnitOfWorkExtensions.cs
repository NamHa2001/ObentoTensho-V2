using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Core.Common;

public static class UnitOfWorkExtensions
{
    // Lưu và đổi lỗi đồng thời/trùng khóa thành mã lỗi nghiệp vụ (412 / mã trùng do bên gọi chọn)
    public static async Task<Result<T>> TrySaveAsync<T>(
        this IUnitOfWork unitOfWork,
        Func<T> onSuccess,
        string duplicateErrorCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(onSuccess);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(onSuccess());
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure<T>(ErrorCodes.PreconditionFailed);
        }
        catch (DuplicateKeyException)
        {
            return Result.Failure<T>(duplicateErrorCode);
        }
    }

    public static async Task<Result> TrySaveAsync(this IUnitOfWork unitOfWork, string duplicateErrorCode, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.TrySaveAsync(() => true, duplicateErrorCode, cancellationToken);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.ErrorCode ?? ErrorCodes.InternalError);
    }

    // Tạo bản ghi rồi ghi audit với Id vừa sinh, trong cùng một transaction
    public static async Task<Result<T>> TryCreateAsync<T>(
        this IUnitOfWork unitOfWork,
        Func<CancellationToken, Task<T>> work,
        string duplicateErrorCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        try
        {
            return Result.Success(await unitOfWork.InTransactionAsync(work, cancellationToken));
        }
        catch (DuplicateKeyException)
        {
            return Result.Failure<T>(duplicateErrorCode);
        }
    }
}
