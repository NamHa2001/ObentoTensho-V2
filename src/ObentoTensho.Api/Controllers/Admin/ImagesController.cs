using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Media;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Media.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/04 §2: tải ảnh lên, các nội dung tham chiếu bằng mã ảnh trả về (BR-CNT-30)
[ApiController]
[Route("api/v1/admin/images")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Content)]
public sealed class ImagesController(ImageService images) : ControllerBase
{
    // Phần dư ngoài tệp cho ranh giới multipart và trường altText
    private const long UploadRequestLimit = MediaRules.MaxUploadBytes + (64 * 1024);

    // Action không có tham số để tự đọc form: xem ghi chú ở MenuItemsController.Import
    [HttpPost]
    [RequestSizeLimit(UploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<IActionResult> Upload()
    {
        var cancellationToken = HttpContext.RequestAborted;
        if (Request.ContentLength > UploadRequestLimit)
        {
            return ApiProblems.Create(HttpContext, ErrorCodes.FileTooLarge).ToActionResult();
        }

        var form = Request.HasFormContentType ? await Request.ReadFormAsync(cancellationToken) : null;
        if (form?.Files.GetFile("file") is not { } file)
        {
            return ApiProblems.Create(HttpContext, ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal) { ["file"] = [FieldErrorCodes.Required] }).ToActionResult();
        }

        await using var content = file.OpenReadStream();
        return this.CreatedOrProblem(await images.UploadAsync(content, form["altText"], User.StaffId(), cancellationToken));
    }

    // FR-IMG-08: hết lượt tự thử lại thì người quản trị bấm "Thử lại"
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await images.RetryAsync(id, cancellationToken));

    // FR-IMG-03/04: chỉ SuperAdmin — sinh lại biến thể mọi ảnh sau khi đổi cấu hình; chạy nền, ảnh vẫn hiện bộ cũ tới khi xong
    [HttpPost("regenerate")]
    [RequireCapability(StaffCapability.StaffAndAudit)]
    public async Task<IActionResult> Regenerate(CancellationToken cancellationToken) =>
        Accepted(new ApiResponse<RegenerateImagesResponse>(new RegenerateImagesResponse(await images.RequestRegenerationAsync(cancellationToken))));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await images.GetAsync(id, cancellationToken));
}
