using StreamDesk.Application;

namespace StreamDesk.Probe.Infrastructure;

public sealed class SafeFileLogger(string path) : IAppLogger
{
    private readonly string _path = Path.GetFullPath(path);

    public void LogError(string eventId, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            // Deliberately omit exception messages, stack traces, URLs, and request data.
            File.AppendAllText(_path, $"{DateTimeOffset.UtcNow:O} {Sanitize(eventId)} {exception.GetType().Name}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never take down the UI.
        }
    }

    private static string Sanitize(string value) =>
        new(value.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').Take(40).ToArray());
}
