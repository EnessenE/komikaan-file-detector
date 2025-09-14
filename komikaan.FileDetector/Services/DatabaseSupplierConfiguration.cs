using komikaan.Common.Enums;
using System.ComponentModel.DataAnnotations;

public class DatabaseSupplierConfiguration
{
    [Key]
    public string Name { get; set; } = null!;

    public RetrievalType RetrievalType { get; set; }

    public SupplierType DataType { get; set; }

    public TimeSpan PollingRate { get; set; }

    public string Url { get; set; } = null!;

    public DateTimeOffset LastUpdated { get; set; }

    public bool DownloadPending { get; set; } = false;

    public Guid ImportId { get; set; } = Guid.Empty;

    public Guid LatestSuccesfullImportId { get; set; } = Guid.Empty;

    public DateTimeOffset? LastAttempt { get; set; }

    public string? ETag { get; set; }

    public DateTimeOffset? LastChecked { get; set; }

    public DateTimeOffset? LastCheckFailure { get; set; }

    public string State { get; set; } = "unknown";

    public DateTimeOffset? LastImportStart { get; set; }

    public DateTimeOffset? LastImportSuccess { get; set; }

    public DateTimeOffset? LastImportFailure { get; set; }

    public TimeSpan LastDuration { get; set; } = TimeSpan.Zero;

    public Guid QueuedImportId { get; set; } = Guid.Empty;
    public TimeSpan? DelayImportBy { get; set; }
    public DateTimeOffset ImportRequestedAt { get; set; }
}