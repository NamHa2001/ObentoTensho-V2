using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Services;

// Nhóm tùy chọn dùng chung và tùy chọn (BR-MNU-18..25)
public sealed class OptionGroupAdminService(
    IMenuRepository menu,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IMenuCache cache,
    IValidator<OptionGroupRequest> groupValidator,
    IValidator<OptionRequest> optionValidator)
{
    private const string GroupEntity = "option_group";
    private const string OptionEntity = "option";

    public async Task<IReadOnlyList<AdminOptionGroupDto>> ListAsync(CancellationToken cancellationToken)
    {
        var groups = await menu.ListGroupsAsync(cancellationToken);
        var options = (await menu.ListOptionsAsync(null, cancellationToken)).ToLookup(o => o.GroupId);
        return groups.OrderBy(g => g.DisplayOrder).ThenBy(g => g.Id).Select(g => g.ToAdmin(options[g.Id])).ToArray();
    }

    public async Task<Result<AdminOptionGroupDto>> CreateGroupAsync(OptionGroupRequest request, CancellationToken cancellationToken)
    {
        var validation = await groupValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminOptionGroupDto>();
        }

        var order = (await menu.ListGroupsAsync(cancellationToken)).Select(g => g.DisplayOrder).DefaultIfEmpty(0).Max() + 1;
        var result = await unitOfWork.TryCreateAsync(async ct =>
        {
            var group = new OptionGroup { Name = string.Empty, DisplayOrder = order };
            Apply(group, request);
            menu.AddGroup(group);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"menu.{GroupEntity}.created", GroupEntity, Id(group.Id), After: request));
            await unitOfWork.SaveChangesAsync(ct);
            return group.ToAdmin([]);
        }, ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    public async Task<Result<AdminOptionGroupDto>> UpdateGroupAsync(int id, OptionGroupRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var validation = await groupValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminOptionGroupDto>();
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<AdminOptionGroupDto>(ErrorCodes.PreconditionRequired);
        }

        if (await menu.FindGroupAsync(id, cancellationToken) is not { } group)
        {
            return Result.Failure<AdminOptionGroupDto>(ErrorCodes.NotFound);
        }

        var before = new { group.Name, group.SelectionType, group.IsRequired, group.MinSelect, group.MaxSelect, group.IsActive };
        unitOfWork.ExpectRowVersion(group, version);
        Apply(group, request);
        audit.Record(new AuditEntry($"menu.{GroupEntity}.updated", GroupEntity, Id(id), before, request));
        var options = await menu.ListOptionsAsync([id], cancellationToken);
        var result = await unitOfWork.TrySaveAsync(() => group.ToAdmin(options), ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    // BR-MNU-23: nhóm đang gắn với món thì không xóa — gỡ khỏi món hoặc tắt nhóm
    public async Task<Result> DeleteGroupAsync(int id, CancellationToken cancellationToken)
    {
        if (await menu.FindGroupAsync(id, cancellationToken) is not { } group)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (await menu.GroupInUseAsync(id, cancellationToken))
        {
            return Result.Failure(ErrorCodes.OptionGroupInUse);
        }

        // BR-MNU-23: tùy chọn của nhóm đã có trong đơn ⇒ chỉ ẩn nhóm, giữ dữ liệu cho thống kê
        var optionIds = (await menu.ListOptionsAsync([id], cancellationToken)).Select(o => o.Id).ToArray();
        if (await menu.AnyOptionOrderedAsync(optionIds, cancellationToken))
        {
            group.IsActive = false;
            audit.Record(new AuditEntry($"menu.{GroupEntity}.hidden", GroupEntity, Id(id), Before: new { group.Name }));
        }
        else
        {
            menu.RemoveGroup(group);
            audit.Record(new AuditEntry($"menu.{GroupEntity}.deleted", GroupEntity, Id(id), Before: new { group.Name }));
        }

        var result = await unitOfWork.TrySaveAsync(ErrorCodes.OptionGroupInUse, cancellationToken);
        cache.Invalidate();
        return result;
    }

    public async Task<Result<AdminOptionDto>> CreateOptionAsync(int groupId, OptionRequest request, CancellationToken cancellationToken)
    {
        var validation = await optionValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminOptionDto>();
        }

        if (await menu.FindGroupAsync(groupId, cancellationToken) is null)
        {
            return Result.Failure<AdminOptionDto>(ErrorCodes.NotFound);
        }

        var count = await menu.CountOptionsAsync(groupId, cancellationToken);
        if (count >= MenuRules.MaxOptionsPerGroup)
        {
            return Result.Failure<AdminOptionDto>(ErrorCodes.ValidationFailed);
        }

        var result = await unitOfWork.TryCreateAsync(async ct =>
        {
            var option = new MenuOption { GroupId = groupId, Name = string.Empty, DisplayOrder = count + 1 };
            Apply(option, request);
            menu.AddOption(option);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"menu.{OptionEntity}.created", OptionEntity, Id(option.Id), After: request));
            await unitOfWork.SaveChangesAsync(ct);
            return option.ToAdmin();
        }, ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    public async Task<Result<AdminOptionDto>> UpdateOptionAsync(int id, OptionRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var validation = await optionValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminOptionDto>();
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<AdminOptionDto>(ErrorCodes.PreconditionRequired);
        }

        if (await menu.FindOptionAsync(id, cancellationToken) is not { } option)
        {
            return Result.Failure<AdminOptionDto>(ErrorCodes.NotFound);
        }

        var before = new { option.Name, option.PriceDelta, option.Status, option.IsDefault };
        unitOfWork.ExpectRowVersion(option, version);
        Apply(option, request);
        audit.Record(new AuditEntry($"menu.{OptionEntity}.updated", OptionEntity, Id(id), before, request));
        var result = await unitOfWork.TrySaveAsync(option.ToAdmin, ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    // Khi có đơn hàng: tùy chọn đã nằm trong đơn chỉ được ẩn (BR-MNU-23) — kiểm thêm ở phần đặt món
    public async Task<Result> DeleteOptionAsync(int id, CancellationToken cancellationToken)
    {
        if (await menu.FindOptionAsync(id, cancellationToken) is not { } option)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        // BR-MNU-23: đã có trong đơn ⇒ chỉ ẩn
        if (await menu.AnyOptionOrderedAsync([id], cancellationToken))
        {
            option.Status = SaleStatus.Hidden;
            audit.Record(new AuditEntry($"menu.{OptionEntity}.hidden", OptionEntity, Id(id), Before: new { option.Name, option.PriceDelta }));
        }
        else
        {
            menu.RemoveOption(option);
            audit.Record(new AuditEntry($"menu.{OptionEntity}.deleted", OptionEntity, Id(id), Before: new { option.Name, option.PriceDelta }));
        }

        var result = await unitOfWork.TrySaveAsync(ErrorCodes.OptionGroupInUse, cancellationToken);
        cache.Invalidate();
        return result;
    }

    private static void Apply(OptionGroup group, OptionGroupRequest request)
    {
        group.Name = request.Name.Trim();
        group.SelectionType = request.SelectionType;
        group.IsRequired = request.IsRequired;
        group.MinSelect = (byte)request.MinSelect;
        group.MaxSelect = (byte)request.MaxSelect;
        group.IsActive = request.IsActive;
    }

    private static void Apply(MenuOption option, OptionRequest request)
    {
        option.Name = request.Name.Trim();
        option.PriceDelta = request.PriceDelta;
        option.IsDefault = request.IsDefault;
        option.Status = request.Status;
        option.Allergens = MenuRules.JoinAllergens(request.Allergens);
    }

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
}
