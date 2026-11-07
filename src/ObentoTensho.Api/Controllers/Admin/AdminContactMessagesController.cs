using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/contact-messages")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Feedback)]
public sealed class AdminContactMessagesController(ContactService contacts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await contacts.ListAsync(unreadOnly, page, pageSize, cancellationToken));

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await contacts.MarkReadAsync(id, User.StaffId(), cancellationToken));

    [HttpPost("{id:int}/replied")]
    public async Task<IActionResult> Replied(int id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await contacts.MarkRepliedAsync(id, User.StaffId(), cancellationToken));
}
