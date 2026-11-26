using Godot;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 主菜单条目：纯文字行，没有填充色块。激活态只做三件事——
/// 编号点亮为 Signal、标题提亮到 Platinum、底部升起一条 1px 强调线。
/// </summary>
public partial class TechMenuItem : Button
{
    public string Index { get; set; } = "01";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";

    private float _hover;
    private bool _active;

    public override void _Ready()
    {
        Flat = true;
        Text = string.Empty;
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        CustomMinimumSize = new Vector2(0, 54);

        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        SetProcess(false);
        Resized += QueueRedraw;
        MouseEntered += GrabFocus;
        FocusEntered += Enter;
        FocusExited += Exit;
    }

    private void Enter()
    {
        _active = true;
        SetProcess(true);
    }

    private void Exit()
    {
        _active = false;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _hover = Mathf.MoveToward(_hover, _active ? 1f : 0f, (float)delta * (_active ? 8f : 5f));
        QueueRedraw();

        if (!_active && Mathf.IsZeroApprox(_hover))
        {
            SetProcess(false);
        }
    }

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        if (w < 80f || h < 20f) return;

        float hov = _hover;
        float baseline = MenuPalette.Baseline(MenuPalette.Text, 0, h, 19);

        // 常态分割线 + 激活时从左侧升起的强调线。
        DrawLine(new Vector2(0, h - 0.5f), new Vector2(w, h - 0.5f),
            MenuPalette.Alpha(MenuPalette.Steel, 0.62f + 0.22f * hov), 1f, true);
        if (hov > 0.01f)
        {
            DrawLine(new Vector2(0, h - 0.5f), new Vector2(w * hov, h - 0.5f),
                MenuPalette.Alpha(MenuPalette.Signal, 0.78f * hov), 1f, true);
        }

        // 编号：Ash → Signal。
        Color indexColor = MenuPalette.Mix(
            MenuPalette.Alpha(MenuPalette.Ash, 0.52f),
            MenuPalette.Signal, hov);
        MenuPalette.DrawTracked(this, MenuPalette.Mono,
            new Vector2(0, MenuPalette.Baseline(MenuPalette.Mono, 0, h, 9)), Index, 9, 1.2f, indexColor);

        // 中文标题：未选中也保持可读，悬停提亮并右移 4px。
        MenuPalette.DrawTracked(this, MenuPalette.Text, new Vector2(44f + 4f * hov, baseline), Title,
            19, 0.6f, MenuPalette.Alpha(MenuPalette.Platinum, 0.62f + 0.38f * hov));

        // 英文副标：右对齐，悬停向左让出 2px，与标题形成轻微分离。
        float subWidth = MenuPalette.MeasureTracked(MenuPalette.Mono, Subtitle, 9, 1.6f);
        MenuPalette.DrawTracked(this, MenuPalette.Mono,
            new Vector2(w - subWidth - 2f * hov, MenuPalette.Baseline(MenuPalette.Mono, 0, h, 9)), Subtitle,
            9, 1.6f, MenuPalette.Alpha(MenuPalette.Ash, 0.40f + 0.38f * hov));
    }
}
