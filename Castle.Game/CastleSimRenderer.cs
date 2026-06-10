using System;
using System.Collections.Generic;
using Castle.Sim;
using Castle.Sim.Entities;
using Castle.Sim.Geometry;
using Castle.Sim.Resources;
using Castle.Sim.Workers;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Input;
using Stride.Rendering;
using Stride.Rendering.Lights;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Rendering.ProceduralModels;

namespace Castle.Game;

/// <summary>
/// Bridge between Castle.Sim and Stride: fixed-timestep sim tick, a smooth
/// procedural-heightmap hill, first-person player, fullscreen (Alt+Enter) and a
/// debug HUD. The terrain mesh is sampled far finer than the gameplay grid, so the
/// hill is smooth without shrinking sim cells. Visuals are still coloured cubes —
/// see ROADMAP.md for the asset replacement plan.
/// </summary>
public class CastleSimRenderer : SyncScript
{
    private const float Tile = 2f;
    private const float SimStep = 0.1f;        // fixed simulation tick, 10 Hz
    private const float MaxFrameDt = 0.1f;     // clamp hitches so the sim isn't fast-forwarded
    private const int MaxStepsPerFrame = 3;    // backstop against a catch-up spiral
    private const float HillHeight = 3.5f;
    private const float EyeHeight = 1.7f;
    private const float WalkSpeed = 5f;
    private const float RunMultiplier = 2f;
    private const float LookSpeed = 3f;

    private Simulation _sim = null!;
    private TerrainField _terrain = null!;
    private readonly Dictionary<Worker, Entity> _workerEntities = new();
    private readonly Dictionary<Tree, Entity> _treeEntities = new();
    private Entity _siteEntity = null!;

    private Entity _cameraEntity = null!;
    private float _yaw = -MathUtil.PiOverTwo;  // start facing the forest (+X)
    private float _pitch;
    private float _simAccumulator;

    public override void Start()
    {
        _sim = Scenarios.ForestKeep();
        _terrain = new TerrainField(_sim.Map.Width * Tile, _sim.Map.Height * Tile, HillHeight);
        BuildEnvironment();
        BuildSimEntities();
        LockMouse();
    }

    public override void Update()
    {
        float dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        if (dt <= 0f) return;

        // Clamp the frame delta: a load/alt-tab hitch would otherwise dump dozens of
        // sim ticks at once, fast-forwarding past short tasks (e.g. mining) before they're seen.
        dt = Math.Min(dt, MaxFrameDt);

        _simAccumulator += dt;
        int steps = 0;
        while (_simAccumulator >= SimStep && steps < MaxStepsPerFrame)
        {
            _sim.Tick(SimStep);
            _simAccumulator -= SimStep;
            steps++;
        }
        if (steps == MaxStepsPerFrame)
            _simAccumulator = 0f;   // drop any remaining backlog rather than spiral

        SyncEntities(dt);
        HandleWindowInput();
        MovePlayer(dt);
        DrawHud();
    }

    private void BuildEnvironment()
    {
        var scene = Entity.Scene;

        var terrainMaterial = MakeMaterial(new Color(86, 125, 70), doubleSided: true);
        var terrain = new Entity("Terrain") { new ModelComponent(_terrain.BuildModel(GraphicsDevice, terrainMaterial, step: 0.5f)) };
        scene.Entities.Add(terrain);

        var sun = new Entity("Sun") { new LightComponent { Type = new LightDirectional(), Intensity = 1.1f } };
        sun.Transform.Rotation = Quaternion.RotationYawPitchRoll(0.7f, -1.0f, 0f);
        scene.Entities.Add(sun);

        var ambient = new Entity("Ambient") { new LightComponent { Type = new LightAmbient(), Intensity = 0.25f } };
        scene.Entities.Add(ambient);

        _cameraEntity = new Entity("Player");
        var camera = new CameraComponent { FarClipPlane = 500f };
        if (SceneSystem.GraphicsCompositor?.Cameras.Count > 0)
            camera.Slot = SceneSystem.GraphicsCompositor.Cameras[0].ToSlotId();
        _cameraEntity.Add(camera);
        _cameraEntity.Transform.Position = OnGround(new Cell(1, 4), EyeHeight);
        _cameraEntity.Transform.Rotation = Quaternion.RotationYawPitchRoll(_yaw, _pitch, 0f);
        scene.Entities.Add(_cameraEntity);
    }

