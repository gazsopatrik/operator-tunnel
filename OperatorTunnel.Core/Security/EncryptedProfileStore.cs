using System.Security.Cryptography;
using System.Text;
using OperatorTunnel.Core.Profiles;

namespace OperatorTunnel.Core.Security;

public sealed class EncryptedProfileStore
{
    private static readonly byte[] Header = "OPERATOR-TUNNEL-PROFILE-V1\0"u8.ToArray();
    private readonly ISecretProtector _protector;
    private readonly string _rootDirectory;
    private readonly WireGuardConfigSerializer _serializer = new();
    private readonly WireGuardConfigParser _parser = new();

    public EncryptedProfileStore(ISecretProtector protector, string? rootDirectory = null)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _rootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OperatorTunnel",
            "profiles");
    }

    public async Task SaveAsync(WireGuardProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var fileName = SanitizeProfileName(profile.Name) + ".otp";
        var targetPath = GetSafePath(fileName);
        Directory.CreateDirectory(_rootDirectory);

        var config = _serializer.Serialize(profile);
        var plaintext = Encoding.UTF8.GetBytes(config);
        var protectedData = _protector.Protect(plaintext);
        var payload = new byte[Header.Length + protectedData.Length];
        Buffer.BlockCopy(Header, 0, payload, 0, Header.Length);
        Buffer.BlockCopy(protectedData, 0, payload, Header.Length, protectedData.Length);

        var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, payload, cancellationToken);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(protectedData);
            CryptographicOperations.ZeroMemory(payload);
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async Task<WireGuardProfile> LoadAsync(string profileName, CancellationToken cancellationToken = default)
    {
        var path = GetSafePath(SanitizeProfileName(profileName) + ".otp");
        var stored = await File.ReadAllBytesAsync(path, cancellationToken);
        if (stored.Length <= Header.Length || !stored.AsSpan(0, Header.Length).SequenceEqual(Header))
            throw new InvalidDataException("The stored profile format is invalid.");

        var protectedData = stored.AsSpan(Header.Length).ToArray();
        var plaintext = _protector.Unprotect(protectedData);
        try
        {
            var config = Encoding.UTF8.GetString(plaintext);
            var parsed = _parser.Parse(config, SanitizeProfileName(profileName));
            if (!parsed.IsValid)
                throw new InvalidDataException("The stored profile failed configuration validation.");
            return parsed.Profile!;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(stored);
            CryptographicOperations.ZeroMemory(protectedData);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private string GetSafePath(string fileName)
    {
        var fullRoot = Path.GetFullPath(_rootDirectory);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, fileName));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Profile path escaped the profile store directory.");
        return fullPath;
    }

    private static string SanitizeProfileName(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        if (profileName.Length > 64 || profileName.Any(character =>
                !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
            throw new ArgumentException("Profile name contains unsupported characters.", nameof(profileName));
        return profileName;
    }
}

