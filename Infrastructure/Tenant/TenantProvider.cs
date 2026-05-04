using CheckAccess.Infrastructure.Auth;

namespace CheckTrip.Web.Infrastructure.Tenant;

public class TenantProvider : ITenantProvider
{
    private readonly ICurrentUserService _currentUser;

    public TenantProvider(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public Guid GetTenantId()
    {
        return _currentUser.TenantId
            ?? Guid.Parse("C8BA16FF-031E-44F3-B86B-A87A8F10FC9E");
    }
}