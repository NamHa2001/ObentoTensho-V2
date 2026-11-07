using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Content.Services;

// FR-CNT-07: đăng ký hai bước (BR-CNT-21), hủy bằng POST (BR-CNT-22), đăng ký lại sau hủy phải xác nhận lại (BR-CNT-20)
public sealed class NewsletterService(
    INewsletterRepository newsletter,
    INewsletterEmails emails,
    INewsletterTokenProtector protector,
    INewsletterSpreadsheet spreadsheet,
    ISecureTokenService tokens,
    IActionThrottle throttle,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<NewsletterSubscribeRequest> validator)
{
    public const int MaxPageSize = 50;
    public const int ExportLimit = 50_000;
    private const string DefaultLanguage = "vi";

    public async Task<Result<NewsletterSubscribeResultDto>> SubscribeAsync(NewsletterSubscribeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pending = new NewsletterSubscribeResultDto(NewsletterSubscribeStatus.PendingConfirmation);
        if (!string.IsNullOrEmpty(request.Website))
        {
            return Result.Success(pending);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<NewsletterSubscribeResultDto>();
        }

        var email = EmailAddress.Normalize(request.Email);
        if (throttle.Increment($"newsletter:{email}", TimeSpan.FromHours(1)) > NewsletterSubscriber.MaxPerEmailPerHour)
        {
            return Result.Failure<NewsletterSubscribeResultDto>(ErrorCodes.RateLimited);
        }

        var language = request.Lang ?? DefaultLanguage;
        var subscriber = await newsletter.FindByEmailAsync(email, cancellationToken);
        if (subscriber is { IsSubscribed: true })
        {
            return Result.Success(new NewsletterSubscribeResultDto(NewsletterSubscribeStatus.AlreadySubscribed));
        }

        var now = clock.UtcNow;
        if (subscriber is null)
        {
            var unsubscribe = tokens.Create();
            subscriber = new NewsletterSubscriber
            {
                Email = email,
                Lang = language,
                UnsubscribeTokenHash = unsubscribe.Hash,
                UnsubscribeTokenProtected = protector.Protect(unsubscribe.Value),
                SubscribedAtUtc = now,
            };
            newsletter.Add(subscriber);
        }

        var confirm = tokens.Create();
        subscriber.Lang = language;
        subscriber.ConfirmTokenHash = confirm.Hash;
        subscriber.ConfirmExpiryUtc = now + NewsletterSubscriber.ConfirmLifetime;
        emails.EnqueueConfirmation(subscriber, confirm.Value);

        // Hai yêu cầu song song cùng email: một bên trùng khóa, bên còn lại đã gửi thư xác nhận
        var saved = await unitOfWork.TrySaveAsync(() => pending, ErrorCodes.DuplicateName, cancellationToken);
        return string.Equals(saved.ErrorCode, ErrorCodes.DuplicateName, StringComparison.Ordinal) ? Result.Success(pending) : saved;
    }

    public async Task<Result> ConfirmAsync(string token, string? ipAddress, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (string.IsNullOrEmpty(token)
            || await newsletter.FindByConfirmHashAsync(tokens.Hash(token), cancellationToken) is not { } subscriber
            || subscriber.ConfirmExpiryUtc < now)
        {
            return Result.Failure(ErrorCodes.TokenInvalid);
        }

        subscriber.IsActive = true;
        subscriber.ConfirmedAtUtc = now;
        subscriber.SubscribedAtUtc = now;
        subscriber.UnsubscribedAtUtc = null;
        subscriber.ConfirmTokenHash = null;
        subscriber.ConfirmExpiryUtc = null;
        newsletter.Add(new ConsentRecord
        {
            Email = subscriber.Email,
            DocType = ConsentRecord.Marketing,
            Version = ConsentRecord.MarketingVersion,
            AcceptedAtUtc = now,
            Ip = ipAddress,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UnsubscribeAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token)
            || await newsletter.FindByUnsubscribeHashAsync(tokens.Hash(token), cancellationToken) is not { } subscriber)
        {
            return Result.Failure(ErrorCodes.TokenInvalid);
        }

        if (subscriber.IsActive)
        {
            subscriber.IsActive = false;
            subscriber.UnsubscribedAtUtc = clock.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<PagedResponse<NewsletterSubscriberDto>> ListSubscribersAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await newsletter.ListSubscribersAsync(page, pageSize, cancellationToken);
        return new PagedResponse<NewsletterSubscriberDto>(
            items.Select(s => new NewsletterSubscriberDto(s.Id, s.Email, s.Lang, s.IsActive, s.ConfirmedAtUtc, s.SubscribedAtUtc, s.UnsubscribedAtUtc)).ToArray(),
            new PageMeta(page, pageSize, total));
    }

    // BR-CNT-25, BR-CUS-07: mỗi lần xuất đều ghi audit
    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken)
    {
        var items = await newsletter.ListForExportAsync(ExportLimit, cancellationToken);
        audit.Record(new AuditEntry("content.newsletter_subscriber.exported", "newsletter_subscriber", "export", After: new { items.Count }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return spreadsheet.Write(items);
    }

    public string ExportFileName() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"newsletter-{clock.TodayLocal:yyyy-MM-dd}.xlsx");
}
