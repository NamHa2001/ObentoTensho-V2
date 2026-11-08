using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ObentoTensho.Api.Logging;
using ObentoTensho.Core.Time;

namespace ObentoTensho.IntegrationTests.Logging;

// SEC-53: log không chứa bí mật/PII nguyên văn và không chèn được dòng giả
public sealed class LogRedactionTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("NewPassword")]
    [InlineData("refreshToken")]
    [InlineData("Authorization")]
    [InlineData("ticket")]
    [InlineData("otpCode")]
    [InlineData("Cookie")]
    [InlineData("clientSecret")]
    [InlineData("securityStamp")]
    public void Khoa_bi_mat_an_ca_gia_tri(string key) =>
        Assert.Equal(LogRedactor.Hidden, LogRedactor.Value(key, "gia-tri-that-123"));

    [Theory]
    [InlineData("GET /hubs/v1/staff?ticket=abc.DEF-123&x=1", "GET /hubs/v1/staff?ticket=***&x=1")]
    [InlineData("/auth/reset-password#token=QWE%2Brty", "/auth/reset-password#token=***")]
    [InlineData("/x?a=1&access_token=eyJhbGciOi", "/x?a=1&access_token=***")]
    [InlineData("/x?CODE=123456", "/x?CODE=***")]
    [InlineData("Authorization: Bearer eyJ.abc-def_ghi", "Authorization: Bearer ***")]
    [InlineData("Khách khach.an@example.com đặt bàn", "Khách k***@example.com đặt bàn")]
    [InlineData("SĐT 0912345678 gọi", "SĐT 091****678 gọi")]
    [InlineData("SĐT +84912345678.", "SĐT +84******678.")]
    [InlineData("dòng 1\r\n[Error] dòng giả", "dòng 1  [Error] dòng giả")]
    [InlineData("tab\tvà\u0000null", "tab và null")]
    public void Che_theo_mau_trong_chuoi_tu_do(string input, string expected) =>
        Assert.Equal(expected, LogRedactor.Scrub(input));

    [Theory]
    [InlineData("Đơn OBT-TK-20261108-0001 tổng 1650000")]
    [InlineData("Mã băm 0912345678901234567890abcdef")]
    [InlineData("Số 10912345678")]
    [InlineData("/menu?page=2&category=com")]
    public void Khong_che_nham_du_lieu_thuong(string input) =>
        Assert.Equal(input, LogRedactor.Scrub(input));

    [Fact]
    public void Formatter_ghi_mot_dong_JSON_da_che()
    {
        var formatter = new RedactingJsonConsoleFormatter(new FixedClock());
        var state = new List<KeyValuePair<string, object?>>
        {
            new("Email", "khach.an@example.com"),
            new("Token", "bi-mat"),
            new("Count", 3),
            new("{OriginalFormat}", "Khách {Email} gửi {Token} lần {Count}"),
        };
        using var output = new StringWriter();
        var entry = new LogEntry<IReadOnlyList<KeyValuePair<string, object?>>>(
            LogLevel.Warning,
            "Thu.Nghiem",
            new EventId(7),
            state,
            new InvalidOperationException("Lỗi với 0912345678\nDòng giả"),
            (_, _) => "Khách khach.an@example.com gửi bi-mat lần 3\n[Critical] giả");

        formatter.Write(entry, scopeProvider: null, output);

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var json = JsonDocument.Parse(Assert.Single(lines)).RootElement;
        Assert.Equal("Warning", json.GetProperty("level").GetString());
        Assert.Equal("Thu.Nghiem", json.GetProperty("category").GetString());
        Assert.Equal(7, json.GetProperty("eventId").GetInt32());
        Assert.Equal("2026-11-08T05:00:00.0000000Z", json.GetProperty("timestamp").GetString());
        Assert.Equal("Khách k***@example.com gửi bi-mat lần 3 [Critical] giả", json.GetProperty("message").GetString());
        var fields = json.GetProperty("state");
        Assert.Equal("k***@example.com", fields.GetProperty("Email").GetString());
        Assert.Equal(LogRedactor.Hidden, fields.GetProperty("Token").GetString());
        Assert.Equal("3", fields.GetProperty("Count").GetString());
        Assert.False(fields.TryGetProperty("{OriginalFormat}", out _));
        var exception = json.GetProperty("exception").GetString()!;
        Assert.Contains("091****678", exception, StringComparison.Ordinal);
        Assert.DoesNotContain("0912345678", exception, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', exception);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 11, 8, 5, 0, 0, DateTimeKind.Utc);

        public DateOnly TodayLocal => DateOnly.FromDateTime(UtcNow.AddHours(7));

        public DateTime ToLocal(DateTime utc) => utc.AddHours(7);

        public DateTime ToUtc(DateOnly localDate, TimeOnly localTime) => localDate.ToDateTime(localTime).AddHours(-7);
    }
}
