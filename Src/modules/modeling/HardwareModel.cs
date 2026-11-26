using ComputerSimulator.Modules.Hardware;
using Godot;

namespace ComputerSimulator.Modules.Modeling;

internal sealed class HardwareModel
{
    private readonly HardwareLibrary _lib;
    private readonly Node3D _root;
    private readonly Dictionary<(Mesh Mesh, string Finish), List<Transform3D>> _batches = new();
    private static Basis Turn(Vector3 axis, float angle) => new(axis, angle);
    public HardwareModel(HardwareLibrary library, Node3D root) { _lib = library; _root = root; }

    private void Place(Mesh mesh, Vector3 pos, Vector3 scale, string finish, Basis? basis = null)
    {
        var key = (mesh, finish);
        if (!_batches.TryGetValue(key, out var batch)) _batches[key] = batch = [];
        batch.Add(new Transform3D((basis ?? Basis.Identity) * Basis.FromScale(scale), pos));
    }
    private void Box(Vector3 c, Vector3 size, string finish, Basis? basis = null) =>
        Place(_lib.Cube, c, size, finish, basis);

    private void Body(Vector3 c, Vector3 size, float corner, float bevel, string finish, Basis? basis = null) =>
        Place(_lib.Profile(size, corner, bevel), c, Vector3.One, finish, basis);

    private void Cylinder(Vector3 c, float diameter, float height, string finish, Basis? basis = null) =>
        Place(_lib.Barrel, c, new(diameter, height, diameter), finish, basis);

    private void Rod(Vector3 a, Vector3 b, float diameter, string finish)
    {
        var dir = b - a;
        var rotation = new Basis(new Quaternion(Vector3.Up, dir.Normalized()));
        Cylinder((a + b) / 2, diameter, dir.Length(), finish, rotation);
    }

    private void Screw(Vector3 center, float diameter, Basis face)
    {
        Cylinder(center, diameter, 1.3f, "graphite", face);
        var top = center + face.Y * .7f;
        Box(top, new(diameter * .60f, .16f, diameter * .12f), "inset", face);
        Box(top, new(diameter * .12f, .16f, diameter * .60f), "inset", face);
    }

    // Local fan coordinates: shaft X, swept blades in YZ.
    private void Fan(Vector3 c, float span, float depth, Basis face, bool lit, bool framed = true)
    {
        Vector3 At(float x, float y, float z) => c + face * new Vector3(x * depth, y * span, z * span);
        var scale = new Vector3(depth, span, span);
        if (framed) Place(_lib.Shell, c, scale, "black", face);
        Place(_lib.Blades, c, scale, "blade", face);
        Place(_lib.Ring, c, scale, lit ? "led" : "graphite", face);
        var shaft = face * Turn(Vector3.Back, -Mathf.Pi / 2);
        Cylinder(At(.04f, 0, 0), span * .278f, depth * .75f, "black", shaft);
        Cylinder(At(.423f, 0, 0), span * .223f, depth * .025f, "graphite", shaft);
        Cylinder(At(.439f, 0, 0), span * .152f, depth * .012f, "black", shaft);
        for (int i = 0; i < 3; i++)
        {
            var detail = face * Turn(Vector3.Right, -.45f);
            Box(At(.454f, (i - 1) * .022f, 0), new(depth * .013f, span * .009f, span * (.075f - .013f * i)), "edge", detail);
        }
        if (!framed) return;
        foreach (int y in new[] { -1, 1 })
            foreach (int z in new[] { -1, 1 })
            {
                var screw = At(.46f, y * .405f, z * .405f);
                Cylinder(screw, span * .105f, depth * .058f, "inset", shaft);
                Screw(screw + shaft.Y * (depth * .035f), span * .061f, shaft);
                Rod(At(-.40f, y * .09f, z * .09f), At(-.41f, y * .34f, z * .31f), span * .029f, "shell");
            }
    }

