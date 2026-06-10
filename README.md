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
├─ Castle.Sim.Demo/    # headless console demo proving the worker/economy loop
├─ Castle.Game/        # Stride game package: scene assets + CastleSimRenderer bridge
└─ Castle.Windows/     # Stride Windows executable entry point
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
cd C:\git\castle_construction
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

## Run the 3D game (Stride)

The Stride renderer lives in `Castle.Game` (package + `CastleSimRenderer`
bridge script) and `Castle.Windows` (executable). Build and run:

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"   # needs .NET 10 SDK
dotnet build CastleDevelopment.sln -c Release
& "Bin\Windows\Release\win-x64\Castle.Windows.exe"
```

Editing scenes/assets is done in **Stride Game Studio**: open
**`CastleDevelopment.sln`** via the Stride launcher (NOT the bare
`Castle.Game.sdpkg` — opening the package alone loads no `Castle.Windows`
executable, and Run fails with "Platform Windows is not supported"). The bridge keeps all
game rules in `Castle.Sim`; the Stride layer only renders sim state: coloured
boxes for workers/trees, the Keep grows with build `Progress`, and a free-fly
camera (WASD + RMB-look, Q/E up/down, Shift to speed up).

Current state and next steps are tracked in [ROADMAP.md](ROADMAP.md).
