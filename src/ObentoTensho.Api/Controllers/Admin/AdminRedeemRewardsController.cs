using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Vouchers.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// Danh mục phần thưởng đổi điểm: cùng quyền với voucher (SuperAdmin, Manager)
[ApiController]
[Route("api/v1/admin/redeem-rewards")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Customers)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminRedeemRewardsController(RedeemRewardAdminService rewards) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await rewards.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(RedeemRewardRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await rewards.CreateAsync(request, cancellationToken), r => r.Version);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, RedeemRewardRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await rewards.UpdateAsync(id, request, this.IfMatch(), cancellationToken), r => r.Version);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await rewards.DeleteAsync(id, this.IfMatch(), cancellationToken));
}
