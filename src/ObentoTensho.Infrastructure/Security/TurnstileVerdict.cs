namespace ObentoTensho.Infrastructure.Security;

public enum TurnstileVerdict
{
    Passed,
    Failed,

    // Không gọi được Cloudflare: bên gọi cho qua có giới hạn (SEC-57, Q5)
    Unavailable
}
