using komikaan.FileDetector.Enums;
using System.ComponentModel.DataAnnotations;

namespace komikaan.FileDetector.Models;

public class SupplierConfiguration
{
    public required RetrievalType RetrievalType { get; set; }
    public required SupplierType DataType { get; set; }
    public required TimeSpan PollingRate { get; set; }

    [Key]
    public required string Name { get; set; }
    public required string Url { get; set; }

    public DateTimeOffset LastUpdated { get; set; }
}