using Castle.Sim.Entities;
using Castle.Sim.Geometry;
using Castle.Sim.Resources;
using Castle.Sim.Workers;
using Castle.Sim.World;

namespace Castle.Sim;

public static class Scenarios
{
    /// <summary>Small flat field, forest on the right, workers build a keep from wood.</summary>
    public static Simulation ForestKeep(int width = 28, int height = 14)
    {
        var map = new GridMap(width, height, TileType.Grass);
        var sim = new Simulation(map)
        {
            ChopSeconds = 1.5f,
            TreeYield = 2,
            DepositSeconds = 0.3f,
        };

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

        sim.Stockpiles.Add(new Stockpile(new Cell(4, 4)));

        var keep = new ConstructionSite(
            new Cell(7, 7), "Keep",
            required: new Dictionary<ResourceKind, int> { [ResourceKind.Wood] = 10 },
            buildWorkSeconds: 5f);
        map.SetBlocked(keep.Cell, true);
        sim.Sites.Add(keep);

        sim.Workers.Add(new Worker("Aldric", new Cell(2, 2)));
        sim.Workers.Add(new Worker("Brom", new Cell(2, 4)));
        sim.Workers.Add(new Worker("Cedric", new Cell(2, 6)));

        return sim;
    }
}
