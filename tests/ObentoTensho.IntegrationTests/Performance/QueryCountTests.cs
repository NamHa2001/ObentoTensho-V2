using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;
using ObentoTensho.IntegrationTests.Reservations;

namespace ObentoTensho.IntegrationTests.Performance;

// PERF-51, TST-28: chặn hồi quy N+1 ở endpoint nóng bằng cách đếm câu lệnh SQL thật của một yêu cầu.
// Số câu lệnh không được tăng theo lượng dữ liệu (số món, số dòng đơn) và không vượt trần đo được hiện nay —
// thêm một truy vấn mới vào đường nóng là test đỏ, phải xem lại có cần không
[Collection(SqlServerFixture.CollectionName)]
public sealed class QueryCountTests : IDisposable
{
    // Thực đơn dựng lại một lần sau mỗi lần sửa rồi cache: mỗi bảng một truy vấn (danh mục, món, nhóm, tùy chọn,
    // liên kết nhóm, 4 bảng dịch, ảnh món, ảnh). Yêu cầu khi đã có cache: ≤ 2 như PERF-51
    private const int MenuColdLimit = 11;
    private const int MenuWarmLimit = 2;

    // Giờ mở cửa, giờ đặc biệt, ngày khóa, ca, sức chứa theo ngày
    private const int AvailabilityLimit = 5;

    // 3 bảng lịch + 4 bảng thực đơn (giá luôn đọc từ CSDL, BR-ORD-22) + giới hạn + số thứ tự + ghi đơn/dòng/tùy chọn/lịch sử/hộp thư
    private const int GuestOrderLimit = 13;

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;

    public QueryCountTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _factory.Server.PreserveExecutionContext = true;
        _http = _factory.CreateApiClient();
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private static async Task<int> CountAsync(Func<Task<HttpResponseMessage>> send, HttpStatusCode expected, int limit)
    {
        using var counter = QueryCounter.Begin();
        var response = await send();
        Assert.Equal(expected, response.StatusCode);
        Assert.True(counter.Count <= limit, $"{counter.Count} câu lệnh (giới hạn {limit}):{Environment.NewLine}{counter.Describe()}");
        return counter.Count;
    }

    private async Task<int> ColdMenuAsync()
    {
        _factory.Services.GetRequiredService<IMenuCache>().Invalidate();
        return await CountAsync(() => _http.GetAsync("/api/v1/menu/items"), HttpStatusCode.OK, MenuColdLimit);
    }

    [Fact]
    public async Task Thuc_don_cong_khai_so_truy_van_co_dinh_va_co_cache()
    {
        await OrderTestMenu.CreateAsync(_fixture);
        var cold = await ColdMenuAsync();
        Assert.True(cold > 0, "Lần đầu phải đọc CSDL — bộ đếm không hoạt động?");
        await CountAsync(() => _http.GetAsync("/api/v1/menu/items?page=1"), HttpStatusCode.OK, MenuWarmLimit);

        // Thêm cả một thực đơn nữa (món, nhóm, tùy chọn): số truy vấn không đổi
        await OrderTestMenu.CreateAsync(_fixture);
        await OrderTestMenu.CreateAsync(_fixture);
        Assert.Equal(cold, await ColdMenuAsync());
    }

    [Fact]
    public async Task Cho_trong_dat_ban_so_truy_van_co_dinh()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var url = $"/api/v1/reservations/availability?date={ReservationApiClient.Date(TestSchedule.Tomorrow)}";
        var first = await CountAsync(() => _http.GetAsync(url), HttpStatusCode.OK, AvailabilityLimit);

        // Có đặt bàn trong ngày cũng không đọc thêm
        Assert.Equal(HttpStatusCode.Created, (await new ReservationApiClient(_http).CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow))).StatusCode);
        Assert.Equal(first, await CountAsync(() => _http.GetAsync(url), HttpStatusCode.OK, AvailabilityLimit));
    }

    [Fact]
    public async Task Dat_mon_vang_lai_so_truy_van_khong_tang_theo_so_dong()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var orders = new OrderApiClient(_http);

        // Lần đầu làm nóng cache cài đặt; đo các lần sau như lúc chạy thật
        Assert.Equal(HttpStatusCode.Created, (await orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small)], email: AuthApiClient.NewEmail()))).StatusCode);
        var oneLine = await CountAsync(
            () => orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small)], email: AuthApiClient.NewEmail())),
            HttpStatusCode.Created,
            GuestOrderLimit);
        var manyLines = await CountAsync(
            () => orders.CreateAsync(OrderApiClient.Order(
                [
                    OrderApiClient.Line(menu.Rice, 2, menu.Large),
                    OrderApiClient.Line(menu.Rice, 1, menu.Small),
                    OrderApiClient.Line(menu.Tea, 1, menu.Egg),
                    OrderApiClient.Line(menu.Tea, 3),
                ],
                email: AuthApiClient.NewEmail())),
            HttpStatusCode.Created,
            GuestOrderLimit);
        Assert.Equal(oneLine, manyLines);
    }
}
