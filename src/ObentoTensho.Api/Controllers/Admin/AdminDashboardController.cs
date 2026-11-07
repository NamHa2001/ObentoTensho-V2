using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Reports.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// BR-RPT-03, BR-RPT-32: server chọn bảng tin theo quyền; Reception không có quyền báo cáo nên không nhận trường doanh thu
[ApiController]
[Route("api/v1/admin/dashboard")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Dashboard)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        StaffCapabilities.Has(User.StaffRole(), StaffCapability.Reports)
            ? this.Data(await dashboard.ManagerAsync(cancellationToken))
            : this.Data(await dashboard.ReceptionAsync(cancellationToken));
}
