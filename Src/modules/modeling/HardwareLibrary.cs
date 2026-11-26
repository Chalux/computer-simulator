using Godot;

namespace ComputerSimulator.Modules.Modeling;

// Shared mesh library. Repeated fins, fasteners, electronics and blades are instanced per material.
internal sealed class HardwareLibrary
{
    public readonly Mesh Cube = new BoxMesh();
    public readonly Mesh Barrel = new CylinderMesh { TopRadius = .5f, BottomRadius = .5f, Height = 1, RadialSegments = 28, Rings = 0 };
    public readonly Mesh Shell = MachinedMeshes.FanShell(false), Ring = MachinedMeshes.FanShell(true), Blades = MachinedMeshes.Impeller();
    public readonly ShaderMaterial Led = new() { Shader = GD.Load<Shader>("res://Resource/hardware/argb.gdshader") };
    private readonly Dictionary<(Vector3, float, float), Mesh> _profiles = new();
    private readonly Dictionary<(Vector3, int), Mesh> _fascias = new();
    private readonly Dictionary<string, StandardMaterial3D> _materials = new();

    public Mesh Profile(Vector3 size, float corner, float bevel)
    {
        var key = (size, corner, bevel);
        if (!_profiles.TryGetValue(key, out var mesh)) _profiles[key] = mesh = MachinedMeshes.Enclosure(size, corner, bevel);
        return mesh;
    }

    public Mesh Fascia(Vector3 extent, int count, float radius)
    {
        var key = (extent, count);
        if (!_fascias.TryGetValue(key, out var mesh)) _fascias[key] = mesh = MachinedMeshes.CardFascia(extent.X, extent.Y, count, radius);
        return mesh;
    }

    public Material Finish(string key)
    {
        if (key == "led") return Led;
        if (_materials.TryGetValue(key, out var mat)) return mat;
        (string color, float metal, float rough) = key switch
        {
            "shell" => ("#333d4c", .45f, .34f),
            "black" => ("#161c26", .12f, .42f),
            "inset" => ("#0e131a", .05f, .48f),
            "graphite" => ("#576371", .69f, .33f),
            "fin" => ("#86929e", .63f, .48f),
            "edge" => ("#cdd9de", .72f, .27f),
            "pipe" => ("#bd815c", .65f, .29f),
            "gold" => ("#d4ac6a", .60f, .34f),
            "pcb" => ("#163532", .12f, .44f),
            "trace" => ("#476256", .45f, .43f),
            "silk" => ("#bbc5c3", .1f, .55f),
            "blade" => ("#657080", .18f, .43f),
            "teal" => ("#445b72", .67f, .31f),
            "chip" => ("#272b2f", .06f, .64f),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "未知硬件材质")
        };
        return _materials[key] = new StandardMaterial3D { AlbedoColor = new Color(color), Metallic = metal, Roughness = rough };
    }
}
