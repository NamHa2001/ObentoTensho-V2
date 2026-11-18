using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Contract;

// Kiểu TypeScript của web sinh từ web/libs/ot-api/openapi.json (CI kiểm schema.d.ts khớp tệp đó).
// Test này giữ đầu còn lại: tệp phải khớp tài liệu backend sinh ra thật, sửa DTO mà quên xuất lại là đỏ.
// Xuất lại: đặt OBENTO_UPDATE_OPENAPI=1 rồi chạy test này, sau đó `npm run api:types` trong web/.
[Collection(SqlServerFixture.CollectionName)]
public sealed class OpenApiContractTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Tai_lieu_API_trong_web_khop_backend()
    {
        // OpenAPI chỉ bật ở môi trường dev (cùng cấu hình JSON mô tả enum chuỗi, số là số).
        // Bộ test tự thêm vài controller thử phân quyền; chúng không thuộc API thật nên không vào tài liệu.
        using var factory = new ApiFactory(
            fixture.ConnectionString,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["environment"] = "Development" },
            s => s.Configure<OpenApiOptions>("v1", o =>
            {
                var include = o.ShouldInclude;
                o.ShouldInclude = d => d.ActionDescriptor is not ControllerActionDescriptor c || c.ControllerTypeInfo.Assembly != typeof(ApiFactory).Assembly ? include(d) : false;
            }));
        using var http = factory.CreateApiClient();
        var response = await http.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = Normalize(JsonNode.Parse(await response.Content.ReadAsStringAsync()));
        var file = Path.Combine(RepoRoot(), "web", "libs", "ot-api", "openapi.json");

        if (string.Equals(Environment.GetEnvironmentVariable("OBENTO_UPDATE_OPENAPI"), "1", StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(file, actual + "\n");
            return;
        }

        var expected = Normalize(JsonNode.Parse(await File.ReadAllTextAsync(file)));
        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            "web/libs/ot-api/openapi.json lệch với API" + FirstDifference(expected, actual)
            + ". Xuất lại: OBENTO_UPDATE_OPENAPI=1 dotnet test --filter OpenApiContractTests, rồi `npm run api:types` trong web/.");
    }

    private static string FirstDifference(string expected, string actual)
    {
        var a = expected.Split('\n');
        var b = actual.Split('\n');
        var i = 0;
        while (i < a.Length && i < b.Length && string.Equals(a[i], b[i], StringComparison.Ordinal))
        {
            i++;
        }

        static string At(string[] lines, int from) => string.Join(' ', lines.Skip(from).Take(2).Select(l => l.Trim()));
        return $" từ dòng {i + 1}: tệp có `{At(a, i)}`, API có `{At(b, i)}`";
    }

    // Bỏ địa chỉ máy chủ (khác nhau giữa máy dev và test), giữ nguyên phần còn lại
    private static string Normalize(JsonNode? doc)
    {
        var root = Assert.IsType<JsonObject>(doc);
        root.Remove("servers");
        return root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, NewLine = "\n", Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObentoTensho.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc repo");
    }
}
