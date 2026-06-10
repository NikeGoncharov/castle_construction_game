using Castle.Sim.Entities;
using Castle.Sim.Geometry;
using Castle.Sim.Resources;
using Castle.Sim.Workers;
using Castle.Sim.World;

namespace Castle.Sim;

public static class Scenarios
{
    /// <summary>A large field that slopes gently up to a forested rise on the right, with a
    /// stone quarry off in the far part of the map. A crew chops wood and mines stone to
    /// raise the Keep.</summary>
    public static Simulation ForestKeep(int width = 112, int height = 56)
    {
        var map = new GridMap(width, height, TileType.Grass);
        var sim = new Simulation(map)
        {
            ChopSeconds = 1.5f,
            MineSeconds = 2.0f,
            TreeYield = 2,
            QuarryYield = 2,
            DepositSeconds = 0.3f,
        };

        // Forest on the right half. Sparse (~20% of cells) so the player can walk through and
        // the big map's tree count stays manageable.
        int forestStart = width / 2;
        for (int x = forestStart; x < width - 1; x++)
        for (int y = 1; y < height - 1; y++)
        {
            map.SetTerrain(new Cell(x, y), TileType.Forest);
            if (Hash(x, y) % 100 >= 20)
                continue;

            var cell = new Cell(x, y);
            map.SetBlocked(cell, true);
            sim.Trees.Add(new Tree(cell, wood: sim.TreeYield));
        }

        // Quarry out in the far (lower) part of the map, a real walk from the spawn.
        foreach (var qCell in new[] { new Cell(46, 50), new Cell(47, 50) })
        {
            map.SetTerrain(qCell, TileType.Rock);
            map.SetBlocked(qCell, true);
            sim.Quarries.Add(new Quarry(qCell, sim.QuarryYield));
        }

        sim.Stockpiles.Add(new Stockpile(new Cell(16, 16)));

        // Keep sits on the open field with room around it for the (large) castle the renderer
        // builds on completion.
        var keep = new ConstructionSite(
            new Cell(28, 28), "Keep",
            required: new Dictionary<ResourceKind, int>
            {
                [ResourceKind.Wood] = 12,
                [ResourceKind.Stone] = 8,
            },
            buildWorkSeconds: 6f);
        map.SetBlocked(keep.Cell, true);
        sim.Sites.Add(keep);

        string[] names = { "Aldric", "Brom", "Cedric", "Dunstan", "Edric", "Fulk" };
        for (int i = 0; i < names.Length; i++)
            sim.Workers.Add(new Worker(names[i], new Cell(3, 8 + i * 6), slotIndex: i));

        return sim;
    }

    // Deterministic per-cell hash (matches the renderer's, so layout is stable across runs).
    private static int Hash(int x, int y)
    {
        unchecked
        {
            int h = (x * 73856093) ^ (y * 19349663);
            return h & 0x7fffffff;
        }
    }
}
