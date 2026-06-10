using Castle.Sim.Geometry;
using Castle.Sim.Resources;

namespace Castle.Sim.Entities;

/// <summary>
/// A building under construction. First it needs materials delivered (hauled in),
/// then a worker performs build work to advance progress to completion. This single
/// pattern yields the whole "bring materials -> build" loop
/// (see medieval-castles/construction.md and game-ai/job-system.md).
/// </summary>
public sealed class ConstructionSite
{
    private readonly Dictionary<ResourceKind, int> _required;
    private readonly Dictionary<ResourceKind, int> _delivered = new();
    private readonly Dictionary<ResourceKind, int> _incoming = new();   // reserved by workers en route

    public Cell Cell { get; }
    public string Name { get; }

    /// <summary>Build effort in seconds once all materials are present.</summary>
    public float BuildWorkSeconds { get; }

    /// <summary>0..1 build completion.</summary>
    public float Progress { get; private set; }

    public bool Reserved { get; set; }
    public bool Complete { get; private set; }

    public ConstructionSite(Cell cell, string name, Dictionary<ResourceKind, int> required, float buildWorkSeconds)
    {
        Cell = cell;
        Name = name;
        _required = required;
        BuildWorkSeconds = buildWorkSeconds;
    }

    public void Deliver(ResourceKind kind, int amount)
    {
        _delivered.TryGetValue(kind, out int current);
        _delivered[kind] = current + amount;
    }

    public int Outstanding(ResourceKind kind)
    {
        _required.TryGetValue(kind, out int need);
        _delivered.TryGetValue(kind, out int have);
        return System.Math.Max(0, need - have);
    }

    /// <summary>Mark <paramref name="amount"/> of a resource as on its way (a worker was dispatched to fetch it).</summary>
    public void ReserveIncoming(ResourceKind kind, int amount)
    {
        _incoming.TryGetValue(kind, out int current);
        _incoming[kind] = current + amount;
    }

    /// <summary>Cancel an incoming reservation when the job finishes or aborts.</summary>
    public void ReleaseIncoming(ResourceKind kind, int amount)
    {
        _incoming.TryGetValue(kind, out int current);
        _incoming[kind] = System.Math.Max(0, current - amount);
    }

    /// <summary>What still needs to be fetched: required minus delivered minus already en route.
    /// Drives load-balancing so workers don't all chase the same resource.</summary>
    public int Remaining(ResourceKind kind)
    {
        _required.TryGetValue(kind, out int need);
        _delivered.TryGetValue(kind, out int have);
        _incoming.TryGetValue(kind, out int inc);
        return System.Math.Max(0, need - have - inc);
    }

    public IEnumerable<ResourceKind> RequiredKinds => _required.Keys;

    public bool MaterialsComplete => RequiredKinds.All(k => Outstanding(k) == 0);

    /// <summary>Advance build progress by <paramref name="seconds"/> of work. Returns true when finished.</summary>
    public bool AdvanceBuild(float seconds)
    {
        if (Complete) return true;
        Progress = System.Math.Min(1f, Progress + seconds / BuildWorkSeconds);
        if (Progress >= 1f)
            Complete = true;
        return Complete;
    }
}