    private void BuildSimEntities()
    {
        var scene = Entity.Scene;

        foreach (var tree in _sim.Trees)
        {
            var e = new Entity($"Tree_{tree.Cell}") { new ModelComponent(MakeCubeModel(new Color(34, 90, 34))) };
            e.Transform.Scale = new Vector3(0.7f, 2.2f, 0.7f);
            e.Transform.Position = OnGround(tree.Cell, 1.1f);
            scene.Entities.Add(e);
            _treeEntities[tree] = e;
        }

        foreach (var quarry in _sim.Quarries)
        {
            var e = new Entity($"Quarry_{quarry.Cell}") { new ModelComponent(MakeCubeModel(new Color(95, 95, 100))) };
            e.Transform.Scale = new Vector3(1.5f, 0.8f, 1.5f);
            e.Transform.Position = OnGround(quarry.Cell, 0.4f);
            scene.Entities.Add(e);
        }

        _siteEntity = new Entity("Keep") { new ModelComponent(MakeCubeModel(new Color(150, 140, 120))) };
        _siteEntity.Transform.Position = OnGround(_sim.Sites[0].Cell, 0.1f);
        scene.Entities.Add(_siteEntity);

        var palette = new[]
        {
            new Color(200, 70, 60), new Color(70, 110, 200), new Color(220, 150, 50),
            new Color(90, 180, 90), new Color(180, 90, 190), new Color(210, 200, 80),
        };
        int i = 0;
        foreach (var worker in _sim.Workers)
        {
            var e = new Entity($"Worker_{worker.Name}") { new ModelComponent(MakeCubeModel(palette[i++ % palette.Length])) };
            e.Transform.Scale = new Vector3(0.7f, 1.2f, 0.7f);
            e.Transform.Position = OnGround(worker.Cell, 0.6f);
            scene.Entities.Add(e);
            _workerEntities[worker] = e;
        }
    }

    private void SyncEntities(float dt)
    {
        float lerp = Math.Min(1f, dt * 8f);

        foreach (var (worker, e) in _workerEntities)
        {
            var target = OnGround(worker.Cell, 0.6f);
            e.Transform.Position = Vector3.Lerp(e.Transform.Position, target, lerp);
        }

        foreach (var (tree, e) in _treeEntities)
        {
            var model = e.Get<ModelComponent>();
            if (tree.Depleted && model.Enabled)
                model.Enabled = false;
        }

        var site = _sim.Sites[0];
        float targetHeight = 0.4f + site.Progress * 4f;
        var scale = _siteEntity.Transform.Scale;
        scale.X = 1.6f;
        scale.Z = 1.6f;
        scale.Y = MathUtil.Lerp(scale.Y <= 0 ? targetHeight : scale.Y, targetHeight, lerp);
        _siteEntity.Transform.Scale = scale;
        _siteEntity.Transform.Position = OnGround(site.Cell, scale.Y / 2f);
    }

    private void HandleWindowInput()
    {
        if (Input.IsKeyPressed(Keys.Escape))
            UnlockMouse();
        if (!Input.IsMousePositionLocked && Input.IsMouseButtonPressed(MouseButton.Left))
            LockMouse();
        if (Input.IsKeyPressed(Keys.Enter) && (Input.IsKeyDown(Keys.LeftAlt) || Input.IsKeyDown(Keys.RightAlt)))
            ToggleFullscreen();
    }

    private void LockMouse()
    {
        Input.LockMousePosition(forceCenter: true);
        ((Stride.Engine.Game)Game).IsMouseVisible = false;
    }

    private void UnlockMouse()
    {
        Input.UnlockMousePosition();
        ((Stride.Engine.Game)Game).IsMouseVisible = true;
    }

