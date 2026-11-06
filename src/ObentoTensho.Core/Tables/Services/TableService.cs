using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Core.Tables.Services;

// Bàn và sơ đồ bàn (docs/srs/16). Khu vực phải tồn tại và đang bật (BR-TBL-14); tên bàn không trùng trong khu vực.
public sealed class TableService(
    ITableLayoutRepository layout,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IReservationRepository reservations,
    IValidator<TableRequest> validator)
{
    private const string Entity = "table";

    public async Task<IReadOnlyList<TableDto>> ListAsync(int? areaId, CancellationToken cancellationToken) =>
        (await layout.ListTablesAsync(areaId, cancellationToken)).Select(t => t.ToDto()).ToArray();

    public async Task<IReadOnlyList<FloorPlanAreaDto>> FloorPlanAsync(CancellationToken cancellationToken)
    {
        var areas = await layout.ListAreasAsync(cancellationToken);
        var tables = (await layout.ListTablesAsync(areaId: null, cancellationToken)).ToLookup(t => t.AreaId);
        return areas.Select(a => new FloorPlanAreaDto(a.Id, a.Name, a.IsActive, tables[a.Id].Select(t => t.ToDto()).ToArray())).ToArray();
    }

    public async Task<Result<TableDto>> CreateAsync(TableRequest request, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, exceptId: null, cancellationToken) is { } failure)
        {
            return Result.Failure<TableDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        return await unitOfWork.TryCreateAsync(async ct =>
        {
            var table = new DiningTable { AreaId = request.AreaId, Name = request.Name.Trim(), MinCapacity = request.MinCapacity, MaxCapacity = request.MaxCapacity };
            Apply(table, request);
            layout.AddTable(table);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Created), Entity, table.Id.ToString(CultureInfo.InvariantCulture), After: Snapshot(table)));
            await unitOfWork.SaveChangesAsync(ct);
            return table.ToDto();
        }, ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result<TableDto>> UpdateAsync(int id, TableRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, id, cancellationToken) is { } failure)
        {
            return Result.Failure<TableDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<TableDto>(ErrorCodes.PreconditionRequired);
        }

        if (await layout.FindTableAsync(id, cancellationToken) is not { } table)
        {
            return Result.Failure<TableDto>(ErrorCodes.NotFound);
        }

        var before = Snapshot(table);
        unitOfWork.ExpectRowVersion(table, version);
        Apply(table, request);
        audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Updated), Entity, id.ToString(CultureInfo.InvariantCulture), before, Snapshot(table)));
        return await unitOfWork.TrySaveAsync(table.ToDto, ErrorCodes.DuplicateName, cancellationToken);
    }

    // BR-TBL-08: chỉ nhận trạng thái hợp lệ, ghi audit; không cần ETag vì là thao tác vận hành nhanh tại quầy
    public async Task<Result<TableDto>> SetStatusAsync(int id, TableStatus status, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(status))
        {
            return Result.Failure<TableDto>(ErrorCodes.ValidationFailed);
        }

        if (await layout.FindTableAsync(id, cancellationToken) is not { } table)
        {
            return Result.Failure<TableDto>(ErrorCodes.NotFound);
        }

        var before = new { table.CurrentStatus };
        table.CurrentStatus = status;
        audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.StatusChanged), Entity, id.ToString(CultureInfo.InvariantCulture), before, new { table.CurrentStatus }));
        return await unitOfWork.TrySaveAsync(table.ToDto, ErrorCodes.DuplicateName, cancellationToken);
    }

    // BR-TBL-06: bàn còn đặt bàn chưa kết thúc thì không xóa (tắt bàn thay vì xóa)
    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await layout.FindTableAsync(id, cancellationToken) is not { } table)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (await reservations.HasActiveForTableAsync(id, cancellationToken))
        {
            return Result.Failure(ErrorCodes.TableHasReservations);
        }

        unitOfWork.ExpectRowVersion(table, version);
        layout.RemoveTable(table);
        audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Deleted), Entity, id.ToString(CultureInfo.InvariantCulture), Before: Snapshot(table)));
        return await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
    }

    private async Task<Result?> CheckAsync(TableRequest request, int? exceptId, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        var area = await layout.FindAreaAsync(request.AreaId, cancellationToken);
        if (area is null || !area.IsActive)
        {
            return Result.Failure(ErrorCodes.AreaInactive);
        }

        return await layout.TableNameExistsAsync(request.AreaId, request.Name.Trim(), exceptId, cancellationToken)
            ? Result.Failure(ErrorCodes.DuplicateName)
            : null;
    }

    private static void Apply(DiningTable table, TableRequest request)
    {
        table.AreaId = request.AreaId;
        table.Name = request.Name.Trim();
        table.MinCapacity = request.MinCapacity;
        table.MaxCapacity = request.MaxCapacity;
        table.PosX = request.Layout.X;
        table.PosY = request.Layout.Y;
        table.Width = request.Layout.Width;
        table.Height = request.Layout.Height;
        table.Rotation = request.Layout.Rotation;
        table.IsActive = request.IsActive;
    }

    private static object Snapshot(DiningTable t) =>
        new { t.AreaId, t.Name, t.MinCapacity, t.MaxCapacity, t.PosX, t.PosY, t.Width, t.Height, t.Rotation, t.IsActive };
}
