namespace ObentoTensho.Api.Http;

// Q5: nhớ lúc Cloudflare bắt đầu không phản hồi; lỗi kéo dài quá AlertAfter thì báo SuperAdmin một lần cho mỗi đợt
public sealed class TurnstileOutage
{
    public static readonly TimeSpan AlertAfter = TimeSpan.FromMinutes(10);

    private readonly Lock _lock = new();
    private DateTime? _since;
    private bool _alerted;

    // Trả mốc bắt đầu đợt lỗi khi bên gọi cần gửi cảnh báo (một lần mỗi đợt)
    public DateTime? Down(DateTime now)
    {
        lock (_lock)
        {
            _since ??= now;
            if (_alerted || now - _since.Value < AlertAfter)
            {
                return null;
            }

            _alerted = true;
            return _since;
        }
    }

    public void Up()
    {
        lock (_lock)
        {
            _since = null;
            _alerted = false;
        }
    }
}
