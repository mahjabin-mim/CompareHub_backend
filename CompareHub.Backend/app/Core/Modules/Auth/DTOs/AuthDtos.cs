namespace CompareHub.Backend.app.Core.Modules.Auth.DTOs;

public record RegisterRequestDto(string FullName, string Email, string Password);
public record LoginRequestDto(string Email, string Password);
public record AuthResponseDto(Guid UserId, string Email, string Token);
