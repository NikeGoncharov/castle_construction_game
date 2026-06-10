using Castle.Sim.Entities;
using Castle.Sim.Geometry;
using Castle.Sim.Resources;
using Castle.Sim.Workers;
using Castle.Sim.World;

namespace Castle.Sim;

public static class Scenarios
{
    /// <summary>A wide field that slopes gently up to a forested rise on the right, with a
    /// stone quarry off in the far part of the map. A crew chops wood and mines stone to
    /// raise the Keep.</summary>
    public static Simulation ForestKeep(int width = 56, int height = 28)
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

        // Forest on the right half. Trees are scattered (~32% of cells) rather than a solid
        // checkerboard, so the player can actually stroll between them.
        int forestStart = width / 2;
        for (int x = forestStart; x < width - 1; x++)
        for (int y = 1; y < height - 1; y++)
        {
            map.SetTerrain(new Cell(x, y), TileType.Forest);
            if (Hash(x, y) % 100 >= 32)
                continue;

            var cell = new Cell(x, y);
            map.SetBlocked(cell, true);
            sim.Trees.Add(new Tree(cell, wood: sim.TreeYield));
        }

        // Quarry out in the far (lower) part of the map, a real walk from the spawn.
        foreach (var qCell in new[] { new Cell(24, 24), new Cell(25, 24) })
        {
            map.SetTerrain(qCell, TileType.Rock);
            map.SetBlocked(qCell, true);
            sim.Quarries.Add(new Quarry(qCell, sim.QuarryYield));
        }

        sim.Stockpiles.Add(new Stockpile(new Cell(10, 10)));

        var keep = new ConstructionSite(
            new Cell(14, 14), "Keep",
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
            sim.Workers.Add(new Worker(names[i], new Cell(2, 4 + i * 3), slotIndex: i));

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
