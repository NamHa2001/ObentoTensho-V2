namespace ObentoTensho.Core.Reports.Services;

public sealed record ReportFile(string FileName, string ContentType, byte[] Content);
