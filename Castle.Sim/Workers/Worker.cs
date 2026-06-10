using Castle.Sim.Geometry;
using Castle.Sim.Resources;

namespace Castle.Sim.Workers;

/// <summary>
/// An NPC laborer. When idle it requests a job from the simulation; a job fills
/// its step queue. The worker executes one step at a time. Carrying capacity is
/// a single resource stack for now. (See game-ai/job-system.md.)
/// </summary>
public sealed class Worker
{
    private readonly Queue<IStep> _steps = new();
    private System.Action? _releaseReservations;

    public string Name { get; }
    public Cell Cell { get; set; }
    public Cell HomeCell { get; }            // idle rally spot, so finished workers clear the work area
    public float Speed { get; }              // cells per second
    public float MoveAccumulator { get; set; }

    public int SlotIndex { get; }              // visual stand offset within a cell

    public (ResourceKind Kind, int Amount)? Carry { get; set; }
    public string Activity { get; private set; } = "idle";

    public bool IsIdle => _steps.Count == 0;

    public Worker(string name, Cell cell, float speed = 3f, int slotIndex = 0)
    {
        Name = name;
        Cell = cell;
        HomeCell = cell;
        Speed = speed;
        SlotIndex = slotIndex;
    }

    /// <summary>Assign a job: a label, the steps to run, and how to release any
    /// reservations when the job ends or is aborted.</summary>
    public void AssignJob(string activity, IEnumerable<IStep> steps, System.Action? releaseReservations = null)
    {
        Activity = activity;
        _steps.Clear();
        foreach (var s in steps)
            _steps.Enqueue(s);
        _releaseReservations = releaseReservations;
    }

    public void Tick(Simulation sim, float dt)
    {
        if (_steps.Count == 0)
        {
            sim.TryAssignJob(this);
            if (_steps.Count == 0)
            {
                Activity = "idle";
                return;
            }
        }

        var step = _steps.Peek();
        var status = step.Tick(this, sim, dt);
        switch (status)
        {
            case StepStatus.Done:
                _steps.Dequeue();
                if (_steps.Count == 0)
                    FinishJob();
                break;
            case StepStatus.Failed:
                AbortJob();
                break;
            case StepStatus.Running:
            default:
                break;
        }
    }

    private void FinishJob()
    {
        _releaseReservations = null; // reservations are released by the job's own final ActionStep
        Activity = "idle";
    }

    public void AbortJob()
    {
        _steps.Clear();
        _releaseReservations?.Invoke();
        _releaseReservations = null;
        Carry = null;
        Activity = "idle";
    }
}
