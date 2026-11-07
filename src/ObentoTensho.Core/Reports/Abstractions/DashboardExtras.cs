namespace ObentoTensho.Core.Reports.Abstractions;

public sealed record DashboardExtras(
    int UnreadContacts,
    IReadOnlyList<(string Code, int HoursLeft)> ExpiringVouchers,
    IReadOnlyList<(string StartTime, int PercentFull)> BusySlots,
    IReadOnlyDictionary<int, string> CategoryNames,
    IReadOnlyDictionary<int, string> ItemNames);
