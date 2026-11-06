namespace ObentoTensho.Core.Auth.Abstractions;

public interface IAccessTokenIssuer
{
    AccessToken IssueForCustomer(Guid userId, string securityStamp);
}
