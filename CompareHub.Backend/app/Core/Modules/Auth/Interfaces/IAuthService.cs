using CompareHub.Backend.app.Core.Modules.Auth.DTOs;

namespace CompareHub.Backend.app.Core.Modules.Auth.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
}
