using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Reservations.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/11 §8 (admin): Reception, Manager, SuperAdmin (docs/srs/01 §3)
[ApiController]
[Route("api/v1/admin/reservations")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Reservations)]
public sealed class AdminReservationsController(ReservationDeskService desk, ReservationStaffService staff) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] ReservationStatus? status,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await desk.ListAsync(new ReservationQuery(from, to, status, q, page, pageSize), cancellationToken));

    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.CalendarAsync(from, to, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(StaffCreateReservationRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await staff.CreateAsync(request, User.StaffId(), User.StaffRole(), cancellationToken));

    [HttpPost("{id:guid}/reveal-contact")]
    public async Task<IActionResult> RevealContact(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.RevealContactAsync(id, User.StaffId(), cancellationToken));

    [HttpPost("{id:guid}/call-attempts")]
    public async Task<IActionResult> RecordCallAttempt(Guid id, ReservationNoteRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.RecordCallAttemptAsync(id, request.Note, cancellationToken));

    [HttpPost("{id:guid}/contact-confirmation")]
    public async Task<IActionResult> ConfirmContact(Guid id, ReservationNoteRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.ConfirmContactAsync(id, User.StaffId(), request.Note, cancellationToken));

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeReservationStatusRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.ChangeStatusAsync(id, request, User.StaffId(), cancellationToken));

    [HttpPatch("{id:guid}/assign-table")]
    public async Task<IActionResult> AssignTable(Guid id, AssignTableRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await staff.AssignTableAsync(id, request.TableId, cancellationToken));

    [HttpPatch("{id:guid}/note")]
    public async Task<IActionResult> SetNote(Guid id, ReservationNoteRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.SetInternalNoteAsync(id, request.Note, cancellationToken));
}
