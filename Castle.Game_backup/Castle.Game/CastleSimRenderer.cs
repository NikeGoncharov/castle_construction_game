using System.Collections.Generic;
using Castle.Sim;
using Castle.Sim.Entities;
using Castle.Sim.Geometry;
using Castle.Sim.Workers;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;
using Stride.Rendering;
using Stride.Rendering.Lights;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Rendering.ProceduralModels;

namespace Castle.Game;

/// <summary>
/// Bridge between Castle.Sim and Stride. Builds a flat world of coloured cubes —
/// the original "Slice 1" visual before heightmaps and composed models.
/// </summary>
public class CastleSimRenderer : SyncScript
{
    private const float Tile = 2f;
    private const float MoveSpeed = 12f;
    private const float LookSpeed = 3f;

    private Simulation _sim = null!;
    private readonly Dictionary<Worker, Entity> _workerEntities = new();
    private readonly Dictionary<Tree, Entity> _treeEntities = new();
    private Entity _siteEntity = null!;

    private Entity _cameraEntity = null!;
    private float _yaw;
    private float _pitch = -0.5f;

    public override void Start()
    {
        _sim = Scenarios.ForestKeep();
        BuildEnvironment();
        BuildSimEntities();
    }

    public override void Update()
    {
        float dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        if (dt <= 0f) return;

        _sim.Tick(dt);
        SyncEntities(dt);
        MoveCamera(dt);
    }

    private void BuildEnvironment()
    {
        var scene = Entity.Scene;

        float w = _sim.Map.Width * Tile;
        float h = _sim.Map.Height * Tile;
        var ground = new Entity("Ground") { new ModelComponent(MakeCubeModel(new Color(76, 120, 64))) };
        ground.Transform.Scale = new Vector3(w + Tile, 0.4f, h + Tile);
        ground.Transform.Position = new Vector3((w - Tile) / 2f, -0.2f, (h - Tile) / 2f);
        scene.Entities.Add(ground);

        var sun = new Entity("Sun") { new LightComponent { Type = new LightDirectional(), Intensity = 1.1f } };
        sun.Transform.Rotation = Quaternion.RotationYawPitchRoll(0.7f, -1.0f, 0f);
        scene.Entities.Add(sun);

        _cameraEntity = new Entity("FreeCamera");
        var camera = new CameraComponent();
        if (SceneSystem.GraphicsCompositor?.Cameras.Count > 0)
            camera.Slot = SceneSystem.GraphicsCompositor.Cameras[0].ToSlotId();
        _cameraEntity.Add(camera);
        _cameraEntity.Transform.Position = new Vector3((w - Tile) / 2f, 16f, h + 10f);
        scene.Entities.Add(_cameraEntity);
        ApplyCameraRotation();
    }

    private void BuildSimEntities()
    {
        var scene = Entity.Scene;

        foreach (var tree in _sim.Trees)
        {
            var e = new Entity($"Tree_{tree.Cell}") { new ModelComponent(MakeCubeModel(new Color(34, 90, 34))) };
            e.Transform.Scale = new Vector3(0.7f, 2.2f, 0.7f);
            e.Transform.Position = CellToWorld(tree.Cell, 1.1f);
            scene.Entities.Add(e);
            _treeEntities[tree] = e;
        }

        _siteEntity = new Entity("Keep") { new ModelComponent(MakeCubeModel(new Color(150, 140, 120))) };
        _siteEntity.Transform.Position = CellToWorld(_sim.Sites[0].Cell, 0.1f);
        scene.Entities.Add(_siteEntity);

        var palette = new[] { new Color(200, 70, 60), new Color(70, 110, 200), new Color(220, 150, 50) };
        int i = 0;
        foreach (var worker in _sim.Workers)
        {
            var e = new Entity($"Worker_{worker.Name}") { new ModelComponent(MakeCubeModel(palette[i++ % palette.Length])) };
            e.Transform.Scale = new Vector3(0.7f, 1.2f, 0.7f);
            e.Transform.Position = CellToWorld(worker.Cell, 0.6f);
            scene.Entities.Add(e);
            _workerEntities[worker] = e;
        }
    }

    private void SyncEntities(float dt)
    {
        float lerp = System.Math.Min(1f, dt * 8f);

        foreach (var (worker, e) in _workerEntities)
        {
            var target = CellToWorld(worker.Cell, 0.6f);
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
        _siteEntity.Transform.Position = CellToWorld(site.Cell, scale.Y / 2f);
    }

    private void MoveCamera(float dt)
    {
        if (Input.IsMouseButtonDown(MouseButton.Right))
        {
            var d = Input.MouseDelta;
            _yaw -= d.X * LookSpeed;
            _pitch -= d.Y * LookSpeed;
            _pitch = MathUtil.Clamp(_pitch, -1.5f, 1.5f);
            ApplyCameraRotation();
        }

        var rot = _cameraEntity.Transform.Rotation;
        var forward = Vector3.Transform(-Vector3.UnitZ, rot);
        var right = Vector3.Transform(Vector3.UnitX, rot);

        var move = Vector3.Zero;
        if (Input.IsKeyDown(Keys.W)) move += forward;
        if (Input.IsKeyDown(Keys.S)) move -= forward;
        if (Input.IsKeyDown(Keys.D)) move += right;
        if (Input.IsKeyDown(Keys.A)) move -= right;
        if (Input.IsKeyDown(Keys.E)) move += Vector3.UnitY;
        if (Input.IsKeyDown(Keys.Q)) move -= Vector3.UnitY;

        if (move.LengthSquared() > 0)
        {
            move.Normalize();
            float speed = MoveSpeed * (Input.IsKeyDown(Keys.LeftShift) ? 2.5f : 1f);
            _cameraEntity.Transform.Position += move * speed * dt;
        }
    }

    private void ApplyCameraRotation()
        => _cameraEntity.Transform.Rotation = Quaternion.RotationYawPitchRoll(_yaw, _pitch, 0f);

    private Vector3 CellToWorld(Cell c, float yOffset)
        => new Vector3(c.X * Tile + Tile / 2f, yOffset, c.Y * Tile + Tile / 2f);

    private Model MakeCubeModel(Color color)
    {
        var material = Material.New(GraphicsDevice, new MaterialDescriptor
        {
            Attributes = new MaterialAttributes
            {
                Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(color.ToColor4())),
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
            }
        });

        var generator = new CubeProceduralModel { Size = Vector3.One };
        generator.MaterialInstance.Material = material;
        return generator.Generate(Services);
    }
}
