using Castle.Sim.Geometry;

namespace Castle.Sim.World;

/// <summary>
/// The walkable world as a grid. Terrain defines base walkability; an occupancy
/// layer marks cells blocked by trees/buildings. Pathfinding queries this map
/// (see game-ai/pathfinding.md). A 3D game would replace this with a navmesh,
/// but the grid keeps the simulation engine-agnostic and headless-testable.
/// </summary>
public sealed class GridMap
{
    private readonly TileType[] _terrain;
    private readonly bool[] _blocked;

    public int Width { get; }
    public int Height { get; }

    public GridMap(int width, int height, TileType fill = TileType.Grass)
    {
        Width = width;
        Height = height;
        _terrain = new TileType[width * height];
        _blocked = new bool[width * height];
        System.Array.Fill(_terrain, fill);
    }

    private int Index(Cell c) => c.Y * Width + c.X;

    public bool InBounds(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;

    public TileType GetTerrain(Cell c) => _terrain[Index(c)];
    public void SetTerrain(Cell c, TileType t) => _terrain[Index(c)] = t;

    public bool IsBlocked(Cell c) => _blocked[Index(c)];
    public void SetBlocked(Cell c, bool blocked) => _blocked[Index(c)] = blocked;

    /// <summary>A cell a worker can stand on: in bounds, walkable terrain, not occupied.</summary>
    public bool IsWalkable(Cell c)
        => InBounds(c) && GetTerrain(c) != TileType.Water && !IsBlocked(c);

    public IEnumerable<Cell> WalkableNeighbours(Cell c)
    {
        foreach (var n in c.Neighbours4())
            if (IsWalkable(n))
                yield return n;
    }

    /// <summary>Walkable cells adjacent to a (possibly blocked) target, e.g. to chop a tree.</summary>
    public IEnumerable<Cell> WalkableAdjacent(Cell target)
    {
        foreach (var n in target.Neighbours4())
            if (IsWalkable(n))
                yield return n;
    }
}
