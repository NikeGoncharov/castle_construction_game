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

    /// <summary>True if any other worker currently stands on <paramref name="cell"/> (worker–worker collision).</summary>
    public bool IsCellOccupied(Worker mover, Cell cell)
    {
        foreach (var w in Workers)
            if (!ReferenceEquals(w, mover) && w.Cell == cell)
                return true;
        return false;
    }

    /// <summary>Cells occupied by every worker except <paramref name="except"/> — used to reroute around jams.</summary>
    public IEnumerable<Cell> OccupiedCells(Worker except)
    {
        foreach (var w in Workers)
            if (!ReferenceEquals(w, except))
                yield return w.Cell;
    }

    public void TryAssignJob(Worker worker)
    {
        // 1. If a site has all its materials and nobody is building it, build.
        foreach (var site in Sites)
        {
            if (!site.Complete && site.MaterialsComplete && !site.Reserved)
            {
                AssignBuild(worker, site);
                return;
            }
        }

        // 2. Otherwise fetch the resource the neediest site still lacks, balancing
        //    workers across wood and stone by what's left to fetch (Remaining).
        var siteNeedy = Sites.FirstOrDefault(s => !s.Complete);
        if (siteNeedy == null)
            return;

        int woodRem = siteNeedy.Remaining(ResourceKind.Wood);
        int stoneRem = siteNeedy.Remaining(ResourceKind.Stone);

        // Try the resource with the greater remaining need first; fall back to the other
        // if no free source is available for it.
        bool stoneFirst = stoneRem > woodRem;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool tryStone = stoneFirst ? attempt == 0 : attempt == 1;
            if (tryStone)
            {
                if (stoneRem > 0 && NearestFreeQuarry(worker.Cell) is { } quarry)
                {
                    AssignMineAndDeliver(worker, quarry, siteNeedy);
                    return;
                }
            }
            else
            {
                if (woodRem > 0 && NearestFreeTree(worker.Cell) is { } tree)
                {
                    AssignChopAndDeliver(worker, tree, siteNeedy);
                    return;
                }
            }
        }

        // 3. No work right now (everything needed is already en route). Step out of the
        //    work area back to the home cell so deliverers can reach the site.
        if (worker.Cell != worker.HomeCell)
            worker.AssignJob("returning", new IStep[] { GoToStep.ToCell(worker.HomeCell) });
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

    private Quarry? NearestFreeQuarry(Cell from)
    {
        Quarry? best = null;
        int bestDist = int.MaxValue;
        foreach (var q in Quarries)
        {
            if (q.Reserved)
                continue;
            int d = from.ManhattanTo(q.Cell);
            if (d < bestDist)
            {
                bestDist = d;
                best = q;
            }
        }
        return best;
    }

    private void AssignChopAndDeliver(Worker worker, Tree tree, ConstructionSite site)
    {
        tree.Reserved = true;
        site.ReserveIncoming(ResourceKind.Wood, TreeYield);
        void Release()
        {
            tree.Reserved = false;
            site.ReleaseIncoming(ResourceKind.Wood, TreeYield);
        }

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
            new ActionStep(Release),
        };
        worker.AssignJob($"chop {tree.Cell}", steps, releaseReservations: Release);
    }

    private void AssignMineAndDeliver(Worker worker, Quarry quarry, ConstructionSite site)
    {
        quarry.Reserved = true;
        site.ReserveIncoming(ResourceKind.Stone, QuarryYield);
        void Release()
        {
            quarry.Reserved = false;
            site.ReleaseIncoming(ResourceKind.Stone, QuarryYield);
        }

        var steps = new IStep[]
        {
            GoToStep.Adjacent(quarry.Cell),
            new WorkStep(MineSeconds, onDone: () =>
            {
                worker.Carry = (ResourceKind.Stone, QuarryYield);
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
            new ActionStep(Release),
        };
        worker.AssignJob($"mine {quarry.Cell}", steps, releaseReservations: Release);
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
