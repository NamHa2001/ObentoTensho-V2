namespace ObentoTensho.Core.Vouchers.Abstractions;

// BR-VCH-15: xuất Excel, chống chèn công thức (SEC-11)
public interface IVoucherSpreadsheet
{
    byte[] Write(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<object?>> rows);
}
