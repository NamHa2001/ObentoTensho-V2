using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Dtos.Customers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Services;
using ObentoTensho.Core.Customers.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/newsletter")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Feedback)]
public sealed class AdminNewsletterController(NewsletterService newsletter, NewsletterCampaignService campaigns, DataExportService exports) : ControllerBase
{
    [HttpGet("subscribers")]
    public async Task<IActionResult> Subscribers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await newsletter.ListSubscribersAsync(page, pageSize, cancellationToken));

    // BR-CNT-25: cùng luồng xuất có kiểm soát với danh sách khách
    [HttpPost("subscribers/export")]
    [RequireCapability(StaffCapability.CustomerExport)]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> Export(DataExportRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await exports.RequestAsync(DataExportKind.NewsletterSubscribers, request, User.StaffId(), cancellationToken));

    [HttpGet("campaigns")]
    public async Task<IActionResult> Campaigns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await campaigns.ListAsync(page, pageSize, cancellationToken));

    [HttpGet("campaigns/{id:int}")]
    public async Task<IActionResult> Campaign(int id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await campaigns.GetAsync(id, cancellationToken), c => c.Version);

    [HttpPost("campaigns")]
    public async Task<IActionResult> Create(NewsletterCampaignRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await campaigns.CreateAsync(request, cancellationToken), c => c.Version);

    [HttpPut("campaigns/{id:int}")]
    public async Task<IActionResult> Update(int id, NewsletterCampaignRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await campaigns.UpdateAsync(id, request, this.IfMatch(), cancellationToken), c => c.Version);

    [HttpDelete("campaigns/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await campaigns.DeleteAsync(id, this.IfMatch(), cancellationToken));

    // BR-CNT-23: chỉ xếp lịch, job gửi theo lô
    [HttpPost("campaigns/{id:int}/send")]
    public async Task<IActionResult> Send(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await campaigns.ScheduleAsync(id, this.IfMatch(), User.StaffId(), cancellationToken));
}
