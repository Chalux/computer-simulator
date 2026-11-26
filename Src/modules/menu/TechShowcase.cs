using Godot;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 右侧静默线框：一台 ATX 机箱的线稿，只保留轮廓与主要部件，没有标注、没有遥测、没有面板。
/// 用极慢的摆动与低透明度充当“被照亮的实物”，而不是信息面板。
/// </summary>
public partial class TechShowcase : Control
{
    private readonly List<(Vector3 A, Vector3 B, float Tone)> _edges = [];
    private readonly List<(Vector3 C, float Radius, int Axis, float Tone)> _rings = [];

    private float _time;
    private float _scale;
    private float _yaw;
    private float _pitch;
    private Vector2 _origin;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(320, 300);
        Resized += QueueRedraw;
        BuildModel();
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    #region 模型

    private void BuildModel()
    {
        // 机箱：ATX 中塔（宽 220 / 高 470 / 深 460 mm）
        Box(Vector3.Zero, new Vector3(220, 470, 460), 0.26f);

        // 侧透面板内框
        RectOnPlaneX(110f, 206f, 206f, 0.34f);

        // 主板
        Box(new Vector3(-92, -4, -78), new Vector3(10, 350, 300), 0.40f);

        // 双塔风冷
        Box(new Vector3(-26, 64, -54), new Vector3(150, 172, 140), 0.46f);

        // 显卡
        Box(new Vector3(-28, -74, 48), new Vector3(72, 132, 300), 0.50f);

        // 电源仓
        Box(new Vector3(0, -176, -54), new Vector3(170, 86, 300), 0.28f);

        // 前置风扇 ×3
        for (int i = 0; i < 3; i++)
        {
            Ring(new Vector3(0, 118 - i * 118, 228), 58f, 2, 0.32f);
        }
    }

    private void Box(Vector3 center, Vector3 size, float tone)
    {
        Vector3 h = size * 0.5f;
        Vector3[] c =
        [
            center + new Vector3(-h.X, -h.Y, -h.Z), center + new Vector3(h.X, -h.Y, -h.Z),
            center + new Vector3(h.X, h.Y, -h.Z), center + new Vector3(-h.X, h.Y, -h.Z),
            center + new Vector3(-h.X, -h.Y, h.Z), center + new Vector3(h.X, -h.Y, h.Z),
            center + new Vector3(h.X, h.Y, h.Z), center + new Vector3(-h.X, h.Y, h.Z),
        ];

        int[] pairs = [0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7];
        for (int i = 0; i < pairs.Length; i += 2)
        {
            _edges.Add((c[pairs[i]], c[pairs[i + 1]], tone));
        }
    }

    private void RectOnPlaneX(float x, float halfY, float halfZ, float tone)
    {
        Vector3[] c =
        [
            new(x, -halfY, -halfZ), new(x, -halfY, halfZ), new(x, halfY, halfZ), new(x, halfY, -halfZ),
        ];
        for (int i = 0; i < 4; i++)
        {
            _edges.Add((c[i], c[(i + 1) % 4], tone));
        }
    }

    private void Ring(Vector3 center, float radius, int axis, float tone)
        => _rings.Add((center, radius, axis, tone));

    #endregion

    #region 投影

    private static Vector3 Rotate(Vector3 p, float yaw, float pitch)
    {
        float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
        float x1 = p.X * cy + p.Z * sy;
        float z1 = -p.X * sy + p.Z * cy;
        float cp = Mathf.Cos(pitch), sp = Mathf.Sin(pitch);
        float y1 = p.Y * cp - z1 * sp;
        float z2 = p.Y * sp + z1 * cp;
        return new Vector3(x1, y1, z2);
    }

    private Vector2 Project(Vector3 v)
    {
        const float depth = 1500f;
        float persp = depth / (depth + v.Z);
        return _origin + new Vector2(v.X * persp, -v.Y * persp) * _scale;
    }

