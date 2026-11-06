using Microsoft.AspNetCore.Authorization;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Api.Http;

/// <summary>Endpoint quản trị: chỉ nhận access token nhân viên có vai trò được ma trận quyền cho phép (SEC-12).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireCapabilityAttribute : AuthorizeAttribute
{
    public RequireCapabilityAttribute(StaffCapability capability)
    {
        Capability = capability;
        Policy = AuthorizationPolicies.For(capability);
        AuthenticationSchemes = AuthSchemes.Staff;
    }

    public StaffCapability Capability { get; }
}
