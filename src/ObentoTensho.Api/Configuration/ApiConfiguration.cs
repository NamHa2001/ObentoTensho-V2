using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Api.Configuration;

public static class ApiConfiguration
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(o =>
            {
                // SEC-48: body có trường lạ bị từ chối
                o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;

                // Enum dạng chuỗi snake_case, giống giá trị lưu CSDL (vd "password_change")
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
                o.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
            })
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
            {
                // JSON sai cú pháp/sai kiểu/thừa trường: cùng định dạng lỗi VALIDATION_FAILED
                var errors = context.ModelState
                    .Where(e => e.Value is { Errors.Count: > 0 })
                    .ToDictionary(e => e.Key, _ => new[] { FieldErrorCodes.InvalidFormat }, StringComparer.Ordinal);
                return ApiProblems.Create(context.HttpContext, ErrorCodes.ValidationFailed, errors).ToActionResult();
            });

        services.AddProblemDetails();
        return services;
    }

    // 5xx: không lộ chi tiết nội bộ, chỉ trả errorCode và traceId
    public static IApplicationBuilder UseApiExceptionHandler(this IApplicationBuilder app) =>
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            // Lỗi do request (thân quá lớn, multipart hỏng…) giữ đúng 4xx thay vì thành 500
            ExceptionHandler = context => ApiProblems.WriteAsync(context, context.Features.Get<IExceptionHandlerFeature>()?.Error switch
            {
                BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => ErrorCodes.FileTooLarge,
                BadHttpRequestException => ErrorCodes.ValidationFailed,
                _ => ErrorCodes.InternalError,
            }),
        });
}
