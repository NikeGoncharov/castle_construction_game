using Castle.Sim.Geometry;

namespace Castle.Sim.Entities;

/// <summary>A stone quarry node. Workers mine from an adjacent cell and haul
/// stone to the construction site (see medieval-castles/construction.md).</summary>
public sealed class Quarry
{
    public Cell Cell { get; }
    public int StonePerTrip { get; }
    public bool Reserved { get; set; }

    public Quarry(Cell cell, int stonePerTrip = 3) => (Cell, StonePerTrip) = (cell, stonePerTrip);
}
