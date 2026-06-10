using Castle.Sim;
using Castle.Sim.Entities;
using Castle.Sim.Geometry;
using Castle.Sim.Resources;
using Castle.Sim.Workers;
using Castle.Sim.World;

var sim = Scenarios.ForestKeep();
var keep = sim.Sites[0];

Console.WriteLine("Castle.Sim demo — workers chop the forest and build the Keep.");
Console.WriteLine("Legend: . field  , forest  T tree  S stockpile  B building  # done  a/b/c worker\n");

const float dt = 0.1f;
const int maxSteps = 6000;
int step = 0;

Render(sim, step, dt);
while (!sim.AllSitesComplete && step < maxSteps)
{
    sim.Tick(dt);
    step++;
    if (step % 10 == 0)
        Render(sim, step, dt);
}

Render(sim, step, dt);

if (sim.AllSitesComplete)
{
    Console.WriteLine($"\nSUCCESS: Keep built in {sim.Time:0.0}s of simulated time " +
                      $"({step} ticks). Wood delivered: {keep.Outstanding(ResourceKind.Wood) == 0}.");
    return 0;
}

Console.Error.WriteLine("\nFAILURE: Keep was not completed within the step budget.");
return 1;

static void Render(Simulation sim, int step, float dt)
{
    var map = sim.Map;
    var grid = new char[map.Height][];
    for (int y = 0; y < map.Height; y++)
    {
        grid[y] = new char[map.Width];
        for (int x = 0; x < map.Width; x++)
            grid[y][x] = map.GetTerrain(new Cell(x, y)) switch
            {
                TileType.Forest => ',',
                TileType.Water => '~',
                TileType.Rock => ':',
                _ => '.',
            };
    }

    foreach (var t in sim.Trees)
        if (!t.Depleted) grid[t.Cell.Y][t.Cell.X] = 'T';
    foreach (var s in sim.Stockpiles)
        grid[s.Cell.Y][s.Cell.X] = 'S';
    foreach (var site in sim.Sites)
        grid[site.Cell.Y][site.Cell.X] = site.Complete ? '#' : 'B';

    char tag = 'a';
    foreach (var w in sim.Workers)
        grid[w.Cell.Y][w.Cell.X] = tag++;

    Console.WriteLine($"--- t={step * dt:0.0}s  step={step} ---");
    for (int y = 0; y < map.Height; y++)
        Console.WriteLine(new string(grid[y]));

    Console.WriteLine($"Keep: wood needed {sim.Sites[0].Outstanding(ResourceKind.Wood)}, " +
                      $"build {sim.Sites[0].Progress * 100:0}%   |   " +
                      string.Join("  ", sim.Workers.Select(w =>
                          $"{w.Name}:{w.Activity}{(w.Carry is { } c ? $"+{c.Amount}w" : "")}")));
    Console.WriteLine();
}
