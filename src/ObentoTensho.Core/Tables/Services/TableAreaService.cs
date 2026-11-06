using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Core.Tables.Services;

// Khu vực bàn (docs/srs/16): tên không trùng, xóa khi còn bàn ⇒ 409 (BR-TBL-14), sửa có ETag (BR-TBL-16)
public sealed class TableAreaService(
    ITableLayoutRepository layout,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IValidator<TableAreaRequest> validator)
{
    private const string Entity = "table_area";

    public async Task<IReadOnlyList<TableAreaDto>> ListAsync(CancellationToken cancellationToken) =>
        (await layout.ListAreasAsync(cancellationToken)).Select(a => a.ToDto()).ToArray();

    public async Task<Result<TableAreaDto>> CreateAsync(TableAreaRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<TableAreaDto>();
        }

        var name = request.Name.Trim();
        if (await layout.AreaNameExistsAsync(name, exceptId: null, cancellationToken))
        {
            return Result.Failure<TableAreaDto>(ErrorCodes.DuplicateName);
        }

        return await unitOfWork.TryCreateAsync(async ct =>
        {
            var area = new TableArea { Name = name, IsActive = request.IsActive };
            layout.AddArea(area);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Created), Entity, area.Id.ToString(CultureInfo.InvariantCulture), After: Snapshot(area)));
            await unitOfWork.SaveChangesAsync(ct);
            return area.ToDto();
        }, ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result<TableAreaDto>> UpdateAsync(int id, TableAreaRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<TableAreaDto>();
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<TableAreaDto>(ErrorCodes.PreconditionRequired);
        }

        if (await layout.FindAreaAsync(id, cancellationToken) is not { } area)
        {
            return Result.Failure<TableAreaDto>(ErrorCodes.NotFound);
        }

        var name = request.Name.Trim();
        if (await layout.AreaNameExistsAsync(name, id, cancellationToken))
        {
            return Result.Failure<TableAreaDto>(ErrorCodes.DuplicateName);
        }

        var before = Snapshot(area);
        unitOfWork.ExpectRowVersion(area, version);
        area.Name = name;
        area.IsActive = request.IsActive;
        audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Updated), Entity, id.ToString(CultureInfo.InvariantCulture), before, Snapshot(area)));
        return await unitOfWork.TrySaveAsync(area.ToDto, ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await layout.FindAreaAsync(id, cancellationToken) is not { } area)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (await layout.AreaHasTablesAsync(id, cancellationToken))
        {
            return Result.Failure(ErrorCodes.AreaHasTables);
        }

        unitOfWork.ExpectRowVersion(area, version);
        layout.RemoveArea(area);
        audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Deleted), Entity, id.ToString(CultureInfo.InvariantCulture), Before: Snapshot(area)));
        return await unitOfWork.TrySaveAsync(ErrorCodes.AreaHasTables, cancellationToken);
    }

    private static object Snapshot(TableArea area) => new { area.Name, area.IsActive };
}
