using System.ComponentModel.DataAnnotations;

namespace FactoryFlow.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

