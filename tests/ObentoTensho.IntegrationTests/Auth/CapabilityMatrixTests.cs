using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Auth;

// SEC-12 khi chạy thật: nhân viên mở F12 gọi thẳng API của quyền khác phải nhận 403, với mọi API quản trị.
// Test kiến trúc chỉ kiểm có gắn RequireCapability; test này kiểm máy chủ thực sự chặn theo ma trận quyền
[Collection(SqlServerFixture.CollectionName)]
public sealed class CapabilityMatrixTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public CapabilityMatrixTests(SqlServerFixture fixture)
    {
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Moi_API_quan_tri_tu_choi_vai_tro_khong_co_quyen()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (Endpoint: e, Capability: e.Metadata.GetMetadata<RequireCapabilityAttribute>()?.Capability, Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods))
            .Where(e => e.Capability is not null && e.Methods is { Count: > 0 })
            .ToList();
        Assert.NotEmpty(endpoints);

        var tokens = new Dictionary<StaffRole, string>();
        var failures = new List<string>();
        foreach (var (endpoint, capability, methods) in endpoints)
        {
            var denied = Enum.GetValues<StaffRole>().FirstOrDefault(r => !StaffCapabilities.Has(r, capability!.Value), StaffRole.SuperAdmin);
            if (StaffCapabilities.Has(denied, capability!.Value))
            {
                continue;
            }

            if (!tokens.TryGetValue(denied, out var token))
            {
                token = await _admin.SignInAsync(denied);
                tokens[denied] = token;
            }

            var path = "/" + Fill(endpoint.RoutePattern);
            using var request = new HttpRequestMessage(new HttpMethod(methods![0]), path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Origin", ApiFactory.AdminOrigin);
            if (!HttpMethods.IsGet(methods[0]) && !HttpMethods.IsDelete(methods[0]))
            {
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            }

            using var response = await _http.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Forbidden)
            {
                failures.Add($"{methods[0]} {path} ({capability}, vai trò {denied}): {(int)response.StatusCode}");
            }
        }

        Assert.True(failures.Count == 0, "API không chặn đúng vai trò:\n" + string.Join('\n', failures));
    }

    // Điền tham số đường dẫn bằng giá trị đúng kiểu để request đi tới được bước kiểm quyền
    private static string Fill(RoutePattern pattern) =>
        string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separator => separator.Content,
            RoutePatternParameterPart parameter => Sample(parameter),
            _ => string.Empty,
        }))));

    private static string Sample(RoutePatternParameterPart parameter)
    {
        var constraints = parameter.ParameterPolicies.Select(p => p.Content ?? string.Empty).ToList();

        // Ràng buộc dạng regex(^(a|b)$): lấy lựa chọn đầu tiên để khớp được đường dẫn
        if (constraints.FirstOrDefault(c => c.StartsWith("regex(", StringComparison.Ordinal)) is { } regex)
        {
            var open = regex.IndexOf("^(", StringComparison.Ordinal);
            var end = regex.IndexOfAny(['|', ')'], open + 2);
            if (open >= 0 && end > open)
            {
                return regex[(open + 2)..end];
            }
        }

        if (constraints.Contains("guid", StringComparer.Ordinal))
        {
            return Guid.NewGuid().ToString();
        }

        if (constraints.Any(c => c is "int" or "long"))
        {
            return "1";
        }

        return parameter.Name.Contains("date", StringComparison.OrdinalIgnoreCase) ? "2026-11-10" : "x";
    }
}
