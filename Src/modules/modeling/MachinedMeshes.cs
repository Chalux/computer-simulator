using Godot;

namespace ComputerSimulator.Modules.Modeling;

// Clockwise front faces for Godot. Explicit normals retain sharp stamped edges.
internal static class MachinedMeshes
{
    private sealed class Surface
    {
        private readonly SurfaceTool _tool = new();
        public Surface() => _tool.Begin(Mesh.PrimitiveType.Triangles);
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outside)
        {
            var normal = (b - a).Cross(c - a).Normalized();
            if (normal.IsZeroApprox()) return;
            if (normal.Dot(outside) < 0) { (b, c) = (c, b); normal = -normal; }
            Add(a, normal); Add(c, normal); Add(b, normal);
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outside)
        {
            Triangle(a, b, c, outside);
            Triangle(a, c, d, outside);
        }
        public void SmoothQuad((Vector3 P, Vector3 N) a, (Vector3 P, Vector3 N) b, (Vector3 P, Vector3 N) c, (Vector3 P, Vector3 N) d)
        {
            void Tri((Vector3 P, Vector3 N) p, (Vector3 P, Vector3 N) q, (Vector3 P, Vector3 N) r)
            {
                if ((q.P - p.P).Cross(r.P - p.P).Dot(p.N) > 0) (q, r) = (r, q);
                Add(p.P, p.N); Add(q.P, q.N); Add(r.P, r.N);
            }
            Tri(a, b, c); Tri(a, c, d);
        }
        private void Add(Vector3 p, Vector3 n) { _tool.SetNormal(n); _tool.AddVertex(p); }
        public ArrayMesh Finish() { _tool.Index(); return _tool.Commit(); }
    }

    // Eight-sided footprint with separate bevel faces above and below the sidewalls.
    public static ArrayMesh Enclosure(Vector3 size, float corner, float bevel)
    {
        float x = size.X / 2, y = size.Y / 2, z = size.Z / 2;
        bevel = Mathf.Min(bevel, Mathf.Min(y * .45f, Mathf.Min(x, z) * .24f));
        corner = Mathf.Clamp(corner, bevel + .01f, Mathf.Min(x, z) * .88f);
        Vector3[] Ring(float h, float inset)
        {
            float w = x - inset, d = z - inset, k = corner - inset * .55f;
            return [new(-w+k,h,-d),new(w-k,h,-d),new(w,h,-d+k),new(w,h,d-k),
                new(w-k,h,d),new(-w+k,h,d),new(-w,h,d-k),new(-w,h,-d+k)];
        }
        var rings = new[] { Ring(-y, bevel), Ring(-y + bevel, 0), Ring(y - bevel, 0), Ring(y, bevel) };
        var mesh = new Surface();
        for (int i = 0; i < 8; i++)
        {
            int j = (i + 1) % 8;
            mesh.Triangle(Vector3.Down * y, rings[0][i], rings[0][j], Vector3.Down);
            mesh.Triangle(Vector3.Up * y, rings[3][i], rings[3][j], Vector3.Up);
            for (int h = 0; h < 3; h++)
            {
                var middle = (rings[h][i] + rings[h][j]) / 2;
                mesh.Quad(rings[h][i], rings[h][j], rings[h + 1][j], rings[h + 1][i], new(middle.X, 0, middle.Z));
            }
        }
        return mesh.Finish();
    }

    // Unit fan looks along +X. Ring profiles are (axial depth, radius, squared rim).
    public static ArrayMesh FanShell(bool diffuser)
    {
        (float X, float R, bool Square)[] profile = diffuser
            ? [(.45f, .438f, false), (.485f, .442f, false), (.485f, .459f, false), (.45f, .462f, false)]
            : [(-.49f,.486f,true),(-.42f,.5f,true),(.40f,.5f,true),(.49f,.488f,true),
                (.49f,.46f,false),(.43f,.438f,false),(-.42f,.438f,false),(-.49f,.451f,false)];
        var surface = new Surface();
        const int steps = 64;
        Vector3 Point(int k, int i)
        {
            var (x, r, square) = profile[k];
            float a = i * Mathf.Tau / steps, y = Mathf.Cos(a), z = Mathf.Sin(a);
            if (square) r /= Mathf.Pow(Mathf.Pow(Mathf.Abs(y), 10) + Mathf.Pow(Mathf.Abs(z), 10), .1f);
            return new(x, y * r, z * r);
        }
        for (int i = 0; i < steps; i++)
            for (int k = 0; k < profile.Length; k++)
            {
                int n = (k + 1) % profile.Length;
                var a = Point(k, i); var b = Point(k, i + 1); var c = Point(n, i + 1); var d = Point(n, i);
                var radial = new Vector3(0, a.Y, a.Z).Normalized();
                var outward = radial * (d.X - a.X) - Vector3.Right * (d - a).Dot(radial);
                if (diffuser) outward = -outward;
                surface.Quad(a, b, c, d, outward);
            }
        return surface.Finish();
    }

    // A single graphics-card fascia with circular fan openings and clipped outside corners.
    public static ArrayMesh CardFascia(float width, float height, int fans, float radius)
    {
        var surface = new Surface();
        float cell = width / fans;
        for (int fan = 0; fan < fans; fan++)
        {
            float cx = (fan + .5f) * cell;
            Vector3 Rim(int i, float z, bool outer)
            {
                float a = Mathf.Tau * i / 64, x = Mathf.Cos(a), y = Mathf.Sin(a);
                float r = radius;
                if (outer)
                {
                    r = Mathf.Min(cell * .5f / Mathf.Max(.0001f, Mathf.Abs(x)), height * .5f / Mathf.Max(.0001f, Mathf.Abs(y)));
                    if ((fan == 0 && x < 0) || (fan == fans - 1 && x > 0))
                        r = Mathf.Min(r, (cell * .5f + height * .5f - 9) / (Mathf.Abs(x) + Mathf.Abs(y)));
                }
                return new(cx + x * r, height * .5f + y * r, z);
            }
            for (int i = 0; i < 64; i++)
            {
                var outer = Rim(i, 0, true); var next = Rim(i + 1, 0, true);
                var inner = Rim(i, 0, false); var innerNext = Rim(i + 1, 0, false);
                var back = Vector3.Forward * 4;
                surface.Quad(outer, next, innerNext, inner, Vector3.Back);
                surface.Quad(inner, innerNext, innerNext + back, inner + back, new Vector3(cx, height / 2, 0) - inner);
                surface.Quad(outer, outer + back, next + back, next, outer - new Vector3(cx, height / 2, 0));
            }
        }
        return surface.Finish();
    }

    public static ArrayMesh Impeller()
    {
        var mesh = new Surface();
        const int rows = 12, cols = 5;
        for (int blade = 0; blade < 9; blade++)
        {
            Vector3 Point(float row, float col, float side)
            {
                float t = (float)row / rows, u = (float)col / cols;
                float radius = .116f + .310f * t - .017f * Mathf.Pow(2 * u - 1, 2) * t * t;
                float sweep = -.18f * t + .62f * t * t;
                float chord = .42f + .18f * Mathf.Sin(Mathf.Pi * t) - .10f * t;
                float angle = Mathf.Tau * blade / 9 + sweep + (u - .5f) * chord;
                float pitch = (.48f - .24f * t) * (.5f - u) + .07f * Mathf.Sin(Mathf.Pi * u) + side * .015f;
                return new(pitch, radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
            }
            (Vector3, Vector3) Sample(int row, int col, int side)
            {
                var tangent = Point(row + .015f, col, side) - Point(row - .015f, col, side);
                var chord = Point(row, col + .015f, side) - Point(row, col - .015f, side);
                var normal = tangent.Cross(chord).Normalized();
                if (normal.X * side < 0) normal = -normal;
                return (Point(row, col, side), normal);
            }
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    foreach (int side in new[] { -1, 1 })
                        mesh.SmoothQuad(Sample(r, c, side), Sample(r + 1, c, side), Sample(r + 1, c + 1, side), Sample(r, c + 1, side));
                    if (c == 0 || c == cols - 1)
                    {
                        int edge = c == 0 ? 0 : cols;
                        var p = Point(r, edge, 1);
                        mesh.Quad(p, Point(r + 1, edge, 1), Point(r + 1, edge, -1), Point(r, edge, -1),
                            new Vector3(0, -p.Z, p.Y) * (edge == 0 ? -1 : 1));
                    }
                    if (r == rows - 1)
                    {
                        var p = Point(rows, c, 1);
                        mesh.Quad(p, Point(rows, c + 1, 1), Point(rows, c + 1, -1), Point(rows, c, -1), new(0, p.Y, p.Z));
                    }
                }
        }
        return mesh.Finish();
    }
}
