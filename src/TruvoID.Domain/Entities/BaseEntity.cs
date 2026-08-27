namespace TruvoID.Domain.Entities;

/// <summary>
/// Base entity with audit fields for all domain entities.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
