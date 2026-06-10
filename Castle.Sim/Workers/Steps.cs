using Castle.Sim.Geometry;
using Castle.Sim.Pathfinding;

namespace Castle.Sim.Workers;

public enum StepStatus { Running, Done, Failed }

public interface IStep
{
    StepStatus Tick(Worker worker, Simulation sim, float dt);
}

public sealed class GoToStep : IStep
{
    // If a worker is blocked by another worker for this long, replan around the jam.
    private const float StuckLimit = 0.6f;

    private readonly Cell _target;
    private readonly bool _adjacent;
    private List<Cell>? _path;
    private int _index;
    private bool _planned;
    private float _waitTime;

    private GoToStep(Cell target, bool adjacent)
    {
        _target = target;
        _adjacent = adjacent;
    }

    public static GoToStep ToCell(Cell cell) => new(cell, adjacent: false);
    public static GoToStep Adjacent(Cell target) => new(target, adjacent: true);

    public StepStatus Tick(Worker worker, Simulation sim, float dt)
    {
        if (!_planned)
        {
            _planned = true;
            if (!Plan(worker, sim, avoidWorkers: false))
                return StepStatus.Failed;
        }

        if (_path!.Count == 0 || _index >= _path.Count)
            return StepStatus.Done;

        worker.MoveAccumulator += worker.Speed * dt;
        while (worker.MoveAccumulator >= 1f && _index < _path.Count)
        {
            var next = _path[_index];

            // Worker–worker collision: don't step onto a cell another worker holds.
            if (sim.IsCellOccupied(worker, next))
            {
                worker.MoveAccumulator = System.Math.Min(worker.MoveAccumulator, 1f);
                _waitTime += dt;
                if (_waitTime >= StuckLimit)
                {
                    _waitTime = 0f;
                    // Reroute around currently occupied cells; if that fails, keep waiting
                    // (don't fail the job — that would drop a carried load). The blocker usually moves.
                    Plan(worker, sim, avoidWorkers: true);
                }
                break;
            }

            worker.Cell = next;
            _index++;
            worker.MoveAccumulator -= 1f;
            _waitTime = 0f;
        }

        return _index >= _path.Count ? StepStatus.Done : StepStatus.Running;
    }

    private bool Plan(Worker worker, Simulation sim, bool avoidWorkers)
    {
        var avoid = avoidWorkers ? new HashSet<Cell>(sim.OccupiedCells(worker)) : null;
        var path = _adjacent
            ? AStar.FindAdjacentTo(sim.Map, worker.Cell, _target, avoid)
            : AStar.FindTo(sim.Map, worker.Cell, _target, avoid);
        if (path == null)
            return false;
        _path = path;
        _index = 0;
        return true;
    }
}

public sealed class WorkStep : IStep
{
    private readonly float _seconds;
    private readonly System.Action? _onDone;
    private readonly System.Action<float>? _onTick;
    private float _elapsed;

    public WorkStep(float seconds, System.Action? onDone = null, System.Action<float>? onTick = null)
    {
        _seconds = seconds;
        _onDone = onDone;
        _onTick = onTick;
    }

    public StepStatus Tick(Worker worker, Simulation sim, float dt)
    {
        float step = System.Math.Min(dt, _seconds - _elapsed);
        _elapsed += dt;
        _onTick?.Invoke(step);
        if (_elapsed >= _seconds)
        {
            _onDone?.Invoke();
            return StepStatus.Done;
        }
        return StepStatus.Running;
    }
}

public sealed class ActionStep : IStep
{
    private readonly System.Action _action;
    public ActionStep(System.Action action) => _action = action;

    public StepStatus Tick(Worker worker, Simulation sim, float dt)
    {
        _action();
        return StepStatus.Done;
    }
}
