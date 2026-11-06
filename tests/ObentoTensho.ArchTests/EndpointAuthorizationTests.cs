using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;

namespace ObentoTensho.ArchTests;

// AR-09, SEC-12: mỗi action khai báo quyền tường minh; API quản trị chỉ dùng quyền theo ma trận
public sealed class EndpointAuthorizationTests
{
    private const string AdminNamespace = "ObentoTensho.Api.Controllers.Admin";

    public static TheoryData<string> Actions()
    {
        var data = new TheoryData<string>();
        foreach (var action in AllActions())
        {
            data.Add($"{action.DeclaringType?.FullName}.{action.Name}");
        }

        return data;
    }

    [Fact]
    public void Co_controller_de_kiem()
    {
        Assert.NotEmpty(AllActions());
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public void AR_09_Moi_action_co_AllowAnonymous_hoac_Authorize(string action)
    {
        var method = Find(action);
        Assert.True(IsAnonymous(method) || Authorizations(method).Any(), $"{action} thiếu [AllowAnonymous]/[Authorize]");
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public void SEC_12_Action_quan_tri_can_dang_nhap_phai_dung_RequireCapability(string action)
    {
        var method = Find(action);
        if (method.DeclaringType?.Namespace?.StartsWith(AdminNamespace, StringComparison.Ordinal) != true || IsAnonymous(method))
        {
            return;
        }

        Assert.All(Authorizations(method), a => Assert.IsType<RequireCapabilityAttribute>(a));
    }

    private static IEnumerable<MethodInfo> AllActions() =>
        typeof(RequireCapabilityAttribute).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

    private static MethodInfo Find(string action) =>
        AllActions().Single(m => string.Equals($"{m.DeclaringType?.FullName}.{m.Name}", action, StringComparison.Ordinal));

    private static bool IsAnonymous(MethodInfo method) =>
        method.GetCustomAttribute<AllowAnonymousAttribute>() is not null
        || method.DeclaringType?.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

    private static IEnumerable<AuthorizeAttribute> Authorizations(MethodInfo method) =>
        method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(method.DeclaringType?.GetCustomAttributes<AuthorizeAttribute>(inherit: true) ?? []);
}
