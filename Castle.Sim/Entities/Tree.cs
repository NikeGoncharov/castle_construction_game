using Castle.Sim.Geometry;

namespace Castle.Sim.Entities;

/// <summary>A harvestable tree: a resource node that yields wood when chopped.
/// Occupies its cell (blocks movement); workers chop from an adjacent cell.</summary>
public sealed class Tree
{
    public Cell Cell { get; }
    public int Wood { get; private set; }
    public bool Reserved { get; set; }
    public bool Depleted => Wood <= 0;

    public Tree(Cell cell, int wood)
    {
        Cell = cell;
        Wood = wood;
    }

    /// <summary>Remove up to <paramref name="amount"/> wood; returns the amount actually harvested.</summary>
    public int Harvest(int amount)
    {
        int taken = System.Math.Min(amount, Wood);
        Wood -= taken;
        return taken;
    }
}
