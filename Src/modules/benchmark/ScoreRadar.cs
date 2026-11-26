using ComputerSimulator.Modules.Simulation;
using Godot;

namespace ComputerSimulator.Modules.Benchmark;

public partial class ScoreRadar : Control
{
    public static readonly string[] AxisNames = ["性能", "性价比", "散热", "静音", "颜值", "稳定性"];
    private GradedScore[] _scores = [];
    private GradedScore[]? _baseline;

    public void Display(GradedScore[] scores, GradedScore[]? baseline = null)
    {
        _scores = scores;
        _baseline = baseline;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_scores.Length != AxisNames.Length) return;
        var font = GetThemeDefaultFont();
        var center = Size / 2;
        float radius = Mathf.Min((Size.X - 150) / 2, (Size.Y - 62) / 2);
        if (radius < 10) return;
        float maxScore = Mathf.Max(100, _scores.Max(s => s.Score));
        if (_baseline is { Length: var baselineLength } baseline && baselineLength == AxisNames.Length)
            maxScore = Mathf.Max(maxScore, baseline.Max(s => s.Score));
        float scale = radius / maxScore;
        var directions = Enumerable.Range(0, AxisNames.Length)
            .Select(i => Vector2.Up.Rotated(i * Mathf.Tau / AxisNames.Length)).ToArray();
        Vector2[] Ring(float score) => directions.Select(d => center + d * score * scale).ToArray();
        void Outline(Vector2[] points, Color color, float width)
        {
            DrawPolyline([.. points, points[0]], color, width, true);
        }
        void DashedOutline(Vector2[] points, Color color, float width)
        {
            const float dash = 6f, gap = 5f;
            bool ink = true;
            float remain = dash;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 from = points[i], to = points[(i + 1) % points.Length];
                float length = from.DistanceTo(to);
                float at = 0;
                while (at < length)
                {
                    float step = Mathf.Min(remain, length - at);
                    if (ink)
                        DrawLine(from.Lerp(to, at / length), from.Lerp(to, (at + step) / length), color, width, true);
                    at += step;
                    remain -= step;
                    if (remain <= .001f)
                    {
                        ink = !ink;
                        remain = ink ? dash : gap;
                    }
                }
            }
        }

        foreach (var direction in directions)
            DrawLine(center, center + direction * radius, new Color("#374b55"), 1, true);
        foreach (float mark in new[] { 25f, 50f, 75f })
            Outline(Ring(mark), new Color("#344b54"), 1);
        if (_baseline is { } ghost && ghost.Length == AxisNames.Length)
        {
            var ghostShape = directions.Select((d, i) => center + d * ghost[i].Score * scale).ToArray();
            DrawColoredPolygon(ghostShape, new Color(new Color("#d9b47d"), .05f));
        }
        var shape = directions.Select((d, i) => center + d * _scores[i].Score * scale).ToArray();
        var teal = new Color("#7ce5c4");
        DrawColoredPolygon(shape, new Color(teal, .25f));
        Outline(shape, teal, 2);
        if (_baseline is { } ghosted && ghosted.Length == AxisNames.Length)
        {
            var ghostShape = directions.Select((d, i) => center + d * ghosted[i].Score * scale).ToArray();
            DashedOutline(ghostShape, new Color(new Color("#d9b47d"), .72f), 1.4f);
        }
        Outline(Ring(100), new Color("#d9b47d"), 1.6f);
        for (int i = 0; i < directions.Length; i++)
        {
            DrawCircle(shape[i], 3.5f, teal);
            var name = $"{AxisNames[i]}  {_scores[i].Score:F1} · {_scores[i].Grade}";
            float textWidth = font.GetStringSize(name, fontSize: 14).X;
            var point = center + directions[i] * (radius + 8);
            float x = directions[i].X < -.1f ? point.X - textWidth : directions[i].X > .1f ? point.X : point.X - textWidth / 2;
            float y = point.Y + (i == 0 ? -2 : i == 3 ? 14 : 5);
            DrawString(font, new Vector2(Mathf.Clamp(x, 3, Size.X - textWidth - 3), y), name, fontSize: 14);
        }
    }
}
