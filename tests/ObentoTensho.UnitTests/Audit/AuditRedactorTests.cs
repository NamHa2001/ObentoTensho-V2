using System.Text.Json;
using ObentoTensho.Core.Audit.Domain;

namespace ObentoTensho.UnitTests.Audit;

// BR-STF-10, SEC-53
public sealed class AuditRedactorTests
{
    private static readonly string[] RecoveryCodes = ["AAAAA-BBBBB"];

    [Fact]
    public void Bi_mat_bi_an_PII_bi_che_truong_thuong_giu_nguyen()
    {
        var json = AuditRedactor.ToJson(new
        {
            fullName = "Nguyễn Văn An",
            email = "an.nguyen@example.com",
            phoneNumber = "+84912345678",
            passwordHash = "AQAAAAI...",
            securityStamp = "abc",
            inviteToken = "xyz",
            totpSecretEncrypted = new byte[] { 1, 2 },
            nested = new { recoveryCodes = RecoveryCodes, contactEmail = "b@example.com" },
        });

        var root = JsonDocument.Parse(json ?? "{}").RootElement;
        Assert.Equal("Nguyễn Văn An", root.GetProperty("fullName").GetString());
        Assert.Equal("a***@example.com", root.GetProperty("email").GetString());
        Assert.Equal("+84******678", root.GetProperty("phoneNumber").GetString());
        Assert.Equal(AuditRedactor.Hidden, root.GetProperty("passwordHash").GetString());
        Assert.Equal(AuditRedactor.Hidden, root.GetProperty("securityStamp").GetString());
        Assert.Equal(AuditRedactor.Hidden, root.GetProperty("inviteToken").GetString());
        Assert.Equal(AuditRedactor.Hidden, root.GetProperty("totpSecretEncrypted").GetString());
        Assert.Equal(AuditRedactor.Hidden, root.GetProperty("nested").GetProperty("recoveryCodes").GetString());
        Assert.Equal("b***@example.com", root.GetProperty("nested").GetProperty("contactEmail").GetString());
    }

    [Fact]
    public void Null_tra_null()
    {
        Assert.Null(AuditRedactor.ToJson(null));
    }
}
