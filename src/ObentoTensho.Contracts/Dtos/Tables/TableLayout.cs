namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Vị trí, kích thước (đơn vị lưới của sơ đồ) và góc xoay (độ, 0–359).</summary>
public sealed record TableLayout(int X, int Y, int Width, int Height, int Rotation);
