using FluentValidation;
using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Infrastructure.Auth;
using CompareHub.Backend.app.Core.Modules.Auth.DTOs;
using CompareHub.Backend.app.Core.Modules.Auth.Interfaces;
using CompareHub.Backend.app.Core.Modules.Auth.Specifications;
using CompareHub.Backend.app.Core.Shared.Contracts;

namespace CompareHub.Backend.app.Core.Modules.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IRepository<AppUser> _users;
    private readonly ITokenService _tokenService;

    public AuthService(IRepository<AppUser> users, ITokenService tokenService)
    {
        _users = users;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var existing = await _users.FirstOrDefaultAsync(new AppUserByEmailSpecification(request.Email), cancellationToken);
        if (existing is not null)
        {
            throw new ValidationException("Email is already registered.");
        }

        var user = new AppUser
        {
            FullName = request.FullName,
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        await _users.AddAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto(user.Id, user.Email, _tokenService.GenerateToken(user));
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var user = await _users.FirstOrDefaultAsync(new AppUserByEmailSpecification(request.Email.Trim().ToLowerInvariant()), cancellationToken)
            ?? throw new ValidationException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new ValidationException("Invalid credentials.");
        }

        return new AuthResponseDto(user.Id, user.Email, _tokenService.GenerateToken(user));
    }
}
