using ObentoTensho.Core.Realtime.Domain;

namespace ObentoTensho.Core.Realtime.Abstractions;

// Gom sự kiện trong transaction; chỉ phát sau khi commit, transaction hoàn tác hoặc thử lại thì bỏ (DB-25)
public interface IRealtimeEvents
{
    void Stage(RealtimeMessage message);
}
