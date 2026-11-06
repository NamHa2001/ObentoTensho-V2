using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Api.Http;

// Controller mỏng: chuyển Result của Service thành phản hồi HTTP chuẩn (docs/srs/09 §2, §4)
public static class ControllerResults
{
    public static IActionResult Data<T>(this ControllerBase controller, T data, string? version = null)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (version is not null)
        {
            controller.Response.Headers.ETag = $"\"{version}\"";
        }

        return controller.Ok(new ApiResponse<T>(data));
    }

    public static IActionResult DataOrProblem<T>(this ControllerBase controller, Result<T> result, Func<T, string>? version = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? controller.Data(result.Value, version?.Invoke(result.Value)) : controller.ToProblem(result);
    }

    public static IActionResult CreatedOrProblem<T>(this ControllerBase controller, Result<T> result, Func<T, string>? version = null)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            return controller.ToProblem(result);
        }

        if (version is not null)
        {
            controller.Response.Headers.ETag = $"\"{version(result.Value)}\"";
        }

        return new ObjectResult(new ApiResponse<T>(result.Value)) { StatusCode = StatusCodes.Status201Created };
    }

    public static IActionResult NoContentOrProblem(this ControllerBase controller, Result result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? controller.NoContent() : controller.ToProblem(result);
    }

    public static string? IfMatch(this ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        var value = controller.Request.Headers[HeaderNames.IfMatch].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
