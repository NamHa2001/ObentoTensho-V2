using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Api.Http;

// RFC 9457 ProblemDetails kèm errorCode và traceId (docs/srs/09 §2)
public static class ApiProblems
{
    private const string ContentType = "application/problem+json";

    private static readonly Dictionary<string, (int Status, string Title)> Known = new(StringComparer.Ordinal)
    {
        [ErrorCodes.ValidationFailed] = (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ"),
        [ErrorCodes.Unauthenticated] = (StatusCodes.Status401Unauthorized, "Chưa xác thực"),
        [ErrorCodes.TokenExpired] = (StatusCodes.Status401Unauthorized, "Phiên đã hết hạn"),
        [ErrorCodes.InvalidCredentials] = (StatusCodes.Status401Unauthorized, "Email hoặc mật khẩu không đúng"),
        [ErrorCodes.Forbidden] = (StatusCodes.Status403Forbidden, "Không đủ quyền"),
        [ErrorCodes.NotFound] = (StatusCodes.Status404NotFound, "Không tìm thấy"),
        [ErrorCodes.AccountLocked] = (StatusCodes.Status423Locked, "Tài khoản đang bị khóa"),
        [ErrorCodes.TokenInvalid] = (StatusCodes.Status400BadRequest, "Liên kết không hợp lệ hoặc đã hết hạn"),
        [ErrorCodes.PreconditionFailed] = (StatusCodes.Status412PreconditionFailed, "Dữ liệu đã bị người khác thay đổi, hãy tải lại"),
        [ErrorCodes.PreconditionRequired] = (StatusCodes.Status428PreconditionRequired, "Thiếu If-Match (phiên bản dữ liệu)"),
        [ErrorCodes.DuplicateName] = (StatusCodes.Status409Conflict, "Tên đã tồn tại"),
        [ErrorCodes.AreaHasTables] = (StatusCodes.Status409Conflict, "Khu vực còn bàn"),
        [ErrorCodes.AreaInactive] = (StatusCodes.Status400BadRequest, "Khu vực không tồn tại hoặc đang tắt"),
        [ErrorCodes.SlotOverlap] = (StatusCodes.Status409Conflict, "Ca phục vụ bị chồng lấn"),
        [ErrorCodes.SlotOutsideOpeningHours] = (StatusCodes.Status409Conflict, "Ca phục vụ nằm ngoài giờ mở cửa"),
        [ErrorCodes.TableHasReservations] = (StatusCodes.Status409Conflict, "Bàn còn đặt bàn sắp tới"),
        [ErrorCodes.SlotHasReservations] = (StatusCodes.Status409Conflict, "Ca còn đặt bàn sắp tới"),
        [ErrorCodes.SlotFull] = (StatusCodes.Status409Conflict, "Ca đã đầy"),
        [ErrorCodes.DateOutOfRange] = (StatusCodes.Status400BadRequest, "Ngày giờ không nằm trong khoảng nhận đặt"),
        [ErrorCodes.OutsideOpeningHours] = (StatusCodes.Status400BadRequest, "Ngoài giờ mở cửa"),
        [ErrorCodes.OutsideServiceSlot] = (StatusCodes.Status400BadRequest, "Giờ đặt không thuộc ca phục vụ nào"),
        [ErrorCodes.DateBlocked] = (StatusCodes.Status400BadRequest, "Quán tạm ngừng nhận đặt vào thời gian này"),
        [ErrorCodes.ReservationLimit] = (StatusCodes.Status409Conflict, "Số điện thoại đã có đủ đặt bàn đang chờ"),
        [ErrorCodes.OtpRequired] = (StatusCodes.Status409Conflict, "Cần xác minh liên hệ"),
        [ErrorCodes.OtpInvalid] = (StatusCodes.Status422UnprocessableEntity, "Mã xác minh không đúng hoặc đã hết hạn"),
        [ErrorCodes.CaptchaFailed] = (StatusCodes.Status400BadRequest, "Xác minh chống máy tự động không thành công"),
        [ErrorCodes.InvalidTransition] = (StatusCodes.Status409Conflict, "Không thể chuyển sang trạng thái này"),
        [ErrorCodes.ContactNotConfirmed] = (StatusCodes.Status409Conflict, "Chưa gọi xác minh với khách"),
        [ErrorCodes.CallAttemptsExceeded] = (StatusCodes.Status409Conflict, "Đã gọi đủ số lần cho phép"),
        [ErrorCodes.TableConflict] = (StatusCodes.Status409Conflict, "Bàn đã có đặt bàn khác cùng ca"),
        [ErrorCodes.TableUnsuitable] = (StatusCodes.Status400BadRequest, "Bàn không phù hợp số khách hoặc đang tắt"),
        [ErrorCodes.CategoryHasItems] = (StatusCodes.Status409Conflict, "Danh mục còn món"),
        [ErrorCodes.CategoryInactive] = (StatusCodes.Status400BadRequest, "Danh mục không tồn tại hoặc đang ẩn"),
        [ErrorCodes.OptionGroupInUse] = (StatusCodes.Status409Conflict, "Nhóm tùy chọn đang được món sử dụng"),
        [ErrorCodes.OptionInvalid] = (StatusCodes.Status400BadRequest, "Lựa chọn tùy chọn không hợp lệ"),
        [ErrorCodes.OptionUnavailable] = (StatusCodes.Status400BadRequest, "Tùy chọn đã hết"),
        [ErrorCodes.ItemUnavailable] = (StatusCodes.Status400BadRequest, "Có món không đặt được"),
        [ErrorCodes.OrderLimit] = (StatusCodes.Status409Conflict, "Số điện thoại đã có đủ đơn đang xử lý"),
        [ErrorCodes.PaymentRequired] = (StatusCodes.Status409Conflict, "Đơn chưa thanh toán"),
        [ErrorCodes.FileInvalid] = (StatusCodes.Status400BadRequest, "Tệp không đúng định dạng hoặc mẫu"),
        [ErrorCodes.FileTooLarge] = (StatusCodes.Status413PayloadTooLarge, "Tệp quá lớn hoặc quá nhiều dòng"),
        [ErrorCodes.RateLimited] = (StatusCodes.Status429TooManyRequests, "Quá nhiều yêu cầu, vui lòng thử lại sau"),
        [ErrorCodes.InternalError] = (StatusCodes.Status500InternalServerError, "Lỗi hệ thống"),
    };

    public static ProblemDetails Create(HttpContext http, string errorCode, IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        var (status, title) = Known.TryGetValue(errorCode, out var known) ? known : (StatusCodes.Status400BadRequest, errorCode);

        ProblemDetails problem = fieldErrors is { Count: > 0 }
            ? new ValidationProblemDetails(fieldErrors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = title;
        problem.Type = "/errors/" + errorCode.ToLowerInvariant().Replace('_', '-');
        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
        return problem;
    }

    public static ObjectResult ToActionResult(this ProblemDetails problem) =>
        new(problem) { StatusCode = problem.Status, ContentTypes = { ContentType } };

    public static ObjectResult ToProblem(this ControllerBase controller, Result result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);
        var errorCode = result.ErrorCode ?? ErrorCodes.InternalError;
        return Create(controller.HttpContext, errorCode, result.FieldErrors).ToActionResult();
    }

    public static Task WriteAsync(HttpContext http, string errorCode)
    {
        var problem = Create(http, errorCode);
        http.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return http.Response.WriteAsJsonAsync(problem, options: null, contentType: ContentType, http.RequestAborted);
    }
}
