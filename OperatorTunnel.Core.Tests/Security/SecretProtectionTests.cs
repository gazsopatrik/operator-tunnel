using System.Text;
using System.Security.Cryptography;
using OperatorTunnel.Core.Security;

namespace OperatorTunnel.Core.Tests.Security;

public sealed class SecretProtectionTests
{
    [Fact]
    public void DpapiRoundTrip_RestoresPlaintextWithoutMatchingCiphertext()
    {
        var protector = new DpapiSecretProtector();
        var plaintext = Encoding.UTF8.GetBytes("test-only secret");

        var protectedData = protector.Protect(plaintext);
        var restored = protector.Unprotect(protectedData);

        Assert.NotEqual(plaintext, protectedData);
        Assert.Equal(plaintext, restored);
        CryptographicOperations.ZeroMemory(plaintext);
        CryptographicOperations.ZeroMemory(restored);
    }
}
