using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Realtime;

/// <summary>RT-05: sơ đồ bàn cập nhật không cần tải lại.</summary>
public sealed record TableEventDto(int Id, TableStatus Status);
