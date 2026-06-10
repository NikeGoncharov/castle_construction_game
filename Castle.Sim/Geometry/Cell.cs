namespace Castle.Sim.Geometry;

/// <summary>Integer grid coordinate. The simulation runs on a grid; the 3D
/// renderer maps cells to world positions (see medieval-castles / game-ai skills).</summary>
public readonly record struct Cell(int X, int Y)
{
    public int ManhattanTo(Cell other) => System.Math.Abs(X - other.X) + System.Math.Abs(Y - other.Y);

    public IEnumerable<Cell> Neighbours4()
    {
        yield return new Cell(X + 1, Y);
        yield return new Cell(X - 1, Y);
        yield return new Cell(X, Y + 1);
        yield return new Cell(X, Y - 1);
    }

    public override string ToString() => $"({X},{Y})";
}
