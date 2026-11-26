using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Modeling;
using ComputerSimulator.Modules.Simulation;
using Godot;

namespace ComputerSimulator.Modules.Inspection;

public partial class BenchViewport : SubViewportContainer
{
    private readonly Node3D _model = new();
    private readonly Camera3D _camera = new() { Projection = Camera3D.ProjectionType.Orthogonal, Near = 1, Far = 2400 };
    private readonly Dictionary<string, StandardMaterial3D> _materials = new();
    private readonly HardwareLibrary _hardware = new();
    private sealed record Mounted(Node3D Node, Vector3 Extent, bool Rgb, int Fans, float FanThickness, float CoverHeight);
    private readonly Dictionary<string, Mounted> _mounted = new();
    private readonly Node3D _overlays = new();
    private Vector3 _frame = new(244, 160, 244);
    private bool _liquid;
    private bool _autoFramed = true;
    private Vector3 _target = new(122, 82, 122);
    private float _yaw = .93f, _pitch = .48f, _zoom = 350;
    private MeshInstance3D? _panel;
    private bool _showPanel;

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

    public void ShowPanel(bool show)
    {
        _showPanel = show;
        if (_panel != null) _panel.Visible = show;
    }

    public void SetRgb(bool enabled) => _hardware.Led.SetShaderParameter("powered", enabled);

    public void Display(FitResult report)
    {
        var cover = report.Parts.First(p => p.Role == "panel");
        bool liquid = report.GpuLimit.HasValue;
        bool reframe = liquid != _liquid || _mounted.Count == 0 || (_autoFramed && cover.Extent.X != _frame.X);
        _liquid = liquid;
        _frame = new Vector3(cover.Extent.X, cover.Corner.Y, cover.Extent.Z);
        if (reframe) SetView(0);
        var present = report.Parts.Select(p => p.Id).ToHashSet();
        foreach (var id in _mounted.Keys.Where(id => !present.Contains(id)).ToArray()) Unmount(id);
        foreach (var p in report.Parts)
        {
            float coverHeight = p.Role == "panel" ? p.Corner.Y : 0;
            if (_mounted.TryGetValue(p.Id, out var current))
            {
                if (current.Extent == p.Extent && current.Rgb == p.Rgb && current.Fans == p.Fans
                    && current.FanThickness == p.FanThickness && current.CoverHeight == coverHeight)
                {
                    current.Node.Position = p.Corner;
                    continue;
                }
                Unmount(p.Id);
            }
            var node = new Node3D { Name = p.Id, Position = p.Corner };
            _model.AddChild(node);
            var model = new HardwareModel(_hardware, node);
            if (p.Role == "panel")
            {
                var chassis = new Node3D { Position = Vector3.Down * coverHeight };
                node.AddChild(chassis);
                var kit = new HardwareModel(_hardware, chassis);
                kit.Chassis(p.Extent, coverHeight);
                if (liquid) BuildHoses(kit, report);
                _panel = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = p.Extent },
                    MaterialOverride = GetMaterial("#8dbfc92d"),
                    Position = p.Extent / 2,
                    Visible = _showPanel
                };
                node.AddChild(_panel);
            }
            else model.Build(p);
            _mounted[p.Id] = new(node, p.Extent, p.Rgb, p.Fans, p.FanThickness, coverHeight);
        }

        foreach (var child in _overlays.GetChildren()) { _overlays.RemoveChild(child); child.QueueFree(); }
        var bad = report.Issues.SelectMany(issue => issue.Parts).ToHashSet();
        foreach (var p in report.Parts.Where(p => bad.Contains(p.Id)))
        {
            Box(p.Center, p.Extent + Vector3.One * .22f, "#ff5b4512");
            Outline(p, "#ff6b5a");
        }
        foreach (var issue in report.Issues)
            if (issue.Overlap is { } overlap)
            {
                Box(overlap.Center, overlap.Extent, "#ff3c39a8");
                Outline(overlap, "#ffb4a6");
            }
    }

    private void Unmount(string id)
    {
        var node = _mounted[id].Node;
        _model.RemoveChild(node);
        node.QueueFree();
        _mounted.Remove(id);
    }

    private static void BuildHoses(HardwareModel kit, FitResult report)
    {
        var pump = report.Parts.First(p => p.Role == "pump");
        var radiator = report.Parts.First(p => p.Role == "radiator");
        for (int i = 0; i < 2; i++)
        {
            var start = pump.Center + new Vector3(12 * i - 6, 0, -pump.Extent.Z / 2);
            var end = radiator.Corner + new Vector3(0, 22 + i * 18, 12);
            kit.Pipe([start, start+new Vector3(4,16,-18), new Vector3(164,62+i*9,33-i*10),
                end+new Vector3(-38,20,0),end], 6.5f);
        }
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
        var lines = new ImmediateMesh();
        lines.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (int i = 0; i < 8; i++)
        {
            Vector3 start = new(i & 1, (i >> 1) & 1, (i >> 2) & 1);
            foreach (var axis in new[] { Vector3.Right, Vector3.Up, Vector3.Back })
            {
                if (start.Dot(axis) != 0) continue;
                lines.SurfaceAddVertex(start * bounds.Extent);
                lines.SurfaceAddVertex((start + axis) * bounds.Extent);
            }
        }
        lines.SurfaceEnd();
        var mat = GetMaterial(color);
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.NoDepthTest = true;
        AddMesh(lines, bounds.Corner, mat);
    }
}
