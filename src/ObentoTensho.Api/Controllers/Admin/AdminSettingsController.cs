using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Settings;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Settings.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/19 §3: Manager+; khóa thanh toán, liên hệ quán do dịch vụ kiểm thêm vai trò và step-up vì phụ thuộc nội dung yêu cầu
[ApiController]
[Route("api/v1/admin/settings")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Reports)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminSettingsController(SettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await settings.ListAsync(cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateSettingsRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await settings.UpdateAsync(request, User.StaffId(), User.StaffRole(), Request.Headers[StepUpFilter.Header].ToString(), cancellationToken));

    [HttpPost("test-email")]
    public async Task<IActionResult> TestEmail(CancellationToken cancellationToken)
    {
        var result = await settings.SendTestEmailAsync(User.StaffId(), cancellationToken);
        return result.IsSuccess ? Accepted() : this.ToProblem(result);
    }
}
