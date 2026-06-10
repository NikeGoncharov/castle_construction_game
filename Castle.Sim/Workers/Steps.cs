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
    private readonly Cell _target;
    private readonly bool _adjacent;
    private List<Cell>? _path;
    private int _index;
    private bool _planned;

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
            _path = _adjacent
                ? AStar.FindAdjacentTo(sim.Map, worker.Cell, _target)
                : AStar.FindTo(sim.Map, worker.Cell, _target);
            if (_path == null)
                return StepStatus.Failed;
        }

        if (_path!.Count == 0 || _index >= _path.Count)
            return StepStatus.Done;

        worker.MoveAccumulator += worker.Speed * dt;
        while (worker.MoveAccumulator >= 1f && _index < _path.Count)
        {
            worker.Cell = _path[_index];
            _index++;
            worker.MoveAccumulator -= 1f;
        }

        return _index >= _path.Count ? StepStatus.Done : StepStatus.Running;
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
