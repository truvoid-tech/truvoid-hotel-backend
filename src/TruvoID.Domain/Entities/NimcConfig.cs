namespace TruvoID.Domain.Entities;

/// <summary>
/// Simple config to track which NIMC environment is active.
/// Actual API keys are stored in Railway env vars, not here.
/// </summary>
public class NimcConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ActiveEnvironment { get; set; } = "sandbox"; // "live" or "sandbox"
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