    private void ToggleFullscreen()
    {
        var manager = ((Stride.Engine.Game)Game).GraphicsDeviceManager;
        if (manager.IsFullScreen)
        {
            manager.IsFullScreen = false;
            manager.PreferredBackBufferWidth = 1280;
            manager.PreferredBackBufferHeight = 720;
        }
        else
        {
            var mode = GraphicsDevice.Adapter.Outputs[0].CurrentDisplayMode;
            manager.PreferredBackBufferWidth = mode.Width;
            manager.PreferredBackBufferHeight = mode.Height;
            manager.IsFullScreen = true;
        }
        manager.ApplyChanges();
    }

    private void MovePlayer(float dt)
    {
        if (Input.IsMousePositionLocked)
        {
            var d = Input.MouseDelta;
            _yaw -= d.X * LookSpeed;
            _pitch = MathUtil.Clamp(_pitch - d.Y * LookSpeed,
                -MathUtil.PiOverTwo + 0.05f, MathUtil.PiOverTwo - 0.05f);
            _cameraEntity.Transform.Rotation = Quaternion.RotationYawPitchRoll(_yaw, _pitch, 0f);
        }

        var forward = new Vector3(-MathF.Sin(_yaw), 0f, -MathF.Cos(_yaw));
        var right = Vector3.Cross(forward, Vector3.UnitY);

        var move = Vector3.Zero;
        if (Input.IsKeyDown(Keys.W)) move += forward;
        if (Input.IsKeyDown(Keys.S)) move -= forward;
        if (Input.IsKeyDown(Keys.D)) move += right;
        if (Input.IsKeyDown(Keys.A)) move -= right;
        if (move.LengthSquared() < 1e-4f)
        {
            StickToGround();
            return;
        }

        move.Normalize();
        float speed = WalkSpeed * (Input.IsKeyDown(Keys.LeftShift) ? RunMultiplier : 1f);

        var pos = _cameraEntity.Transform.Position;
        var next = pos + move * speed * dt;
        next.X = MathUtil.Clamp(next.X, 0.3f, _sim.Map.Width * Tile - 0.3f);
        next.Z = MathUtil.Clamp(next.Z, 0.3f, _sim.Map.Height * Tile - 0.3f);

        // Axis-separated collision against blocked cells, so the player slides along obstacles.
        if (!IsBlockedAt(next.X, pos.Z)) pos.X = next.X;
        if (!IsBlockedAt(pos.X, next.Z)) pos.Z = next.Z;
        pos.Y = _terrain.HeightAt(pos.X, pos.Z) + EyeHeight;
        _cameraEntity.Transform.Position = pos;
    }

    private void StickToGround()
    {
        var pos = _cameraEntity.Transform.Position;
        pos.Y = _terrain.HeightAt(pos.X, pos.Z) + EyeHeight;
        _cameraEntity.Transform.Position = pos;
    }

    private bool IsBlockedAt(float x, float z)
    {
        var cell = new Cell((int)MathF.Floor(x / Tile), (int)MathF.Floor(z / Tile));
        return _sim.Map.InBounds(cell) && _sim.Map.IsBlocked(cell);
    }

    private void DrawHud()
    {
        var site = _sim.Sites[0];
        string status = site.Complete
            ? $"{site.Name}: built!"
            : $"{site.Name}: wood {site.Outstanding(ResourceKind.Wood)}, stone {site.Outstanding(ResourceKind.Stone)}, build {site.Progress * 100:0}%";
        DebugText.Print(status, new Int2(15, 15));

        int y = 40;
        foreach (var w in _sim.Workers)
        {
            string carry = w.Carry is { } c ? $" (+{c.Amount} {c.Kind})" : "";
            DebugText.Print($"{w.Name}: {w.Activity}{carry}", new Int2(15, y));
            y += 22;
        }

        DebugText.Print("WASD move, Shift run | Esc release cursor, LMB capture | Alt+Enter fullscreen",
            new Int2(15, y + 8));
    }

    private Vector3 OnGround(Cell c, float yAbove)
    {
        float x = c.X * Tile + Tile / 2f;
        float z = c.Y * Tile + Tile / 2f;
        return new Vector3(x, _terrain.HeightAt(x, z) + yAbove, z);
    }

    private Model MakeCubeModel(Color color)
    {
        var generator = new CubeProceduralModel { Size = Vector3.One };
        generator.MaterialInstance.Material = MakeMaterial(color, doubleSided: false);
        return generator.Generate(Services);
    }