    public void Build(BenchPart p)
    {
        var e = p.Extent;
        switch (p.Role)
        {
            case "board": Board(e); break;
            case "tower": Tower(e); break;
            case "fan": Fan(e / 2, e.Y, e.X, Basis.Identity, true); break;
            case "slot": Slot(e); break;
            case "ram": Memory(e, p.Rgb); break;
            case "gpu": GraphicsCard(e, p.Fans); break;
            case "radiator": Radiator(e, p.Fans, p.FanThickness); break;
            case "pump": Pump(e); break;
        }
        Flush();
    }

    public void Chassis(Vector3 e, float coverHeight)
    {
        float x = e.X / 2, z = e.Z / 2;
        Body(new(x, -9, z), new(e.X + 14, 5, e.Z + 14), 6, 1.3f, "shell");
        Body(new(x, -6.7f, z), new(e.X + 7, 2, e.Z + 7), 5, .6f, "black");
        // Folded flanges leave the near edges open for inspection.
        foreach (float sx in new[] { -5f, e.X + 5 })
        {
            Body(new(sx, coverHeight / 2 - 5, -5), new(7, coverHeight + 10, 8), 2, 1, "graphite");
            Body(new(sx, coverHeight * .55f, -2), new(10, coverHeight * .87f, 2), 1, .5f, "shell");
            Body(new(sx, -1, z), new(5, 15, e.Z + 8), 1.5f, .7f, "graphite");
        }
        Body(new(x, coverHeight, -5), new(e.X + 16, 5, 8), 2, .8f, "graphite");
        foreach (float sx in new[] { 10f, e.X - 10 })
            foreach (float sz in new[] { 10f, e.Z - 10 }) Cylinder(new(sx, -15, sz), 15, 9, "black");
        // Elongated cable grommets and stamped ventilation slots in the exposed tray.
        if (e.X > 300)
        {
            for (int i = 0; i < 3; i++)
            {
                float vz = 45 + i * 103;
                Body(new(269, -4.9f, vz), new(25, 1.5f, 56), 9, .5f, "graphite");
                Body(new(269, -4.0f, vz), new(21, .9f, 50), 8, .28f, "inset");
                for (int rib = 0; rib < 5; rib++) Box(new(269, -3.5f, vz + (rib - 2) * 8), new(18, .25f, .85f), "shell");
            }
            for (int i = 0; i < 18; i++) for (int col = 0; col < 3; col++)
                Body(new(41 + i * 12, -5.5f, 291 + col * 10), new(9, 1.1f, 3), 1.2f, .25f, "inset");
            Body(new(-4, 54, 229), new(5, 126, 77), 1.4f, .5f, "graphite");
            for (int i = 0; i < 4; i++)
            {
                Body(new(-1, 55, 200 + i * 18), new(2, 94, 13), .8f, .3f, "shell");
                for (int j = 0; j < 7; j++) Box(new(.1f, 21 + j * 10, 200 + i * 18), new(.4f, 5, 9), "inset");
            }
        }
        Flush();
    }

    public void Pipe(Vector3[] points, float bore)
    {
        var curve = new Curve3D();
        for (int i = 0; i < points.Length; i++)
        {
            var tangent = (points[Math.Min(i + 1, points.Length - 1)] - points[Math.Max(i - 1, 0)]) * .15f;
            curve.AddPoint(points[i], -tangent, tangent);
        }
        float length = curve.GetBakedLength();
        int steps = Mathf.CeilToInt(length / 5);
        for (int i = 0; i < steps; i++)
        {
            var a = curve.SampleBaked(length * i / steps, true);
            var b = curve.SampleBaked(length * (i + 1) / steps, true);
            Rod(a, b, bore, "black");
            if (i % 2 == 0) Rod(a, a.Lerp(b, .26f), bore + .3f, "graphite");
        }
        Flush();
    }

