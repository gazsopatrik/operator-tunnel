using System.Text;
using OperatorTunnel.Core.Profiles;
using OperatorTunnel.Core.Security;

namespace OperatorTunnel.Core.Tests.Security;

public sealed class EncryptedProfileStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsThroughEncryptedFile()
    {
        var root = CreateTestDirectory();
        try
        {
            var profile = CreateProfile();
            var store = new EncryptedProfileStore(new DpapiSecretProtector(), root);

            await store.SaveAsync(profile);
            var restored = await store.LoadAsync("demo");

            Assert.Equal(profile.Name, restored.Name);
            Assert.Equal(profile.InterfaceAddress, restored.InterfaceAddress);
            Assert.Equal(profile.PrivateKey, restored.PrivateKey);
            Assert.Equal(profile.Peers[0].AllowedIps, restored.Peers[0].AllowedIps);

            var storedBytes = await File.ReadAllBytesAsync(Path.Combine(root, "demo.otp"));
            Assert.False(ContainsSubsequence(storedBytes, Encoding.UTF8.GetBytes(profile.PrivateKey)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PathTraversalProfileName_IsRejected()
    {
        var root = CreateTestDirectory();
        try
        {
            var store = new EncryptedProfileStore(new DpapiSecretProtector(), root);
            var profile = CreateProfile() with { Name = "..\\escape" };

            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(profile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ListAsync_ReturnsStoredProfileNamesOnly()
    {
        var root = CreateTestDirectory();
        try
        {
            var store = new EncryptedProfileStore(new DpapiSecretProtector(), root);
            await store.SaveAsync(CreateProfile());
            await File.WriteAllTextAsync(Path.Combine(root, "ignored.txt"), "not a profile");

            var profiles = await store.ListAsync();

            Assert.Equal(["demo"], profiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static WireGuardProfile CreateProfile() => new(
        "demo",
        "10.77.0.2/32",
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
        ["10.77.0.1"],
        [new WireGuardPeer("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB=", "gateway.example.test:51820", ["0.0.0.0/0"], 25, null)]);

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OperatorTunnelTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool ContainsSubsequence(byte[] source, byte[] candidate)
    {
        for (var index = 0; index <= source.Length - candidate.Length; index++)
        {
            if (source.AsSpan(index, candidate.Length).SequenceEqual(candidate))
                return true;
        }

        return false;
    }
}
