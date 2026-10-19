using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StreamDesk.Application;

namespace StreamDesk.Probe.Infrastructure;

public sealed class EncryptedSyncService(IViewerStore viewerStore) : ISyncService
{
    private static readonly byte[] MagicHeader = Encoding.UTF8.GetBytes("STREAMDESK_SYNC_V1");
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int Iterations = 100_000;
    private const int KeySize = 32;

    public async Task<int> ExportAsync(string targetFilePath, string passphrase, string profileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetFilePath)) throw new ArgumentException("Ruta de archivo no válida.", nameof(targetFilePath));
        if (string.IsNullOrWhiteSpace(passphrase) || passphrase.Length < 4)
            throw new ArgumentException("La contraseña de sincronización debe tener al menos 4 caracteres.", nameof(passphrase));
        if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Perfil no válido.", nameof(profileId));

        cancellationToken.ThrowIfCancellationRequested();
        var favorites = await viewerStore.GetFavoritesForSyncAsync(profileId, cancellationToken);
        var history = await viewerStore.GetHistoryForSyncAsync(profileId, cancellationToken);

        var payload = new SyncPackagePayload(
            Version: 1,
            ExportedAt: DateTimeOffset.UtcNow.ToString("O"),
            DeviceId: Environment.MachineName,
            Favorites: favorites,
            History: history);

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[jsonBytes.Length];

        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        using (var aesGcm = new AesGcm(key, TagSize))
        {
            aesGcm.Encrypt(nonce, jsonBytes, ciphertext, tag, MagicHeader);
        }

        var fullDirectory = Path.GetDirectoryName(Path.GetFullPath(targetFilePath))!;
        Directory.CreateDirectory(fullDirectory);
        var tempFile = Path.Combine(fullDirectory, Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            await using (var file = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await file.WriteAsync(MagicHeader, cancellationToken);
                await file.WriteAsync(salt, cancellationToken);
                await file.WriteAsync(nonce, cancellationToken);
                await file.WriteAsync(tag, cancellationToken);
                await file.WriteAsync(ciphertext, cancellationToken);
            }

            File.Move(tempFile, targetFilePath, overwrite: true);
            return favorites.Count + history.Count;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    public async Task<SyncResult> ImportAsync(string sourceFilePath, string passphrase, string profileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            throw new FileNotFoundException("El archivo de sincronización no existe.", sourceFilePath);
        if (string.IsNullOrWhiteSpace(passphrase))
            throw new ArgumentException("Debes introducir la contraseña de cifrado.", nameof(passphrase));
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Perfil no válido.", nameof(profileId));

        cancellationToken.ThrowIfCancellationRequested();
        var fileBytes = await File.ReadAllBytesAsync(sourceFilePath, cancellationToken);

        var headerLen = MagicHeader.Length;
        var minLength = headerLen + SaltSize + NonceSize + TagSize;
        if (fileBytes.Length < minLength)
            throw new InvalidDataException("El archivo de sincronización no es válido o está incompleto.");

        if (!fileBytes.AsSpan(0, headerLen).SequenceEqual(MagicHeader))
            throw new InvalidDataException("El formato del archivo no corresponde a un paquete de sincronización de StreamDesk.");

        var salt = fileBytes.AsSpan(headerLen, SaltSize).ToArray();
        var nonce = fileBytes.AsSpan(headerLen + SaltSize, NonceSize).ToArray();
        var tag = fileBytes.AsSpan(headerLen + SaltSize + NonceSize, TagSize).ToArray();
        var cipherOffset = headerLen + SaltSize + NonceSize + TagSize;
        var ciphertext = fileBytes.AsSpan(cipherOffset).ToArray();

        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aesGcm = new AesGcm(key, TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, MagicHeader);
        }
        catch (CryptographicException)
        {
            throw new CryptographicException("La contraseña es incorrecta o los datos fueron alterados.");
        }

        var payload = JsonSerializer.Deserialize<SyncPackagePayload>(plaintext);
        if (payload is null)
            throw new InvalidDataException("No se pudo leer el contenido del paquete de sincronización.");

        int favoritesAdded = 0;
        int historyMerged = 0;

        foreach (var fav in payload.Favorites)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await viewerStore.SetFavoriteAsync(profileId, fav.ContentType, fav.ChannelId, true, cancellationToken);
            favoritesAdded++;
        }

        foreach (var hist in payload.History)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await viewerStore.RecordPlayedAsync(profileId, hist.ContentType, hist.ChannelId, hist.PlayedAt, cancellationToken);
            historyMerged++;
        }

        return new SyncResult(favoritesAdded, historyMerged, $"Sincronización aplicada: {favoritesAdded} favorito(s) y {historyMerged} historial(es) combinados.");
    }
}
