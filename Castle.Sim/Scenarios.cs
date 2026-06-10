using Castle.Sim.Entities;
using Castle.Sim.Geometry;
using Castle.Sim.Resources;
using Castle.Sim.Workers;
using Castle.Sim.World;

namespace Castle.Sim;

public static class Scenarios
{
    /// <summary>Flat field on the left, a forested hill on the right, and a small stone
    /// quarry on the field. A crew of workers chops wood and mines stone to raise the Keep.</summary>
    public static Simulation ForestKeep(int width = 28, int height = 14)
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

        // Forest on the right half (this is the hill in the 3D renderer).
        int forestStart = width / 2;
        for (int x = forestStart; x < width - 1; x++)
        for (int y = 1; y < height - 1; y++)
        {
            map.SetTerrain(new Cell(x, y), TileType.Forest);
            if ((x + y) % 2 != 0)
                continue;

            var cell = new Cell(x, y);
            map.SetBlocked(cell, true);
            sim.Trees.Add(new Tree(cell, wood: sim.TreeYield));
        }

        // A small quarry on the field — two rock cells so two miners can work at once.
        foreach (var qCell in new[] { new Cell(5, 9), new Cell(5, 10) })
        {
            map.SetTerrain(qCell, TileType.Rock);
            map.SetBlocked(qCell, true);
            sim.Quarries.Add(new Quarry(qCell, sim.QuarryYield));
        }

        sim.Stockpiles.Add(new Stockpile(new Cell(4, 4)));

        var keep = new ConstructionSite(
            new Cell(7, 7), "Keep",
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
            sim.Workers.Add(new Worker(names[i], new Cell(2, 2 + i * 2), slotIndex: i));

        return sim;
    }
}
