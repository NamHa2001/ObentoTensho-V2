using FluentValidation;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Services;
using ObentoTensho.Core.Content.Validators;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.Infrastructure.Media;

namespace ObentoTensho.Api.Configuration;

// Ảnh và nội dung (docs/srs/04 §2, docs/srs/17)
public static class ContentConfiguration
{
    public static IServiceCollection AddContentServices(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<BannerRequest>, BannerRequestValidator>();
        services.AddSingleton<IValidator<GalleryItemRequest>, GalleryItemRequestValidator>();

        services.AddSingleton<ImageMapper>();
        services.AddScoped<ImageService>();
        services.AddScoped<IRecurringJob, ImageProcessingJob>();
        services.AddScoped<BannerAdminService>();
        services.AddScoped<GalleryAdminService>();
        services.AddScoped<PublicContentService>();
        return services;
    }

    // Máy chủ thật do Nginx phục vụ /uploads; API chỉ tự phục vụ khi bật Media:ServeFromApi (mặc định ở Development).
    // FR-IMG-11: đường dẫn chứa id và phiên bản nên cache vĩnh viễn an toàn.
    public static void UseMediaFiles(this WebApplication app)
    {
        var directory = app.Services.GetRequiredService<IOptions<MediaOptions>>().Value.PublicDirectory;
        Directory.CreateDirectory(directory);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(directory),
            RequestPath = "/uploads",
            OnPrepareResponse = context => context.Context.Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable",
        });
    }
}
