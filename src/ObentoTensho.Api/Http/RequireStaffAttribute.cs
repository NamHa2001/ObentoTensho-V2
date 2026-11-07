using Microsoft.AspNetCore.Authorization;
using ObentoTensho.Api.Configuration;

namespace ObentoTensho.Api.Http;

/// <summary>Thao tác trên chính tài khoản của mình: mọi nhân viên đã đăng nhập, không phụ thuộc ma trận quyền.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireStaffAttribute : AuthorizeAttribute
{
    public RequireStaffAttribute() => AuthenticationSchemes = AuthSchemes.Staff;
}
