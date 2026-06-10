namespace Castle.Sim.World;

/// <summary>Terrain type of a grid tile. Movement cost / walkability derive from this.</summary>
public enum TileType
{
    Grass,   // open field, walkable
    Forest,  // walkable ground under/around trees
    Rock,    // walkable, may host a quarry
    Water    // not walkable
}
