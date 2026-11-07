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

        services.AddSingleton<IValidator<NewsCategoryRequest>, NewsCategoryRequestValidator>();
        services.AddSingleton<IValidator<NewsRequest>, NewsRequestValidator>();
        services.AddSingleton<IValidator<StaticPageRequest>, StaticPageRequestValidator>();
        services.AddSingleton<IValidator<CreateReviewRequest>, CreateReviewRequestValidator>();
        services.AddSingleton<IValidator<UpdateReviewRequest>, UpdateReviewRequestValidator>();
        services.AddSingleton<IValidator<ReviewReplyRequest>, ReviewReplyRequestValidator>();
        services.AddSingleton<IValidator<ContactRequest>, ContactRequestValidator>();
        services.AddSingleton<IValidator<NewsletterSubscribeRequest>, NewsletterSubscribeRequestValidator>();
        services.AddSingleton<IValidator<NewsletterCampaignRequest>, NewsletterCampaignRequestValidator>();
        services.AddScoped<NewsletterService>();
        services.AddScoped<NewsletterCampaignService>();
        services.AddScoped<NewsletterSendJob>();
        services.AddScoped<IRecurringJob>(sp => sp.GetRequiredService<NewsletterSendJob>());
        services.AddScoped<ReviewService>();
        services.AddScoped<ReviewAdminService>();
        services.AddScoped<ContactService>();
        services.AddScoped<NewsCategoryService>();
        services.AddScoped<NewsAdminService>();
        services.AddScoped<PublicNewsService>();
        services.AddScoped<StaticPageService>();
        services.AddScoped<IRecurringJob, NewsTrashPurgeJob>();
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
