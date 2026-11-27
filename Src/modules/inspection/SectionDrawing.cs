using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Simulation;
using Godot;

namespace ComputerSimulator.Modules.Inspection;

public partial class SectionDrawing : Control
{
    private FitResult? _result;
    private const int RamLabelSize = 12;
    private const float LedgerGap = 12f;
    public void Display(FitResult result) { _result = result; QueueRedraw(); }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("#15212a"));
        if (_result == null || Size.X < 50 || Size.Y < 40) return;
        var board = _result.Parts.First(p => p.Role == "board");
        if (_result.GpuLimit is int limit) { DrawGraphicsSection(_result, limit); return; }
        Font labelFont = GetThemeDefaultFont();
        var ramParts = _result.Parts.Where(p => p.Role == "ram").OrderBy(p => p.Origin[0] + p.Size[0] / 2).ToList();
        float[] labelWidths = ramParts
            .Select(p => labelFont.GetStringSize(p.Id, HorizontalAlignment.Left, -1, RamLabelSize).X)
            .ToArray();
        int ramRows = ramParts.Count switch
        {
            0 => 0,
            1 => 1,
            2 => 12f + labelWidths[0] + LedgerGap + labelWidths[1] <= Size.X ? 1 : 2,
            _ => 2,
        };
        float ledgerRowHeight = labelFont.GetHeight(RamLabelSize) + 2f;
        float labelAscent = labelFont.GetAscent(RamLabelSize);
        float bottomPad = ramRows >= 2 ? 12f + ledgerRowHeight + labelAscent : 21f;
        float scale = Mathf.Min((Size.X - 32) / board.Size[0],
            Mathf.Max(Size.Y - 17 - bottomPad, 24) / (_result.HeightLimit + 18));
        var start = new Vector2((Size.X - board.Size[0] * scale) / 2, Size.Y - bottomPad);
        Vector2 Project(float x, float y) => start + new Vector2(x, -y) * scale;
        Rect2 Rect(Volume p) => new(Project(p.Origin[0], p.Origin[1] + p.Size[1]),
            new Vector2(p.Size[0], p.Size[1]) * scale);
        var warn = _result.Issues.SelectMany(issue => issue.Parts).ToHashSet();
        var ramAnchors = new List<(string Id, Vector2 Anchor)>();
        foreach (var p in _result.Parts)
        {
            if (p.Role == "panel") continue;
            var color = new Color(p.Role switch
            {
                "tower" => "#a0b5ba",
                "fan" => "#dab88d",
                "ram" => "#7bd5d9",
                "slot" => "#637783",
                _ => "#669a88"
            });
            var rect = Rect(p);
            DrawRect(rect, new Color(color, .25f));
            DrawRect(rect, warn.Contains(p.Id) ? new Color("#ff8179") : color, false, 1);
            if (p.Role == "ram")
                ramAnchors.Add((p.Id, rect.Position + new Vector2(rect.Size.X / 2, rect.Size.Y)));
        }
        DrawRamLedger(labelFont, ramAnchors, ramRows, ledgerRowHeight);
        int callout = 0;
        foreach (var issue in _result.Issues)
        {
            if (issue.Overlap is not { } overlap) continue;
            var contact = Rect(overlap);
            var ink = new Color("#ff958b");
            DrawRect(contact, ink);
            float excess = issue.Excess ?? overlap.Size[1];
            var labelAt = new Vector2(start.X + board.Size[0] * scale + 14, 50 + 19 * callout++);
            if (labelAt.X + 60 >= Size.X) continue;
            DrawLine(contact.GetCenter(), labelAt - new Vector2(2, 5), ink, 1, true);
            DrawString(GetThemeDefaultFont(), labelAt, $"+{excess:0.#} mm", fontSize: 13, modulate: ink);
        }
        var line = Project(0, _result.HeightLimit);
        DrawDashedLine(new Vector2(8, line.Y), new Vector2(Size.X - 8, line.Y), new Color("#c6ac77"), 1, 6);
        DrawString(GetThemeDefaultFont(), new Vector2(12, line.Y - 5), $"侧板  {_result.HeightLimit} mm",
            fontSize: 13, modulate: new Color("#efcc8d"));
    }

    private void DrawRamLedger(Font font, List<(string Id, Vector2 Anchor)> entries, int rows, float rowHeight)
    {
        if (entries.Count == 0 || rows <= 0) return;
        entries.Sort((a, b) => a.Anchor.X.CompareTo(b.Anchor.X));
        float ascent = font.GetAscent(RamLabelSize);
        var cursor = new float[rows];
        for (int i = 0; i < rows; i++) cursor[i] = 6f;
        var ink = new Color("#7bd5d9");
        for (int i = 0; i < entries.Count; i++)
        {
            var (id, anchor) = entries[i];
            int row = i % rows;
            Vector2 textSize = font.GetStringSize(id, HorizontalAlignment.Left, -1, RamLabelSize);
            float baseline = Size.Y - 6f - (rows - 1 - row) * rowHeight;
            float maxLeft = Mathf.Max(cursor[row], Size.X - 6f - textSize.X);
            float left = Mathf.Clamp(anchor.X - textSize.X / 2f, cursor[row], maxLeft);
            cursor[row] = left + textSize.X + LedgerGap;
            var top = new Vector2(left + textSize.X / 2f, baseline - ascent);
            DrawLine(anchor + new Vector2(0, 2), top + new Vector2(0, -2), new Color(ink, .5f), 1, true);
            DrawString(font, new Vector2(left, baseline), id, fontSize: RamLabelSize, modulate: ink);
        }
    }

    private void DrawGraphicsSection(FitResult result, int limit)
    {
        var gpu = result.Parts.First(p => p.Role == "gpu");
        var radiator = result.Parts.First(p => p.Role == "radiator");
        float length = radiator.Origin[0] + radiator.Size[0];
        float height = Mathf.Max(gpu.Origin[1] + gpu.Size[1], radiator.Origin[1] + radiator.Size[1]);
        float scale = Mathf.Min((Size.X - 44) / length, (Size.Y - 45) / height);
        var origin = new Vector2((Size.X - length * scale) / 2, Size.Y - 24);
        Rect2 Project(Volume v) => new(origin + new Vector2(v.Origin[0], -v.Origin[1] - v.Size[1]) * scale,
            new Vector2(v.Size[0], v.Size[1]) * scale);
        var green = new Color("#82cbb7");
        var amber = new Color("#f1c98c");
        var red = new Color("#ff8b82");
        var card = Project(gpu);
        var cooler = Project(radiator);
        DrawRect(card, new Color(green, .22f)); DrawRect(card, green, false, 1);
        DrawRect(cooler, new Color(amber, .22f)); DrawRect(cooler, amber, false, 1);
        DrawString(GetThemeDefaultFont(), card.Position + new Vector2(6, 18), $"显卡  {gpu.Size[0]} mm", fontSize: 14, modulate: green);
        foreach (var issue in result.Issues)
            if (issue.Overlap is { } overlap)
            {
                var hit = Project(overlap);
                DrawRect(hit, new Color(red, .68f));
                DrawString(GetThemeDefaultFont(), new Vector2(cooler.Position.X - 10, 16), $"+{issue.Excess} mm", fontSize: 13, modulate: red);
            }
        float x = origin.X + limit * scale;
        DrawDashedLine(new Vector2(x, 18), new Vector2(x, Size.Y - 17), amber, 1, 4);
        DrawString(GetThemeDefaultFont(), new Vector2(origin.X, Size.Y - 4), $"有效限长  {limit} mm  /  冷排总厚  {radiator.Size[0]} mm", fontSize: 13, modulate: amber);
    }
}
