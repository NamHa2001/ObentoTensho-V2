namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>Tính lại DailyStats cho khoảng ngày (ngày kinh doanh, tối đa 92 ngày một lần) — dùng khi nhập dữ liệu cũ (PERF-55).</summary>
public sealed record RecomputeStatsRequest(DateOnly FromDate, DateOnly ToDate);