    private static Vector3 CirclePoint(Vector3 center, float radius, int axis, float angle)
    {
        float c = Mathf.Cos(angle) * radius;
        float s = Mathf.Sin(angle) * radius;
        return axis switch
        {
            0 => center + new Vector3(0, s, c),
            1 => center + new Vector3(c, 0, s),
            _ => center + new Vector3(c, s, 0),
        };
    }

    #endregion

    public override void _Draw()
    {
        if (Size.X < 120f || Size.Y < 120f) return;

        _yaw = 0.52f + Mathf.Sin(_time * 0.07f) * 0.42f;
        _pitch = -0.17f + Mathf.Sin(_time * 0.05f) * 0.03f;

        FitModel(_yaw, _pitch);

        foreach ((Vector3 a, Vector3 b, float tone) in _edges)
        {
            Vector3 ra = Rotate(a, _yaw, _pitch);
            Vector3 rb = Rotate(b, _yaw, _pitch);
            float near = Mathf.Clamp(1.12f - (ra.Z + rb.Z) * 0.5f / 520f, 0.32f, 1f);
            float alpha = Mathf.Clamp(tone * near * 0.80f, 0f, 1f);

            Vector2 pa = Project(ra);
            Vector2 pb = Project(rb);
            DrawLine(pa, pb, MenuPalette.Alpha(MenuPalette.Signal, alpha * 0.07f), 3.5f, true);
            DrawLine(pa, pb, MenuPalette.Alpha(MenuPalette.Signal, alpha), 1f, true);
        }

        foreach ((Vector3 c, float radius, int axis, float tone) in _rings)
        {
            Vector3[] points = new Vector3[49];
            for (int s = 0; s <= 48; s++)
            {
                points[s] = Rotate(CirclePoint(c, radius, axis, s / 48f * Mathf.Tau), _yaw, _pitch);
            }

            Vector2[] screen = new Vector2[points.Length];
            for (int s = 0; s < points.Length; s++)
            {
                screen[s] = Project(points[s]);
            }

            DrawPolyline(screen, MenuPalette.Alpha(MenuPalette.Signal, tone * 0.75f), 1f, true);
        }
    }

    /// <summary>按当前旋转角把整台机器装进可用区域，任意窗口尺寸都不会越界。</summary>
    private void FitModel(float yaw, float pitch)
    {
        Vector2 min = new(float.MaxValue, float.MaxValue);
        Vector2 max = new(float.MinValue, float.MinValue);

        void Include(Vector3 point)
        {
            Vector3 r = Rotate(point, yaw, pitch);
            float persp = 1500f / (1500f + r.Z);
            Vector2 unit = new(r.X * persp, -r.Y * persp);
            min = new Vector2(Mathf.Min(min.X, unit.X), Mathf.Min(min.Y, unit.Y));
            max = new Vector2(Mathf.Max(max.X, unit.X), Mathf.Max(max.Y, unit.Y));
        }

        for (int i = 0; i < _edges.Count; i++)
        {
            Include(_edges[i].A);
            Include(_edges[i].B);
        }

        for (int i = 0; i < _rings.Count; i++)
        {
            var ring = _rings[i];
            for (int s = 0; s < 36; s++)
            {
                Include(CirclePoint(ring.C, ring.Radius, ring.Axis, s / 36f * Mathf.Tau));
            }
        }

        const float padX = 36f, padY = 34f;
        float availW = Mathf.Max(60f, Size.X - padX * 2f);
        float availH = Mathf.Max(60f, Size.Y - padY * 2f);
        float target = Mathf.Min(availW / Mathf.Max(1f, max.X - min.X), availH / Mathf.Max(1f, max.Y - min.Y));

        _scale = _scale <= 0.001f ? target : Mathf.Lerp(_scale, target, 0.10f);
        _origin = new Vector2(Size.X * 0.5f, Size.Y * 0.5f)
            - new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f) * _scale;
    }
}
