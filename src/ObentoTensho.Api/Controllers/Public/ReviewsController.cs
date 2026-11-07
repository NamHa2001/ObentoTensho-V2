using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Content.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/17 §3.4: xem công khai đánh giá đã duyệt; thành viên viết, sửa, xóa đánh giá của mình
[ApiController]
[Route("api/v1/reviews")]
public sealed class ReviewsController(ReviewService reviews) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(
        [FromQuery] int? menuItemId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await reviews.ListPublicAsync(menuItemId, page, pageSize, cancellationToken));

    [HttpGet("mine")]
    [RequireCustomer]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        this.Data(await reviews.ListMineAsync(CustomerId(), cancellationToken));

    [HttpPost]
    [RequireCustomer]
    public async Task<IActionResult> Create(CreateReviewRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await reviews.CreateAsync(CustomerId(), request, cancellationToken));

    [HttpPut("{id:int}")]
    [RequireCustomer]
    public async Task<IActionResult> Update(int id, UpdateReviewRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await reviews.UpdateAsync(CustomerId(), id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [RequireCustomer]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await reviews.DeleteAsync(CustomerId(), id, cancellationToken));

    private Guid CustomerId() => User.CustomerId() ?? throw new InvalidOperationException("Thiếu định danh khách sau khi xác thực.");
}
