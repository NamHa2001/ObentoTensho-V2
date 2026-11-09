using FluentValidation;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;

namespace ObentoTensho.UnitTests.Common;

// Tên có dấu < > bị từ chối ngay ở đầu vào, tên tiếng Việt bình thường vẫn qua
public sealed class TextRulesTests
{
    private sealed class NameValidator : AbstractValidator<NameHolder>
    {
        public NameValidator() => RuleFor(v => v.Name).NoMarkup();
    }

    private sealed record NameHolder(string Name);

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("Nam <img src=x onerror=alert(1)>")]
    [InlineData("a>b")]
    public void Ten_co_the_HTML_bi_tu_choi(string name)
    {
        var result = new NameValidator().Validate(new NameHolder(name));

        Assert.Contains(result.Errors, e => string.Equals(e.ErrorCode, FieldErrorCodes.InvalidFormat, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Nguyễn Thị Minh Khai")]
    [InlineData("O'Brien - Trần")]
    public void Ten_binh_thuong_hop_le(string name)
    {
        Assert.True(new NameValidator().Validate(new NameHolder(name)).IsValid);
    }
}
