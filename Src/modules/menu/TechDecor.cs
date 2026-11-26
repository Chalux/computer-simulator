using Godot;

namespace ComputerSimulator.Modules.Menu;

/// <summary>1px 细分隔线。实线用于长分割，渐隐线用于标题下的收口。</summary>
public partial class TechLine : Control
{
    public Color Tint { get; set; } = MenuPalette.Steel;
    public bool Vertical { get; set; }
    public bool Solid { get; set; } = true;
    public float Strength { get; set; } = 1f;

    public override void _Ready()
    {
        // 保留调用方显式指定的尺寸（标题下的 360px 短线），只补齐未指定的一边。
        Vector2 min = CustomMinimumSize;
        if (Vertical)
        {
            CustomMinimumSize = new Vector2(min.X > 0 ? min.X : 1f, min.Y > 0 ? min.Y : 24f);
        }
        else
        {
            CustomMinimumSize = new Vector2(min.X > 0 ? min.X : 36f, min.Y > 0 ? min.Y : 1f);
        }

        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        Vector2 a = Vertical ? new(Size.X * 0.5f, 0) : new(0, Size.Y * 0.5f);
        Vector2 b = Vertical ? new(Size.X * 0.5f, Size.Y) : new(Size.X, Size.Y * 0.5f);

        if (Solid)
        {
            DrawLine(a, b, MenuPalette.Alpha(Tint, Strength), 1f, true);
            return;
        }

        const int steps = 24;
        for (int i = 0; i < steps; i++)
        {
            float t0 = i / (float)steps;
            float t1 = (i + 1) / (float)steps;
            float alpha = Strength * Mathf.Pow(1f - t0, 1.5f);
            DrawLine(a.Lerp(b, t0), a.Lerp(b, t1), MenuPalette.Alpha(Tint, alpha), 1f, true);
        }
    }
}

/// <summary>静态状态灯：只用一个 3px 圆点表达系统在线，不做呼吸动画。</summary>
public partial class StatusDot : Control
{    public Color Tint { get; set; } = MenuPalette.Signal;
    public float Radius { get; set; } = 2.5f;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(8, 8);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        Vector2 center = Size * 0.5f;
        DrawCircle(center, Radius + 3f, MenuPalette.Alpha(Tint, 0.07f));
        DrawCircle(center, Radius, Tint);
    }
}

/// <summary>
/// 主标题：自绘带字距的轻字重标题，前缀使用次要色，无描边无发光。
/// </summary>
public partial class TechTitle : Control
{
    public string Text { get; set; } = "";
    public int FontSize { get; set; } = 76;
    public float Tracking { get; set; } = 0.5f;
    /// <summary>前 <see cref="SplitAt"/> 个字符使用 <see cref="Prefix"/> 色；-1 表示整行同色。</summary>
    public int SplitAt { get; set; } = -1;
    public Color Tint { get; set; } = MenuPalette.Platinum;
    public Color Prefix { get; set; } = MenuPalette.Ash;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(
            MenuPalette.MeasureTracked(MenuPalette.Display, Text, FontSize, Tracking) + 4f,
            FontSize * 1.24f);
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        if (string.IsNullOrEmpty(Text)) return;

        float baseline = MenuPalette.Baseline(MenuPalette.Display, 0, Size.Y, FontSize);
        float x = 0f;

        for (int i = 0; i < Text.Length; i++)
        {
            string glyph = Text[i].ToString();
            Color color = SplitAt >= 0 && i < SplitAt ? Prefix : Tint;
            DrawString(MenuPalette.Display, new Vector2(x, baseline), glyph, HorizontalAlignment.Left, -1,
                FontSize, color);
            x += MenuPalette.Display.GetStringSize(glyph, HorizontalAlignment.Left, -1, FontSize).X + Tracking;
        }
    }
}

/// <summary>极简开关：方角细描边 + 滑动方块，无填充色块。</summary>
public partial class TechToggle : Button
{
    private bool _on;
    private float _slide;

    [Signal]
    public delegate void SwitchToggledEventHandler(bool on);

    public bool On
    {
        get => _on;
        set
        {
            _on = value;
            _slide = value ? 1f : 0f;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        Flat = true;
        Text = string.Empty;
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        CustomMinimumSize = new Vector2(54, 22);

        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        Resized += QueueRedraw;
        SetProcess(false);
        Pressed += () =>
        {
            _on = !_on;
            SetProcess(true);
            QueueRedraw();
            EmitSignal(SignalName.SwitchToggled, _on);
        };
    }

    public override void _Process(double delta)
    {
        float target = _on ? 1f : 0f;
        if (Mathf.IsEqualApprox(_slide, target))
        {
            SetProcess(false);
            return;
        }
        _slide = Mathf.MoveToward(_slide, target, (float)delta * 8f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        bool hot = IsHovered() || HasFocus();
        Color accent = _on ? MenuPalette.Signal : MenuPalette.Ash;

        var rect = new Rect2(0.5f, 0.5f, w - 1f, h - 1f);
        DrawRect(rect, _on ? MenuPalette.Alpha(MenuPalette.Signal, 0.09f) : MenuPalette.Alpha(MenuPalette.Steel, 0.20f));
        DrawRect(rect,
            MenuPalette.Alpha(accent, _on ? (hot ? 0.75f : 0.55f) : (hot ? 0.45f : 0.28f)), false, 1f);

        float knobW = 10f;
        float knobH = h - 8f;
        float x = 4f + (w - knobW - 8f) * _slide;
        DrawRect(new Rect2(x, 4f, knobW, knobH),
            _on ? MenuPalette.Signal : MenuPalette.Alpha(MenuPalette.Ash, hot ? 0.55f : 0.38f));

        string label = _on ? "ON" : "OFF";
        const int fontSize = 8;
        Color fontColor = MenuPalette.Alpha(accent, _on ? 0.85f : 0.45f);
        Vector2 textSize = MenuPalette.Mono.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
        float textX = _on ? 9f : w - 9f - textSize.X;
        DrawString(MenuPalette.Mono, new Vector2(textX, MenuPalette.Baseline(MenuPalette.Mono, 0, h, fontSize)),
            label, HorizontalAlignment.Left, -1, fontSize, fontColor);
    }
}

/// <summary>
/// 带字距的文本控件。Label 不支持字距，所有编号、英文元信息与读数都用它绘制。
/// </summary>
public partial class TrackedText : Control
{
    public string Content { get; set; } = "";
    public Font? Typeface { get; set; }
    public int FontSize { get; set; } = 9;
    public float Tracking { get; set; } = 1.4f;
    public Color Tint { get; set; } = MenuPalette.Ash;
    public HorizontalAlignment Align { get; set; } = HorizontalAlignment.Left;
    /// <summary>强制最小宽度，用于右对齐的读数。</summary>
    public float MinWidth { get; set; }

    private Font Face => Typeface ?? MenuPalette.Mono;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Refresh();
    }

    public void SetText(string text)
    {
        Content = text;
        Refresh();
    }

    private void Refresh()
    {
        float width = MenuPalette.MeasureTracked(Face, Content, FontSize, Tracking) + 2f;
        if (MinWidth > 0f) width = Mathf.Max(width, MinWidth);
        CustomMinimumSize = new Vector2(width, FontSize * 1.5f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float width = MenuPalette.MeasureTracked(Face, Content, FontSize, Tracking);
        float x = Align == HorizontalAlignment.Right ? Size.X - width : 0f;
        MenuPalette.DrawTracked(this, Face, new Vector2(x, MenuPalette.Baseline(Face, 0, Size.Y, FontSize)),
            Content, FontSize, Tracking, Tint);
    }
}
