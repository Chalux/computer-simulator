using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Modeling;
using ComputerSimulator.Modules.Simulation;
using Godot;

namespace ComputerSimulator.Modules.Inspection;

/// <summary>
/// 装配视口。硬件外观全部来自外部 Blender 导出的 .glb 资产（res:// 走导入资源，
/// 磁盘资产走 GLTFDocument）；运行时只生成相机、灯光、地面与冲突调试网格，不再生成硬件几何。
/// </summary>
public partial class BenchViewport : SubViewportContainer
{
    private readonly Node3D _model = new();
    private readonly Camera3D _camera = new() { Projection = Camera3D.ProjectionType.Orthogonal, Near = 1, Far = 2400 };
    private readonly Dictionary<string, StandardMaterial3D> _materials = new();
    private readonly HardwareLibrary _hardware = new();
    private readonly Node3D _overlays = new();
    private readonly Dictionary<string, Mounted> _mounted = new();
    private readonly HashSet<string> _reportedMissing = new();

    private sealed record Mounted(string Path, Vector3 Origin, Vector3 Size, Node3D Root, HardwareModel Model);
    private sealed record MountSpec(string Id, string Role, string Path, Vector3 Origin, Vector3 Size);

    private Vector3 _frame = new(244, 160, 244);
    private bool _liquid;
    private bool _autoFramed = true;
    private bool _framedOnce;
    private Vector3 _target = new(122, 82, 122);
    private float _yaw = .93f, _pitch = .48f, _zoom = 350;
    private bool _showPanel;
    private bool _rgb = true;

    /// <summary>当前视图预设；手动调整相机时为 -1。</summary>
    public event Action<int>? ViewPresetChanged;

