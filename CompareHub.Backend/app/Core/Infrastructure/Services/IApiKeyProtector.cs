namespace CompareHub.Backend.app.Core.Infrastructure.Services;

public interface IApiKeyProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedText);
}
