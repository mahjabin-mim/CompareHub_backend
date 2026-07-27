namespace CompareHub.Backend.app.Core.Infrastructure.Auth;

public interface ICurrentUserService
{
    Guid GetUserId();
    string GetEmail();
}
