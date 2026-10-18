using System.Threading.Channels;
using StreamDesk.Application;

namespace StreamDesk.Probe.Infrastructure;

public sealed class SafeFileLogger : IAppLogger, IDisposable, IAsyncDisposable
{
    private readonly string _path;
    private readonly long _maximumBytes;
    private readonly Channel<string> _entries = Channel.CreateBounded<string>(new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Task _writer;
    public SafeFileLogger(string path, long maximumBytes = 1024 * 1024)
    {
        if (maximumBytes is < 1024 or > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        _path = Path.GetFullPath(path); _maximumBytes = maximumBytes; _writer = Task.Run(WriteAsync);
    }
    public void LogError(string eventId, Exception exception) => _entries.Writer.TryWrite(
        $"{DateTimeOffset.UtcNow:O} {Sanitize(eventId, 40)} {Sanitize(exception.GetType().Name, 80)}{Environment.NewLine}");
    private async Task WriteAsync()
    {
        await foreach (var entry in _entries.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                if (File.Exists(_path) && new FileInfo(_path).Length + System.Text.Encoding.UTF8.GetByteCount(entry) > _maximumBytes)
                    File.Move(_path, _path + ".1", overwrite: true);
                await File.AppendAllTextAsync(_path, entry).ConfigureAwait(false);
            }
            catch { /* Logging cannot stop playback or retain exception payloads. */ }
        }
    }
    private static string Sanitize(string value, int limit) => new(value.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').Take(limit).ToArray());
    public async ValueTask DisposeAsync() { _entries.Writer.TryComplete(); await _writer.ConfigureAwait(false); }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
