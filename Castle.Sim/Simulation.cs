using Castle.Sim.Entities;
using Castle.Sim.Geometry;
using Castle.Sim.Resources;
using Castle.Sim.Workers;
using Castle.Sim.World;

namespace Castle.Sim;

public sealed class Simulation
{
    public GridMap Map { get; }
    public List<Tree> Trees { get; } = new();
    public List<Quarry> Quarries { get; } = new();
    public List<Stockpile> Stockpiles { get; } = new();
    public List<ConstructionSite> Sites { get; } = new();
    public List<Worker> Workers { get; } = new();

    public float Time { get; private set; }

    public float ChopSeconds { get; init; } = 1.5f;
    public float MineSeconds { get; init; } = 2.0f;
    public int TreeYield { get; init; } = 2;
    public int QuarryYield { get; init; } = 3;
    public float DepositSeconds { get; init; } = 0.3f;

    public Simulation(GridMap map) => Map = map;

    public bool AllSitesComplete => Sites.All(s => s.Complete);

    public void Tick(float dt)
    {
        Time += dt;
        foreach (var w in Workers)
            w.Tick(this, dt);
    }

    public void TryAssignJob(Worker worker)
    {
        foreach (var site in Sites)
        {
            if (!site.Complete && site.MaterialsComplete && !site.Reserved)
            {
                AssignBuild(worker, site);
                return;
            }
        }

        var siteNeedy = Sites.FirstOrDefault(s => !s.Complete);
        if (siteNeedy == null)
            return;

        if (siteNeedy.Outstanding(ResourceKind.Wood) > 0)
        {
            var tree = NearestFreeTree(worker.Cell);
            if (tree != null)
            {
                AssignChopAndDeliver(worker, tree, siteNeedy);
                return;
            }
        }
    }

    private Tree? NearestFreeTree(Cell from)
    {
        Tree? best = null;
        int bestDist = int.MaxValue;
        foreach (var t in Trees)
        {
            if (t.Reserved || t.Depleted)
                continue;
            int d = from.ManhattanTo(t.Cell);
            if (d < bestDist)
            {
                bestDist = d;
                best = t;
            }
        }
        return best;
    }

    private void AssignChopAndDeliver(Worker worker, Tree tree, ConstructionSite site)
    {
        tree.Reserved = true;
        var steps = new IStep[]
        {
            GoToStep.Adjacent(tree.Cell),
            new WorkStep(ChopSeconds, onDone: () =>
            {
                int got = tree.Harvest(TreeYield);
                worker.Carry = (ResourceKind.Wood, got);
                if (tree.Depleted)
                    Map.SetBlocked(tree.Cell, false);
            }),
            GoToStep.Adjacent(site.Cell),
            new WorkStep(DepositSeconds, onDone: () =>
            {
                if (worker.Carry is { } c)
                {
                    site.Deliver(c.Kind, c.Amount);
                    worker.Carry = null;
                }
            }),
            new ActionStep(() => tree.Reserved = false),
        };
        worker.AssignJob($"chop {tree.Cell}", steps, releaseReservations: () => tree.Reserved = false);
    }

    private void AssignBuild(Worker worker, ConstructionSite site)
    {
        site.Reserved = true;
        var steps = new IStep[]
        {
            GoToStep.Adjacent(site.Cell),
            new WorkStep(site.BuildWorkSeconds, onTick: sec => site.AdvanceBuild(sec)),
            new ActionStep(() => site.Reserved = false),
        };
        worker.AssignJob($"build {site.Name}", steps, releaseReservations: () => site.Reserved = false);
    }
}
