# Castle Development

A first-person medieval survival / colony-builder in C#. The design goal is
**deep logic and economy** (workers, resources, construction, defense) rather
than AAA graphics — inspired by Bellwright / Medieval Dynasty.

The codebase is split so that **gameplay logic is engine-agnostic** and the 3D
engine is just a renderer on top:

```
CastleDevelopment.sln
├─ Castle.Sim/         # engine-agnostic simulation core (NO graphics deps)
│  ├─ Geometry/        # Cell (grid coordinate)
│  ├─ World/           # GridMap, TileType
│  ├─ Pathfinding/     # AStar  (game-ai/pathfinding.md)
│  ├─ Resources/       # ResourceKind
│  ├─ Entities/        # Tree, Stockpile, ConstructionSite
│  ├─ Workers/         # Worker + reusable behavior steps (GoTo/Work/Action)
│  └─ Simulation.cs    # world + fixed-timestep tick + job board (game-ai/job-system.md)
└─ Castle.Sim.Demo/    # headless console demo proving the worker/economy loop
```

This separation is deliberate (see the `game-engine-architecture`, `game-ai`,
and `medieval-castles` skills): the simulation is fully testable headlessly, and
**Stride** (the chosen engine) will render the same `Simulation` state without
owning any game rules.

## Prerequisites

.NET 8 SDK. It was installed to a per-user location (`%USERPROFILE%\.dotnet`) and
may not be on your permanent PATH yet. For a session, prepend it:

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
dotnet --version   # expect 8.0.x
```

(To make it permanent, add `%USERPROFILE%\.dotnet` to your user PATH, or install
the SDK system-wide from https://dotnet.microsoft.com/download.)

## Run the headless simulation demo

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
cd C:\git\castle_development
dotnet run --project Castle.Sim.Demo -c Release
```

You'll see an ASCII map each simulated second: workers (`a/b/c`) walk to the
forest (`T`), chop trees, haul wood to the Keep construction site (`B`), and once
12 wood is delivered they build it to completion (`#`). The program exits `0` on
success.

Legend: `.` field · `,` forest floor · `T` tree · `S` stockpile · `B` building
site · `#` completed building · `a/b/c` workers.

## What the core already models

- **Grid world** with terrain + occupancy, and **A\*** pathfinding (admissible
  Manhattan heuristic, priority-queue open list — Millington's model).
- **Workers** that, when idle, request a job and execute a queue of reusable
  steps (path-follow → timed work → side effect).
- **Job board** with priorities and **target reservations** (no two workers claim
  the same tree).
- **Economy loop**: chop trees → carry/haul wood → deliver to a construction site
  → build to completion. Resource amounts, build progress, and tunables
  (chop time, yield) are all data on the sim.

This is the foundation for the production chains, building tech tree, and
defense systems described in the `medieval-castles` skill.

## Next step: add the Stride 3D renderer (manual, in Game Studio)

Stride's normal workflow uses the **Stride Game Studio** GUI (to set up scenes,
terrain, camera, assets), which can't be scripted by an agent. Do this once:

1. **Install Stride.** Get the Stride launcher from https://www.stride3d.net/ and
   install Stride + Game Studio (it requires the .NET SDK and the Visual Studio
   build tools / workloads it prompts for).
2. **Create a new game** in Game Studio (e.g. `Castle.Game`), saved inside this
   folder so it joins the solution.
3. **Reference the sim core:** add a project reference from `Castle.Game` to
   `Castle.Sim/Castle.Sim.csproj` (right-click Dependencies → Add → Project
   Reference, or `dotnet add Castle.Game reference Castle.Sim/Castle.Sim.csproj`).
4. **Wire it up** with a thin bridge (no game rules in the engine layer):
   - On startup, build a `Simulation` (as the demo does) and store it in a script.
   - In an `Update` sync script, call `sim.Tick((float)gameTime.Elapsed.TotalSeconds)`.
   - Each frame, map sim state to entities: spawn/position a model per `Worker`
     at its `Cell` (×tile size), instance tree models for non-depleted `Tree`s,
     and show the `ConstructionSite` scaling with `Progress`.
   - Add a first-person camera + character controller for the player.

Slice 1 ("a walkable 3D location") then becomes: render the terrain grid as a
ground plane/heightmap, place the trees and the site, drop in the FP controller,
and let the existing simulation drive the workers. The hard logic is already done
and tested here.