    public override void _Ready()
    {
        Stretch = true;
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = "左键拖动旋转 · 中键平移 · 滚轮缩放";
        var viewport = new SubViewport { OwnWorld3D = true, Size = new Vector2I(500, 350), Msaa3D = Viewport.Msaa.Msaa2X };
        AddChild(viewport);
        viewport.AddChild(_model);
        viewport.AddChild(_overlays);
        viewport.AddChild(_camera);
        viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("#15212a"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("#dee7ff"),
                AmbientLightEnergy = .65f,
                Sky = new Sky
                {
                    SkyMaterial = new ProceduralSkyMaterial
                    {
                        SkyTopColor = new Color("#8194ae"),
                        SkyHorizonColor = new Color("#8894a3"),
                        GroundHorizonColor = new Color("#67717f"),
                        GroundBottomColor = new Color("#494f60")
                    }
                },
                ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
                TonemapMode = Godot.Environment.ToneMapper.Aces
            }
        });
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, 30, 0), SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly, LightEnergy = 1.2f, LightSpecular = .40f, LightColor = new Color("#ffefdd"), ShadowEnabled = true, DirectionalShadowMaxDistance = 1250, ShadowBias = .025f });
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-28, -130, 0), SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly, LightEnergy = .70f, LightSpecular = .28f, LightColor = new Color("#b4d8ff") });
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://Resource/hardware/workmat.gdshader") };
        viewport.AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(4800, 4800) },
            MaterialOverride = mat,
            Position = new Vector3(140, -20, 150)
        });
        SetView(0);
    }

    public override void _ExitTree()
    {
        foreach (var id in _mounted.Keys.ToArray()) Unmount(id);
        _hardware.Dispose();
    }

    public void SetView(int view)
    {
        _autoFramed = view == 0;
        (_yaw, _pitch) = view switch
        {
            1 => (0f, 0f),
            2 => (Mathf.Pi / 2, 0f),
            3 => (0f, Mathf.Pi / 2 - .001f),
            _ => (.93f, .48f)
        };
        _target = new Vector3(_frame.X * .50f, _liquid ? 72 : 64, _frame.Z * .5f);
        _zoom = _liquid ? _frame.X * .72f + 222 : 310;
        if (_liquid && view == 0) { _yaw = .76f; _pitch = .58f; }
        if (view == 4)
        {
            _target = _liquid ? new Vector3(_frame.X * .67f, 70, 175) : new Vector3(117, 84, 119);
            _zoom = _liquid ? 275 : 203;
        }
        PositionCamera();
        ViewPresetChanged?.Invoke(view);
    }

    private void PositionCamera()
    {
        var direction = new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(_pitch), Mathf.Sin(_pitch), Mathf.Cos(_yaw) * Mathf.Cos(_pitch));
        _camera.Position = _target + direction * 700;
        _camera.LookAt(_target);
        _camera.Size = _zoom;
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseMotion motion)
        {
            if (motion.ButtonMask.HasFlag(MouseButtonMask.Left))
            {
                _yaw -= motion.Relative.X * .009f;
                _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * .009f, -1.5f, 1.5f);
            }
            else if (motion.ButtonMask.HasFlag(MouseButtonMask.Middle))
            {
                var basis = _camera.Transform.Basis;
                _target += (-basis.X * motion.Relative.X + basis.Y * motion.Relative.Y) * (_zoom / Mathf.Max(1, Size.Y));
            }
            else return;
        }
        else if (input is InputEventMouseButton { Pressed: true } button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp) _zoom = Mathf.Max(110, _zoom / 1.12f);
            else if (button.ButtonIndex == MouseButton.WheelDown) _zoom = Mathf.Min(680, _zoom * 1.12f);
            else return;
        }
        else return;
        _autoFramed = false;
        PositionCamera();
        ViewPresetChanged?.Invoke(-1);
        AcceptEvent();
    }

    /// <summary>显示或隐藏 Blender 资产中的透明侧板。</summary>
    public void ShowPanel(bool show)
    {
        _showPanel = show;
        if (_mounted.TryGetValue("case", out var mounted)) mounted.Model.SetSidePanelVisible(show);
    }

    /// <summary>ARGB/LED 节点的可见性切换。</summary>
    public void SetRgb(bool enabled)
    {
        _rgb = enabled;
        foreach (var mounted in _mounted.Values) mounted.Model.SetRgb(enabled);
    }

    /// <summary>按装配结果、目录与所选 SKU 刷新视口。</summary>
    public void Display(FitResult report, BenchCatalog catalog, BenchSetup setup)
    {
        var panel = report.Parts.FirstOrDefault(p => p.Role == "panel");
        var coverSize = panel is null ? Vector3.Zero : Vector(panel.Size);
        bool liquid = report.GpuLimit.HasValue;
        bool reframe = liquid != _liquid || !_framedOnce || (_autoFramed && coverSize.X != _frame.X);
        _liquid = liquid;
        if (panel is not null) _frame = new Vector3(coverSize.X, panel.Origin[1], coverSize.Z);
        if (reframe)
        {
            SetView(0);
            _framedOnce = true;
        }

        var specs = BuildSpecs(report, catalog, setup);
        foreach (var id in _mounted.Keys.ToArray())
        {
            var current = _mounted[id];
            if (!specs.TryGetValue(id, out var spec))
            {
                Unmount(id);
                continue;
            }
            if (spec.Path == current.Path && spec.Size == current.Size)
            {
                if (spec.Origin != current.Origin)
                {
                    current.Root.Position = spec.Origin;
                    _mounted[id] = current with { Origin = spec.Origin };
                }
                continue;
            }
            Unmount(id);
        }

        foreach (var spec in specs.Values)
        {
            if (_mounted.ContainsKey(spec.Id)) continue;
            if (_hardware.TryCreate(spec.Path) is not { } instance)
            {
                ReportMissing(spec.Path);
                continue;
            }

            var root = new Node3D { Name = spec.Id, Position = spec.Origin };
            _model.AddChild(root);
            root.AddChild(instance);
            var model = new HardwareModel(instance);
            if (!model.Fit(spec.Size, preserveHeight: spec.Role is "case" or "board",
                    alignTop: spec.Role == "case"))
            {
                _model.RemoveChild(root);
                root.QueueFree();
                ReportMissing(spec.Path);
                continue;
            }

            model.SetRgb(_rgb);
            if (spec.Role == "case") model.SetSidePanelVisible(_showPanel);
            _mounted[spec.Id] = new Mounted(spec.Path, spec.Origin, spec.Size, root, model);
        }

        BuildOverlays(report);
    }

    private Dictionary<string, MountSpec> BuildSpecs(FitResult report, BenchCatalog catalog, BenchSetup setup)
    {
        var specs = new Dictionary<string, MountSpec>(StringComparer.Ordinal);

        var panel = report.Parts.FirstOrDefault(p => p.Role == "panel");
        if (panel is not null && Resolve(catalog, setup, "panel") is { } casePath)
        {
            var (origin, size) = Envelope(report, panel);
            specs["case"] = new MountSpec("case", "case", casePath, origin, size);
        }

        foreach (var part in report.Parts)
        {
            // 侧板由机箱模型的 SidePanel 节点承担；插槽通常烘焙在主板模型里，避免重复叠放。
            // 处理器/存储默认同样烘焙在主板里，只有当规则结果为它们给出独立占位时才加装。
            if (part.Role is "panel" or "slot") continue;
            if (Resolve(catalog, setup, part.Role) is not { } path) continue;
            specs[part.Id] = new MountSpec(part.Id, part.Role, path, Vector(part.Origin), Vector(part.Size));
        }

        AddAnchored("cpu", catalog.Board.ProcessorAnchor, Vector3.Zero);
        AddAnchored("storage", catalog.Board.StorageAnchor, Vector3.Zero);
        if (setup.Front360)
        {
            var supply = catalog.PowerSupplies[setup.PsuIndex];
            AddAnchored("psu", catalog.Cases[setup.CaseIndex].PsuAnchor,
                new Vector3(supply.Length, supply.Width, supply.Height));
        }

        return specs;

        void AddAnchored(string role, float[]? anchor, Vector3 size)
        {
            if (anchor is { Length: 3 } && Resolve(catalog, setup, role) is { } path)
                specs[role] = new MountSpec(role, role, path, Vector(anchor), size);
        }
    }

    private static string? Resolve(BenchCatalog catalog, BenchSetup setup, string role)
    {
        try
        {
            return catalog.GetModelPath(setup, role);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"解析硬件资产路径失败（{role}）：{ex.Message}");
            return null;
        }
    }

    /// <summary>机箱可视包络：宽/深取侧板尺寸，高取侧板限高，下沿覆盖主板等负向构件。</summary>
    private static (Vector3 Origin, Vector3 Size) Envelope(FitResult report, BenchPart panel)
    {
        float width = panel.Size[0];
        float depth = panel.Size[2];
        float top = panel.Origin[1];
        float bottom = 0;
        foreach (var part in report.Parts)
            if (part.Role != "panel") bottom = Mathf.Min(bottom, part.Origin[1]);
        return (new Vector3(0, bottom, 0), new Vector3(width, top - bottom, depth));
    }

    private void BuildOverlays(FitResult report)
    {
        foreach (var child in _overlays.GetChildren())
        {
            _overlays.RemoveChild(child);
            child.QueueFree();
        }

        var bad = report.Issues.SelectMany(issue => issue.Parts).ToHashSet();
        foreach (var part in report.Parts.Where(part => bad.Contains(part.Id)))
        {
            var extent = Vector(part.Size);
            Box(Vector(part.Origin) + extent / 2, extent + Vector3.One * .22f, "#ff5b4512");
            Outline(part, "#ff6b5a");
        }

        foreach (var issue in report.Issues)
            if (issue.Overlap is { } overlap)
            {
                var extent = Vector(overlap.Size);
                Box(Vector(overlap.Origin) + extent / 2, extent, "#ff3c39a8");
                Outline(overlap, "#ffb4a6");
            }
    }

    private void ReportMissing(string path)
    {
        if (_reportedMissing.Add(path))
            GD.PushWarning($"硬件资产暂不可用，已跳过：{path}（资产就绪后自动加载）");
    }

    private void Unmount(string id)
    {
        if (!_mounted.Remove(id, out var mounted)) return;
        if (!GodotObject.IsInstanceValid(mounted.Root)) return;
        if (mounted.Root.GetParent() == _model) _model.RemoveChild(mounted.Root);
        mounted.Root.QueueFree();
    }

    private StandardMaterial3D GetMaterial(string color)
    {
        if (_materials.TryGetValue(color, out var material)) return material;
        material = new StandardMaterial3D { AlbedoColor = new Color(color), Roughness = .67f };
        if (material.AlbedoColor.A < 1) material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _materials.Add(color, material);
        return material;
    }

    private MeshInstance3D Box(Vector3 pos, Vector3 size, string color) =>
        AddMesh(new BoxMesh { Size = size }, pos, GetMaterial(color));

    private MeshInstance3D AddMesh(Mesh mesh, Vector3 pos, Material material)
    {
        var node = new MeshInstance3D { Position = pos, Mesh = mesh, MaterialOverride = material };
        _overlays.AddChild(node);
        return node;
    }

    private void Outline(Volume bounds, string color)
    {
        var extent = Vector(bounds.Size);
        var lines = new ImmediateMesh();
        lines.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (int i = 0; i < 8; i++)
        {
            Vector3 start = new(i & 1, (i >> 1) & 1, (i >> 2) & 1);
            foreach (var axis in new[] { Vector3.Right, Vector3.Up, Vector3.Back })
            {
                if (start.Dot(axis) != 0) continue;
                lines.SurfaceAddVertex(start * extent);
                lines.SurfaceAddVertex((start + axis) * extent);
            }
        }
        lines.SurfaceEnd();
        var mat = GetMaterial(color);
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.NoDepthTest = true;
        AddMesh(lines, Vector(bounds.Origin), mat);
    }

    private static Vector3 Vector(float[] xyz) => new(xyz[0], xyz[1], xyz[2]);
}
