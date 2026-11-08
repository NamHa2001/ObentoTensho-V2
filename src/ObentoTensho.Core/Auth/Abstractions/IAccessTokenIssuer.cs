using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Auth.Abstractions;

public interface IAccessTokenIssuer
{
    // sessionId = FamilyId của refresh token, để thu hồi được access token theo phiên
    AccessToken IssueForCustomer(Guid userId, string securityStamp, Guid sessionId);

    AccessToken IssueForStaff(Guid staffId, StaffRole role, string securityStamp, Guid sessionId);
}
