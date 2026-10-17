using System.Security.Cryptography;
using System.Text.Json;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

public sealed class DpapiCredentialStore(string directory) : ICredentialStore
{
    private readonly string _directory = Path.GetFullPath(directory);

    public async Task SaveAsync(string key, StoredCredentials credentials, CancellationToken cancellationToken = default)
    {
        var file = GetPath(key);
        Directory.CreateDirectory(_directory);
        var plain = JsonSerializer.SerializeToUtf8Bytes(credentials);
        var entropy = System.Text.Encoding.UTF8.GetBytes(key);
        byte[]? protectedBytes = null;
        var temp = file + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            protectedBytes = ProtectedData.Protect(plain, entropy, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(temp, protectedBytes, cancellationToken);
            File.Move(temp, file, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(entropy);
            if (protectedBytes is not null) CryptographicOperations.ZeroMemory(protectedBytes);
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public async Task<StoredCredentials?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var file = GetPath(key);
        if (!File.Exists(file)) return null;
        var protectedBytes = await File.ReadAllBytesAsync(file, cancellationToken);
        var entropy = System.Text.Encoding.UTF8.GetBytes(key);
        byte[]? plain = null;
        try
        {
            plain = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<StoredCredentials>(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            CryptographicOperations.ZeroMemory(entropy);
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = GetPath(key);
        if (File.Exists(file)) File.Delete(file);
        return Task.CompletedTask;
    }

    private string GetPath(string key)
    {
        if (!Guid.TryParseExact(key, "N", out _)) throw new ArgumentException("Clave de credencial no válida.", nameof(key));
        return Path.Combine(_directory, key + ".dpapi");
    }
}
