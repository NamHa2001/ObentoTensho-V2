using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reservations.Services;

// FR-RSV-01 (khách đặt), FR-RSV-03 (tra cứu, hủy). Không cần tài khoản; đang đăng nhập thì gắn vào tài khoản.
public sealed class PublicReservationService(
    IReservationRepository reservations,
    ReservationSchedule schedule,
    ReservationBooking booking,
    ReservationTransitions transitions,
    ISecureTokenService secureTokens,
    IActionThrottle throttle,
    IReservationSettings settings,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CreateReservationRequest> validator)
{
    // SEC-01 e: sai 5 lần với một mã ⇒ khóa tra cứu mã đó 15 phút (giới hạn theo IP nằm ở rate limit "lookup")
    private const int MaxLookupFailures = 5;
    private static readonly TimeSpan LookupLockout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan NoShowWindow = TimeSpan.FromDays(90);

    public async Task<Result<PublicReservationDto>> CreateAsync(CreateReservationRequest request, Guid? userId, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<PublicReservationDto>();
        }

        var slot = await schedule.CheckBookableAsync(request.Date, request.Time, staff: false, cancellationToken);
        if (!slot.IsSuccess)
        {
            return Result.Failure<PublicReservationDto>(slot.ErrorCode ?? ErrorCodes.OutsideServiceSlot);
        }

        // Validator đã kiểm định dạng; nhánh này chỉ để có giá trị chuẩn hóa
        if (!VietnamesePhoneNumber.TryNormalize(request.GuestPhone, out var phone))
        {
            return Result.Failure<PublicReservationDto>(ErrorCodes.ValidationFailed);
        }

        var now = clock.UtcNow;
        if (await reservations.CountActiveForPhoneAsync(phone, now, cancellationToken) >= settings.MaxActivePerPhone)
        {
            return Result.Failure<PublicReservationDto>(ErrorCodes.ReservationLimit);
        }

        // BR-RSV-21/22: SĐT có lịch sử không đến hoặc quán bật chống lạm dụng ⇒ phải xác minh OTP (luồng OTP ở bước sau)
        var flagged = await reservations.FlaggedPhonesAsync([phone], now - NoShowWindow, settings.NoShowFlagCount, cancellationToken);
        if (settings.RequireContactVerify || flagged.Count > 0)
        {
            return Result.Failure<PublicReservationDto>(ErrorCodes.OtpRequired);
        }

        var draft = new BookingDraft(
            request.Date,
            request.Time,
            slot.Value,
            request.AdultCount,
            request.ChildCount,
            request.GuestName.Trim(),
            phone,
            EmailAddress.Normalize(request.GuestEmail),
            Clean(request.SpecialRequest),
            InternalNote: null,
            request.Lang ?? AuthPolicy.DefaultLanguage,
            ReservationSource.Web,
            userId,
            StaffId: null,
            ReservationStatus.Pending,
            OverrideCapacityReason: null);

        var created = await booking.BookAsync(draft, cancellationToken);
        return created.IsSuccess
            ? Result.Success(created.Value.ToPublic())
            : Result.Failure<PublicReservationDto>(created.ErrorCode ?? ErrorCodes.SlotFull);
    }

    // Sai mã và sai SĐT trả cùng NOT_FOUND để không dò được mã nào tồn tại
    public async Task<Result<PublicReservationDto>> LookupAsync(LookupReservationRequest request, CancellationToken cancellationToken)
    {
        var code = ReservationCodes.Normalize(request.Code);
        var key = $"reservation-lookup:{code}";
        if (code.Length == 0 || throttle.Count(key) >= MaxLookupFailures)
        {
            return Result.Failure<PublicReservationDto>(code.Length == 0 ? ErrorCodes.NotFound : ErrorCodes.RateLimited);
        }

        var reservation = await reservations.FindByCodeForUpdateAsync(code, cancellationToken);
        if (reservation is null || !SameSecret(PiiMask.Last4(reservation.GuestPhone), request.PhoneLast4 ?? string.Empty))
        {
            throttle.Increment(key, LookupLockout);
            return Result.Failure<PublicReservationDto>(ErrorCodes.NotFound);
        }

        return Result.Success(reservation.ToPublic());
    }

    // BR-RSV-06, D-38: hủy được bất cứ lúc nào trước check-in, không phạt; cần token trong email hoặc là chủ tài khoản
    public Task<Result<PublicReservationDto>> CancelAsync(string code, CancelReservationRequest request, Guid? userId, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            var reservation = await reservations.FindByCodeForUpdateAsync(ReservationCodes.Normalize(code), ct);
            if (reservation is null || !IsOwner(reservation, request.Token, userId))
            {
                return Result.Failure<PublicReservationDto>(ErrorCodes.NotFound);
            }

            var applied = await transitions.ApplyAsync(reservation, ReservationStatus.Cancelled, ReservationActor.Guest, userId, Clean(request.Reason), ct);
            if (!applied.IsSuccess)
            {
                return Result.Failure<PublicReservationDto>(applied.ErrorCode ?? ErrorCodes.InvalidTransition, applied.FieldErrors);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(reservation.ToPublic());
        }, cancellationToken);

    private bool IsOwner(Reservation reservation, string? token, Guid? userId)
    {
        if (!string.IsNullOrEmpty(token) && reservation.AccessTokenHash is { } hash)
        {
            return secureTokens.HashEquals(hash, secureTokens.Hash(token));
        }

        return userId is not null && reservation.UserId == userId;
    }

    private static bool SameSecret(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
