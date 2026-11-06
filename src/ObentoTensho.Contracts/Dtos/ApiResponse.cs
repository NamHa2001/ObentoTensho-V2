namespace ObentoTensho.Contracts.Dtos;

/// <summary>Phong bì phản hồi thành công <c>{ "data": … }</c> (docs/srs/09 §2).</summary>
public sealed record ApiResponse<T>(T Data);
