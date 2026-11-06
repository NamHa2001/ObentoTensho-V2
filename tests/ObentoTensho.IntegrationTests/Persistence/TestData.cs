using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.IntegrationTests.Persistence;

internal static class TestData
{
    public static DateTime Now { get; } = new(2026, 11, 6, 10, 30, 15, 123, DateTimeKind.Utc);

    public static User NewUser(string? email = null)
    {
        var address = email ?? $"{Guid.NewGuid():N}@example.test";
        return new User
        {
            FullName = "Nguyễn Văn A",
            Email = address,
            EmailCanonical = address,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = Now,
        };
    }

    public static Staff NewStaff(StaffRole role = StaffRole.Reception, string? email = null) => new()
    {
        FullName = "Lễ tân",
        Email = email ?? $"{Guid.NewGuid():N}@example.test",
        Role = role,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        CreatedAtUtc = Now,
    };
}
