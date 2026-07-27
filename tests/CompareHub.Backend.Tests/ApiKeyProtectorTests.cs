using Microsoft.AspNetCore.DataProtection;
using CompareHub.Backend.app.Core.Infrastructure.Services;

namespace CompareHub.Backend.Tests;

public class ApiKeyProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_ReturnsOriginalValue()
    {
        var provider = DataProtectionProvider.Create("CompareHubTests");
        var protector = new ApiKeyProtector(provider);
        var original = "test-api-key";

        var protectedText = protector.Protect(original);
        var unprotected = protector.Unprotect(protectedText);

        Assert.Equal(original, unprotected);
    }
}
