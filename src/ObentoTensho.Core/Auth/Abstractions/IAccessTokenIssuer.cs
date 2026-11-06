using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Auth.Abstractions;

public interface IAccessTokenIssuer
{
    AccessToken IssueForCustomer(Guid userId, string securityStamp);

    AccessToken IssueForStaff(Guid staffId, StaffRole role, string securityStamp);
}
