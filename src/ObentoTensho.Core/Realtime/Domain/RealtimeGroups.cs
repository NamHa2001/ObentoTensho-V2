using System.Globalization;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Realtime.Domain;

// SEC-04: nhóm do server gán; nhân viên theo vai trò, khách chỉ vào nhóm đã chứng minh quyền
public static class RealtimeGroups
{
    public const string StaffPrefix = "staff:";

    public static string Staff(StaffRole role) => StaffPrefix + role;

    // Người nhận theo ma trận quyền (một nguồn duy nhất với API)
    public static IReadOnlyList<string> StaffWith(StaffCapability capability) =>
        StaffCapabilities.RolesFor(capability).Select(Staff).ToArray();

    public static string Order(Guid orderId) => "order:" + orderId.ToString("D", CultureInfo.InvariantCulture);

    public static string Availability(DateOnly date) => "availability:" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
