namespace ObentoTensho.Core.Reports.Abstractions;

public sealed record ReportNames(IReadOnlyDictionary<int, string> Categories, IReadOnlyDictionary<int, string> Items, IReadOnlyDictionary<int, string> Areas);
