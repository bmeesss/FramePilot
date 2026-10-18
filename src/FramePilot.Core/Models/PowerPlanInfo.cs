namespace FramePilot.Core.Models;

/// <summary>
/// A Windows power plan (power scheme) as reported by <c>powercfg</c>.
/// </summary>
public sealed record PowerPlanInfo(Guid Guid, string Name, bool IsActive)
{
    public override string ToString() => IsActive ? $"{Name} (active)" : Name;
}