    private void Flush()
    {
        foreach (var ((mesh, finish), transforms) in _batches)
        {
            if (transforms.Count == 1)
            {
                _root.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = _lib.Finish(finish), Transform = transforms[0] });
                continue;
            }
            var instances = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = mesh,
                InstanceCount = transforms.Count
            };
            for (int i = 0; i < transforms.Count; i++) instances.SetInstanceTransform(i, transforms[i]);
            _root.AddChild(new MultiMeshInstance3D { Multimesh = instances, MaterialOverride = _lib.Finish(finish) });
        }
        _batches.Clear();
    }
    private void Print(string text, Vector3 pos, float pixelSize, Basis plane)
    {
        _root.AddChild(new Label3D
        {
            Text = text,
            FontSize = 32,
            PixelSize = pixelSize,
            Modulate = new Color("#c5cdcf"),
            OutlineSize = 0,
            DoubleSided = false,
            Transform = new Transform3D(plane, pos)
        });
    }

    private void Board(Vector3 e)
    {
        Body(e / 2, e, 4, .35f, "pcb");
        var top = Turn(Vector3.Right, -Mathf.Pi / 2);
        // Fanned-out board traces between slot banks and controller packages.
        for (int lane = 0; lane < 14; lane++)
        {
            float x = 193 + lane * 2.4f, z = 22 + lane * 1.35f;
            var a = new Vector3(x, e.Y + .12f, z);
            var b = a + new Vector3(0, 0, 59);
            var c = b + new Vector3(-13, 0, 13);
            Rod(a, b, .47f, "trace"); Rod(b, c, .47f, "trace"); Rod(c, c + Vector3.Back * 35, .47f, "trace");
            Cylinder(a, 1.3f, .3f, "gold");
        }
        for (int i = 0; i < 10; i++)
        {
            var a = new Vector3(31 + i * 2, 2.14f, 157 + i * 2);
            var b = a + new Vector3(23, 0, 23);
            Rod(a, b, .5f, "trace"); Rod(b, b + Vector3.Right * 44, .5f, "trace");
        }
        foreach (float x in new[] { 7f, e.X - 7 })
            foreach (float z in new[] { 7f, e.Z - 7 })
            {
                Cylinder(new(x, e.Y + .3f, z), 8, .6f, "gold"); Screw(new(x, e.Y + 1, z), 5, Basis.Identity);
            }
        // SMD banks, VRM chokes and polarized capacitors.
        for (int i = 0; i < 8; i++)
        {
            float x = 42 + 11 * i;
            Body(new(x, 7, 38), new(8, 10, 10), 1.1f, .5f, "graphite");
            Box(new(x, 3.7f, 29), new(6, 3, 4), "chip");
            Cylinder(new(x, 7, 49), 6.4f, 10, "edge");
            Box(new(x, 12.1f, 49), new(3, .2f, 1), "black");
        }
        for (int i = 0; i < 42; i++)
        {
            float x = 199 + (i % 6) * 4, z = 141 + (i / 6) * 4.8f;
            Box(new(x, 2.6f, z), new(2.7f, 1, 1.25f), "edge");
            Box(new(x, 2.8f, z), new(1.6f, 1.15f, 1.3f), "graphite");
        }
        Body(new(24, 9, 111), new(24, 15, 69), 3, 1, "shell");
        for (int i = 0; i < 8; i++) Box(new(24, 17, 83 + i * 8), new(21, 2, 2), "graphite");
        for (int i = 0; i < 4; i++)
        {
            float z = 22 + i * 22;
            Body(new(9, 9, z), new(18, 14, 17), 1, .65f, "edge");
            Box(new(-.1f, 9, z), new(.8f, 8, 12), "inset");
        }
        for (int i = 0; i < 2; i++)
        {
            float z = 206 + i * 20;
            Body(new(85, 4.8f, z), new(101, 5.8f, 7), 1.1f, .5f, "graphite");
            Box(new(85, 7.8f, z), new(93, .4f, 2), "inset");
            Box(new(136, 7, z), new(6, 6, 7), "black");
        }
        Body(new(202, 6, 205), new(50, 8, 47), 5, 1.3f, "shell");
        for (int i = 0; i < 5; i++) Box(new(190 + i * 6, 10.5f, 205), new(2.3f, 1, 28), "graphite", Turn(Vector3.Up, .22f));
        var power = new Vector3(229, 7, 111);
        Body(power, new(10, 11, 41), 1, .6f, "black");
        for (int r = 0; r < 2; r++) for (int k = 0; k < 12; k++)
            Box(power + new Vector3((r - .5f) * 4, 5.6f, (k - 5.5f) * 3.1f), new(2.8f, .4f, 2.5f), "inset");
        Body(new(98, 5, 120), new(61, 6, 61), 3, 1, "edge");
        Body(new(98, 11, 120), new(46, 7, 46), 3, 1, "pipe");
        Print("M24   /   DDR5", new(97, 2.55f, 191), .18f, top);
        Print("A1   A2     B1   B2", new(154, 2.5f, 48), .13f, top);
    }

    private void Tower(Vector3 e)
    {
        var c = e / 2;
        // Slim stamped aluminium plates leave visible air between their edges.
        const int count = 32;
        for (int i = 0; i < count; i++)
        {
            float y = 1 + i * (e.Y - 8) / (count - 1);
            Body(new(c.X, y, c.Z), new(e.X, 1.05f, e.Z), 4, .20f, "fin");
        }
        for (int i = 0; i < 5; i++)
        {
            float z = e.Z * (i + 1) / 6;
            Cylinder(new(c.X, (e.Y - 5) / 2, z), 4.9f, e.Y - 5, "pipe");
            var p = new Vector3(c.X, -2, z);
            Rod(p, p + new Vector3(0, -15, 0), 5, "pipe");
            Rod(p + new Vector3(0, -15, 0), p + new Vector3(0, -24, c.Z * .62f - z * .62f), 5, "pipe");
        }
        Body(new(c.X, e.Y - 3, c.Z), new(e.X, 6, e.Z), 4.5f, 1.25f, "shell");
        Body(new(c.X, e.Y - 1.1f, c.Z), new(e.X - 6, 2.1f, e.Z - 10), 3, .55f, "graphite");
        var top = Turn(Vector3.Right, -Mathf.Pi / 2) * Turn(Vector3.Back, Mathf.Pi / 2);
        Print("S P E C T R A    /    D154", new(c.X, e.Y + .12f, c.Z), .17f, top);
        foreach (float z in new[] { 8f, e.Z - 8 })
            foreach (float x in new[] { 6f, e.X - 6 }) Screw(new(x, e.Y - .6f, z), 3.5f, Basis.Identity);
        for (int i = 0; i < 5; i++)
            Box(new(c.X, e.Y + .012f, 15 + i * 2.6f), new(e.X - 13, .15f, .8f), "black");
    }

    private void Slot(Vector3 e)
    {
        Body(e / 2, e, 1, .35f, "black");
        Box(new(e.X / 2, e.Y + .05f, e.Z / 2), new(2, .15f, e.Z - 6), "inset");
        for (int k = 0; k < 2; k++)
            Body(new(e.X / 2, e.Y * .78f, k == 0 ? 1 : e.Z - 1), new(e.X, 5, 4), .8f, .45f, "edge");
    }

    private void Memory(Vector3 e, bool rgb)
    {
        var c = e / 2;
        Body(new(c.X, e.Y * .44f, c.Z), new(1.8f, e.Y * .88f, e.Z), 2, .45f, "pcb");
        for (int i = 0; i < 30; i++)
            Box(new(c.X, 3.5f, 4 + i * (e.Z - 8) / 29), new(2.05f, 5.6f, 1.1f), "gold");
        var face = Turn(Vector3.Back, -Mathf.Pi / 2);
        Body(new(c.X, c.Y + 2, c.Z), new(e.Y - 8, e.X - 1, e.Z - 2), 5, 1.1f, "teal", face);
        for (int i = 0; i < 5; i++)
        {
            float z = 15 + 9 * i;
            Box(new(e.X - .35f, c.Y + 3, z), new(.6f, e.Y * .54f, 2.2f), "graphite", Turn(Vector3.Right, -.38f));
        }
        Body(new(c.X, e.Y - 2.5f, c.Z), new(e.X, 5, e.Z - 6), 2, .8f, rgb ? "led" : "graphite");
        Print("PRISM   //   DDR5", new(e.X - .25f, c.Y + 2, e.Z * .69f), .15f, Turn(Vector3.Up, Mathf.Pi / 2));
    }

    private void GraphicsCard(Vector3 e, int count)
    {
        var c = e / 2;
        var front = Turn(Vector3.Up, -Mathf.Pi / 2);
        // Axial fans face +Z. A backplate, PCB, fin bank and shroud fill the actual slot thickness.
        Body(new(c.X, c.Y, 2.4f), new(e.X, 4.8f, e.Y), 7, 1, "shell", Turn(Vector3.Right, Mathf.Pi / 2));
        Box(new(c.X, c.Y, 7), new(e.X - 7, e.Y - 10, 2), "pcb");
        for (int i = 0; i < 68; i++)
        {
            float x = 5 + i * (e.X - 10) / 67;
            Body(new(x, c.Y, e.Z * .45f), new(1.6f, e.Y - 12, e.Z * .52f), .5f, .2f, "fin");
        }
        float fanSize = Mathf.Min(e.X / count - 6, e.Y - 10), depth = 14;
        for (int i = 0; i < count; i++) Fan(new((i + .5f) * e.X / count, c.Y, e.Z - depth / 2 - .8f), fanSize, depth, front, false, false);
        Place(_lib.Fascia(e, count, fanSize * .452f), new(0, 0, e.Z), Vector3.One, "shell");
        // Shroud braces, contrasting inlays and the bolted I/O bracket.
        for (int i = 0; i <= count; i++)
            foreach (float y in new[] { 9f, e.Y - 9 })
            {
                float x = Mathf.Clamp(i * e.X / count, 9, e.X - 9);
                Screw(new(x, y, e.Z - .5f), 4, Turn(Vector3.Right, Mathf.Pi / 2));
                if (i > 0 && i < count)
                    Box(new(x, y + Mathf.Sign(c.Y - y) * 7, e.Z - .15f), new(19, 2, .6f), "edge", Turn(Vector3.Back, -.58f));
            }
        Body(new(2, c.Y, c.Z), new(4, e.Y, e.Z), 1, .65f, "graphite");
        for (int i = 0; i < 4; i++) Box(new(-.2f, 16 + i * 22, c.Z), new(1, 14, 5), "inset");
        foreach (float y in new[] { 3f, e.Y - 3 })
            Body(new(c.X, y, e.Z * .66f), new(e.X, 6, e.Z * .66f), 4, 1, "shell");
        Body(new(c.X, e.Y - 1.25f, e.Z * .68f), new(e.X * .53f, 2.4f, 3), 1, .4f, "led");
        for (int i = 0; i < 2; i++) Box(new(e.X - 22 - i * 15, e.Y - 2, 14), new(12, 4, 10), "black");
        Print("NVIDYA   /   VECTOR", new(c.X, e.Y + .03f, e.Z * .37f), .21f, Turn(Vector3.Right, -Mathf.Pi / 2));
    }

    private void Radiator(Vector3 e, int count, float fanDepth)
    {
        float core = e.X - fanDepth;
        Body(new(core / 2, e.Y / 2, e.Z / 2), new(core, e.Y, e.Z), 3, 1.2f, "black");
        for (int i = 0; i < 72; i++)
            Box(new(.6f, e.Y / 2, 3 + i * (e.Z - 6) / 71), new(1.9f, e.Y - 14, 1.7f), "graphite");
        for (int i = 0; i < count; i++)
            Fan(new(core + fanDepth / 2, e.Y / 2, (i + .5f) * e.Z / count), e.Y - .5f, fanDepth, Basis.Identity, true);
        foreach (float y in new[] { 5f, e.Y - 5 })
            Body(new(core / 2, y, e.Z / 2), new(core, 9, e.Z), 3, .8f, "shell");
    }

    private void Pump(Vector3 e)
    {
        var c = e / 2;
        Body(new(c.X, 3, c.Z), new(e.X, 6, e.Z), 8, 1, "pipe");
        Body(new(c.X, c.Y, c.Z), new(e.X - 4, e.Y - 5, e.Z - 4), 9, 2, "shell");
        Cylinder(new(c.X, e.Y - 3, c.Z), 47, 4, "led");
        Cylinder(new(c.X, e.Y - 1.5f, c.Z), 42, 3, "black");
        Print("A U R A", new(c.X, e.Y + .12f, c.Z), .22f, Turn(Vector3.Right, -Mathf.Pi / 2));
    }
}
