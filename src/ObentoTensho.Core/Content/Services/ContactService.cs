using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Content.Services;

// BR-CNT-33: lưu rồi đưa email vào hộp thư đi; ô bẫy và tin trùng trả thành công mà không làm gì để máy không dò được
public sealed class ContactService(
    IContactRepository contacts,
    IContactEmailOutbox emails,
    IStaffRepository staff,
    IActionThrottle throttle,
    IUnitOfWork unitOfWork,
    IRealtimeEvents realtime,
    IClock clock,
    IValidator<ContactRequest> validator)
{
    public const int MaxPageSize = 50;
    private const string DefaultLanguage = "vi";

    public async Task<Result> SubmitAsync(ContactRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.IsNullOrEmpty(request.Website))
        {
            return Result.Success();
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        var email = EmailAddress.Normalize(request.Email);
        if (throttle.Increment($"contact:{email}", TimeSpan.FromHours(1)) > ContactMessage.MaxPerEmailPerHour)
        {
            return Result.Failure(ErrorCodes.RateLimited);
        }

        var now = clock.UtcNow;
        var text = request.Message.Trim();
        if (await contacts.ExistsSinceAsync(email, text, now - ContactMessage.DuplicateWindow, cancellationToken))
        {
            return Result.Success();
        }

        var recipients = await staff.ListActiveEmailsAsync(StaffCapabilities.RolesFor(StaffCapability.Feedback), cancellationToken);
        var message = new ContactMessage
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            IpAddress = ipAddress,
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            Message = text,
            CreatedAtUtc = now,
        };
        await unitOfWork.InTransactionAsync(async ct =>
        {
            contacts.Add(message);
            await unitOfWork.SaveChangesAsync(ct);
            emails.EnqueueReceived(message, request.Lang ?? DefaultLanguage);
            realtime.Stage(RealtimeMessages.ContactCreated(message));
            foreach (var address in recipients)
            {
                emails.EnqueueInternal(message, address);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
        return Result.Success();
    }

    public async Task<PagedResponse<ContactMessageDto>> ListAsync(bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await contacts.ListAsync(unreadOnly, page, pageSize, cancellationToken);
        return new PagedResponse<ContactMessageDto>(items.Select(ToDto).ToArray(), new PageMeta(page, pageSize, total));
    }

    public async Task<Result<ContactMessageDto>> MarkReadAsync(int id, Guid staffId, CancellationToken cancellationToken)
    {
        if (await contacts.FindAsync(id, cancellationToken) is not { } message)
        {
            return Result.Failure<ContactMessageDto>(ErrorCodes.NotFound);
        }

        Read(message, staffId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(message));
    }

    // Đã phản hồi cũng có nghĩa là đã đọc; giữ mốc đầu tiên để biết tin nào từng bị bỏ sót bao lâu
    public async Task<Result<ContactMessageDto>> MarkRepliedAsync(int id, Guid staffId, CancellationToken cancellationToken)
    {
        if (await contacts.FindAsync(id, cancellationToken) is not { } message)
        {
            return Result.Failure<ContactMessageDto>(ErrorCodes.NotFound);
        }

        Read(message, staffId);
        if (message.RepliedAtUtc is null)
        {
            message.RepliedAtUtc = clock.UtcNow;
            message.RepliedByStaffId = staffId;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(message));
    }

    private void Read(ContactMessage message, Guid staffId)
    {
        if (!message.IsRead)
        {
            message.IsRead = true;
            message.ReadAtUtc = clock.UtcNow;
            message.ReadByStaffId = staffId;
        }
    }

    private static ContactMessageDto ToDto(ContactMessage m) =>
        new(m.Id, m.FullName, m.Email, m.PhoneNumber, m.Subject, m.Message, m.IsRead, m.ReadAtUtc, m.RepliedAtUtc, m.CreatedAtUtc);
}
