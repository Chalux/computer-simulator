using ComputerSimulator.Modules.Simulation;
using Godot;

namespace ComputerSimulator.Modules.Benchmark;

public partial class ScoreRadar : Control
{
    private static readonly string[] Names = ["性能", "性价比", "散热", "静音", "颜值", "稳定性"];
    private GradedScore[] _scores = [];

    public void Display(GradedScore[] scores)
    {
        _scores = scores;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_scores.Length != Names.Length) return;
        var font = GetThemeDefaultFont();
        var center = Size / 2;
        float radius = Mathf.Min((Size.X - 150) / 2, (Size.Y - 62) / 2);
        if (radius < 10) return;
        float scale = radius / Mathf.Max(100, _scores.Max(s => s.Score));
        var directions = Enumerable.Range(0, Names.Length)
            .Select(i => Vector2.Up.Rotated(i * Mathf.Tau / Names.Length)).ToArray();
        Vector2[] Ring(float score) => directions.Select(d => center + d * score * scale).ToArray();
        void Outline(Vector2[] points, Color color, float width)
        {
            DrawPolyline([.. points, points[0]], color, width, true);
        }

        foreach (var direction in directions)
            DrawLine(center, center + direction * radius, new Color("#374b55"), 1, true);
        foreach (float mark in new[] { 25f, 50f, 75f })
            Outline(Ring(mark), new Color("#344b54"), 1);
        var shape = directions.Select((d, i) => center + d * _scores[i].Score * scale).ToArray();
        var teal = new Color("#7ce5c4");
        DrawColoredPolygon(shape, new Color(teal, .25f));
        Outline(shape, teal, 2);
        Outline(Ring(100), new Color("#d9b47d"), 1.6f);
        for (int i = 0; i < directions.Length; i++)
        {
            DrawCircle(shape[i], 3.5f, teal);
            var name = $"{Names[i]}  {_scores[i].Score:F1} · {_scores[i].Grade}";
            float textWidth = font.GetStringSize(name, fontSize: 14).X;
            var point = center + directions[i] * (radius + 8);
            float x = directions[i].X < -.1f ? point.X - textWidth : directions[i].X > .1f ? point.X : point.X - textWidth / 2;
            float y = point.Y + (i == 0 ? -2 : i == 3 ? 14 : 5);
            DrawString(font, new Vector2(Mathf.Clamp(x, 3, Size.X - textWidth - 3), y), name, fontSize: 14);
        }
    }
}
