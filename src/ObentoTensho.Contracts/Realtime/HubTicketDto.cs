namespace ObentoTensho.Contracts.Realtime;

/// <summary>Vé nối hub dùng một lần, sống 30 giây: nối <c>/hubs/v1/...?ticket=</c> bằng WebSocket (RT-09, SEC-46).</summary>
public sealed record HubTicketDto(string Ticket, DateTime ExpiresAtUtc);