    private Material MakeMaterial(Color color, bool doubleSided)
    {
        var attributes = new MaterialAttributes
        {
            Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(color.ToColor4())),
            DiffuseModel = new MaterialDiffuseLambertModelFeature(),
        };
        if (doubleSided)
            attributes.CullMode = CullMode.None;

        return Material.New(GraphicsDevice, new MaterialDescriptor { Attributes = attributes });
    }
}

/// <summary>
/// A smooth analytic height field: a flat field on the left ramping up into a hill
/// on the right (where the forest sits). Sampled finely to build a smooth mesh, and
/// queried to sit entities and the player on the surface.
/// </summary>
public sealed class TerrainField
{
    private readonly float _worldW;
    private readonly float _worldD;
    private readonly float _hillHeight;

    public TerrainField(float worldW, float worldD, float hillHeight)
    {
        _worldW = worldW;
        _worldD = worldD;
        _hillHeight = hillHeight;
    }

    public float HeightAt(float x, float z)
    {
        float tx = MathUtil.Clamp(x / _worldW, 0f, 1f);
        float tz = MathUtil.Clamp(z / _worldD, 0f, 1f);

        float ramp = Smoothstep(0.30f, 0.95f, tx);          // rises toward the forest (+X)
        float dome = 1f - (2f * tz - 1f) * (2f * tz - 1f);   // gentle crown across depth, 0 at edges
        return _hillHeight * ramp * (0.75f + 0.25f * dome);
    }

    public Vector3 NormalAt(float x, float z)
    {
        const float e = 0.5f;
        float hL = HeightAt(x - e, z), hR = HeightAt(x + e, z);
        float hD = HeightAt(x, z - e), hU = HeightAt(x, z + e);
        var n = new Vector3(hL - hR, 2f * e, hD - hU);
        n.Normalize();
        return n;
    }

    public Model BuildModel(GraphicsDevice device, Material material, float step)
    {
        int cols = Math.Max(1, (int)MathF.Round(_worldW / step));
        int rows = Math.Max(1, (int)MathF.Round(_worldD / step));
        float sx = _worldW / cols;
        float sz = _worldD / rows;

        var vertices = new VertexPositionNormalTexture[(cols + 1) * (rows + 1)];
        var positions = new Vector3[vertices.Length];
        int v = 0;
        for (int r = 0; r <= rows; r++)
        for (int c = 0; c <= cols; c++)
        {
            float x = c * sx, z = r * sz, y = HeightAt(x, z);
            var pos = new Vector3(x, y, z);
            positions[v] = pos;
            vertices[v] = new VertexPositionNormalTexture(pos, NormalAt(x, z), new Vector2((float)c / cols, (float)r / rows));
            v++;
        }

        var indices = new int[cols * rows * 6];
        int i = 0;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            int i00 = r * (cols + 1) + c;
            int i10 = i00 + 1;
            int i01 = i00 + (cols + 1);
            int i11 = i01 + 1;
            indices[i++] = i00; indices[i++] = i01; indices[i++] = i11;
            indices[i++] = i00; indices[i++] = i11; indices[i++] = i10;
        }

        var vertexBuffer = Stride.Graphics.Buffer.Vertex.New(device, vertices);
        var indexBuffer = Stride.Graphics.Buffer.Index.New(device, indices);
        var draw = new MeshDraw
        {
            PrimitiveType = PrimitiveType.TriangleList,
            DrawCount = indices.Length,
            IndexBuffer = new IndexBufferBinding(indexBuffer, true, indices.Length),
            VertexBuffers = new[] { new VertexBufferBinding(vertexBuffer, VertexPositionNormalTexture.Layout, vertices.Length) },
        };

        var bounds = BoundingBox.FromPoints(positions);
        var mesh = new Mesh { Draw = draw, BoundingBox = bounds, MaterialIndex = 0 };

        var model = new Model { BoundingBox = bounds };
        model.Materials.Add(new MaterialInstance(material));
        model.Meshes.Add(mesh);
        return model;
    }

    private static float Smoothstep(float edge0, float edge1, float t)
    {
        float u = MathUtil.Clamp((t - edge0) / (edge1 - edge0), 0f, 1f);
        return u * u * (3f - 2f * u);
    }
}
