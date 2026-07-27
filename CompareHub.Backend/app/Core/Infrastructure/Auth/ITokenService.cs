using CompareHub.Backend.app.Core.Domain.Entities;

namespace CompareHub.Backend.app.Core.Infrastructure.Auth;

public interface ITokenService
{
    string GenerateToken(AppUser user);
}
