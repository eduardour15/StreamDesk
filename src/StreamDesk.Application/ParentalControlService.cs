using System.Security.Cryptography;
using System.Text.RegularExpressions;
using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public sealed class ParentalControlService(ISettingsService settingsService)
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    private static readonly Regex AdultRegex = new(
        @"(?i)\b(adults?|adultos?|xxx|\+?18\+?|eroti[ck]a?|porn|playboy|venus|hustler)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private AppSettings _currentSettings = new();
    private bool _isLocked = true;

    public bool IsEnabled => _currentSettings.ParentalControlEnabled;
    public bool IsLocked => IsEnabled && _isLocked;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _currentSettings = await settingsService.GetAppSettingsAsync(cancellationToken);
        _isLocked = _currentSettings.ParentalControlEnabled;
    }

    public async Task<bool> ConfigurePinAsync(string pin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Length is < 4 or > 8 || !pin.All(char.IsDigit))
            throw new ArgumentException("El PIN debe tener entre 4 y 8 dígitos numéricos.", nameof(pin));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        var updated = _currentSettings with
        {
            ParentalControlEnabled = true,
            ParentalPinHash = Convert.ToBase64String(hash),
            ParentalPinSalt = Convert.ToBase64String(salt)
        };

        await settingsService.SaveAppSettingsAsync(updated, cancellationToken);
        _currentSettings = updated;
        _isLocked = true;
        return true;
    }

    public async Task<bool> DisableAsync(string pin, CancellationToken cancellationToken = default)
    {
        if (!VerifyPin(pin)) return false;

        var updated = _currentSettings with
        {
            ParentalControlEnabled = false,
            ParentalPinHash = null,
            ParentalPinSalt = null
        };

        await settingsService.SaveAppSettingsAsync(updated, cancellationToken);
        _currentSettings = updated;
        _isLocked = false;
        return true;
    }

    public bool Unlock(string pin)
    {
        if (!IsEnabled) { _isLocked = false; return true; }
        if (VerifyPin(pin))
        {
            _isLocked = false;
            return true;
        }
        return false;
    }

    public void Lock()
    {
        if (IsEnabled) _isLocked = true;
    }

    public bool IsRestrictedCategory(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return false;
        return AdultRegex.IsMatch(categoryName);
    }

    public bool ShouldHideCategory(string? categoryName)
    {
        return IsLocked && IsRestrictedCategory(categoryName);
    }

    private bool VerifyPin(string pin)
    {
        if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(_currentSettings.ParentalPinHash) || string.IsNullOrEmpty(_currentSettings.ParentalPinSalt))
            return false;

        try
        {
            var salt = Convert.FromBase64String(_currentSettings.ParentalPinSalt);
            var expectedHash = Convert.FromBase64String(_currentSettings.ParentalPinHash);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch
        {
            return false;
        }
    }
}
