using Castle.Sim.Geometry;
using Castle.Sim.Resources;

namespace Castle.Sim.Entities;

/// <summary>A storage point for loose resources. Hauled materials accumulate here.</summary>
public sealed class Stockpile
{
    private readonly Dictionary<ResourceKind, int> _stored = new();

    public Cell Cell { get; }

    public Stockpile(Cell cell) => Cell = cell;

    public void Add(ResourceKind kind, int amount)
    {
        _stored.TryGetValue(kind, out int current);
        _stored[kind] = current + amount;
    }

    public int Get(ResourceKind kind) => _stored.TryGetValue(kind, out int v) ? v : 0;
}
