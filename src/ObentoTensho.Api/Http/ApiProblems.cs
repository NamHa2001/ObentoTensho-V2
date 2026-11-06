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
