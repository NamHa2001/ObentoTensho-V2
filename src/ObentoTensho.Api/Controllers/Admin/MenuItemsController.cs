using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/menu/items")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Menu)]
public sealed class MenuItemsController(MenuItemAdminService items, MenuSpreadsheetService spreadsheet) : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // Phần dư ngoài tệp cho ranh giới multipart và các trường khác
    private const long ImportRequestLimit = MenuSheet.MaxBytes + (64 * 1024);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? categoryId,
        [FromQuery] SaleStatus? status,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await items.ListAsync(new MenuItemQuery(categoryId, status, q, page, pageSize), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await items.GetAsync(id, cancellationToken), i => i.Version);

    [HttpPost]
    public async Task<IActionResult> Create(MenuItemRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await items.CreateAsync(request, cancellationToken), i => i.Version);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MenuItemRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await items.UpdateAsync(id, request, this.IfMatch(), cancellationToken), i => i.Version);

    // Bật/tắt nhanh lúc đang phục vụ (BR-MNU-16), không cần If-Match; vẫn ghi audit (BR-MNU-13)
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, MenuItemStatusRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await items.SetStatusAsync(id, request, cancellationToken), i => i.Version);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await items.DeleteAsync(id, this.IfMatch(), cancellationToken));

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken) =>
        File(await spreadsheet.ExportAsync(cancellationToken), XlsxContentType, spreadsheet.FileName("thuc-don"));

    [HttpGet("import-template")]
    public async Task<IActionResult> ImportTemplate(CancellationToken cancellationToken) =>
        File(await spreadsheet.TemplateAsync(cancellationToken), XlsxContentType, spreadsheet.FileName("mau-nhap-thuc-don"));

    // BR-MNU-14: mặc định chỉ xem trước; commit=true ghi khi mọi dòng hợp lệ
    [HttpPost("import")]
    [RequestSizeLimit(ImportRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportRequestLimit)]
    public async Task<IActionResult> Import()
    {
        // Action không có tham số: có tham số thì MVC đọc form ngay lúc binding và đổi lỗi "thân quá lớn"
        // thành 400 chung chung. Tự đọc form ở đây để trả đúng 413 FILE_TOO_LARGE.
        var cancellationToken = HttpContext.RequestAborted;
        var commit = string.Equals(Request.Query["commit"], "true", StringComparison.OrdinalIgnoreCase);
        if (Request.ContentLength > ImportRequestLimit)
        {
            return ApiProblems.Create(HttpContext, ErrorCodes.FileTooLarge).ToActionResult();
        }

        if (!Request.HasFormContentType || (await Request.ReadFormAsync(cancellationToken)).Files.GetFile("file") is not { } file)
        {
            return ApiProblems.Create(HttpContext, ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal) { ["file"] = [FieldErrorCodes.Required] }).ToActionResult();
        }

        await using var content = file.OpenReadStream();
        return this.DataOrProblem(await spreadsheet.ImportAsync(content, commit, cancellationToken));
    }

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await items.ReorderAsync(request.Ids, cancellationToken));
}
