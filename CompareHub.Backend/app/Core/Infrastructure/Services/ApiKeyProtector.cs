using Microsoft.AspNetCore.DataProtection;

namespace CompareHub.Backend.app.Core.Infrastructure.Services;

public class ApiKeyProtector : IApiKeyProtector
{
    private readonly IDataProtector _protector;

    public ApiKeyProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("CompareHub.ProductSources.ApiKey");
    }

    public string Protect(string plaintext)
        => string.IsNullOrWhiteSpace(plaintext) ? string.Empty : _protector.Protect(plaintext);

    public string Unprotect(string protectedText)
        => string.IsNullOrWhiteSpace(protectedText) ? string.Empty : _protector.Unprotect(protectedText);
}
