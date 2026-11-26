using Godot;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 尚未开放模式的通用简报页。五个占位模式共用这一页，
/// 只读取 <see cref="GameModeCatalog"/> 中的名称与玩法简介。
/// </summary>
public partial class ModeTeaserScreen : Control
{
    public event Action? BackRequested;

    /// <summary>要展示的模式，必须在 AddChild 之前赋值。</summary>
    public GameMode Mode { get; set; } = GameMode.Commission;

    private PanelContainer _panel = null!;
    private TechMenuItem _back = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = MenuPalette.CreateTheme();
        MouseFilter = MouseFilterEnum.Stop;

        Build(GameModeCatalog.Get(Mode));
        PlayIntro();
    }

    private void Build(GameModeInfo info)
    {
        var backdrop = new TechBackdrop();
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        StyleBoxFlat panelStyle = MenuPalette.Box(
            MenuPalette.Alpha(MenuPalette.Graphite, 0.985f),
            MenuPalette.Alpha(MenuPalette.Steel, 0.95f), 1);
        panelStyle.ContentMarginLeft = 34;
        panelStyle.ContentMarginRight = 34;
        panelStyle.ContentMarginTop = 28;
        panelStyle.ContentMarginBottom = 28;

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(660, 0) };
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(_panel);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 0);
        _panel.AddChild(body);

        body.AddChild(Text(info.Title, 34, MenuPalette.Platinum));

        body.AddChild(VSpace(16));

        var summary = Text(info.Summary, 15, MenuPalette.Alpha(MenuPalette.Platinum, 0.86f));
        summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(summary);

        body.AddChild(VSpace(24));
        body.AddChild(Text("尚待开放", 16, MenuPalette.Alpha(MenuPalette.Ash, 0.72f)));
        body.AddChild(VSpace(24));

        _back = new TechMenuItem
        {
            Index = "ESC",
            Title = "返回模式选择",
            Subtitle = "MODE SELECT",
        };
        _back.Pressed += RequestBack;
        body.AddChild(_back);
    }

    #region 行为

    private void RequestBack()
    {
        BackRequested?.Invoke();
    }

    #endregion

    #region 动效

    private void PlayIntro()
    {
        _panel.Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween tween = CreateTween();
        tween.TweenProperty(_panel, "modulate:a", 1f, 0.16).SetEase(Tween.EaseType.Out);

        // 等黑场完全退去后再聚焦返回项，转场期间不接收键盘确认。
        tween.TweenInterval(0.26);
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(this)) _back.GrabFocus();
        }));
    }

    #endregion

    #region 小工具

    private static Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", MenuPalette.Text);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Control VSpace(float height) => new Control
    {
        CustomMinimumSize = new Vector2(0, height),
        MouseFilter = MouseFilterEnum.Ignore,
    };

    #endregion
}
