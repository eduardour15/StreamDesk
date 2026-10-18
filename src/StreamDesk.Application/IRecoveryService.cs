namespace StreamDesk.Application;

public interface IRecoveryService
{
    Task<string> CreateBackupAsync(CancellationToken cancellationToken = default);
    Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default);
}
