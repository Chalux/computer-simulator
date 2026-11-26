using Godot;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 主菜单弹层：与主界面同一套令牌的极简面板。
/// 只有一条分隔线和大量留白，不设角标、不使用彩色边框。
/// </summary>
public partial class MenuOverlay : Control
{
    public event Action? Closed;

    private ColorRect _dim = null!;
    private PanelContainer _panel = null!;
    private Label _title = null!;
    private TrackedText _kicker = null!;
    private VBoxContainer _content = null!;
    private Button _close = null!;
    private Tween? _tween;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dim = new ColorRect
        {
            Color = MenuPalette.Alpha(MenuPalette.Ink, 0.92f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(_dim);
        _dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _dim.GuiInput += DimInput;

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        StyleBoxFlat panelStyle = MenuPalette.Box(
            MenuPalette.Alpha(MenuPalette.Graphite, 0.985f),
            MenuPalette.Alpha(MenuPalette.Steel, 0.95f), 1);
        panelStyle.ContentMarginLeft = 32;
        panelStyle.ContentMarginRight = 32;
        panelStyle.ContentMarginTop = 26;
        panelStyle.ContentMarginBottom = 26;

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(524, 0) };
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(_panel);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 0);
        _panel.AddChild(body);

        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 46) };
        header.AddThemeConstantOverride("separation", 12);
        body.AddChild(header);

        var titles = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        titles.AddThemeConstantOverride("separation", 3);
        header.AddChild(titles);

        _kicker = new TrackedText
        {
            Content = "",
            FontSize = 9,
            Tracking = 2.2f,
            Tint = MenuPalette.Alpha(MenuPalette.Signal, 0.75f),
        };
        titles.AddChild(_kicker);

        _title = new Label { Text = "" };
        _title.AddThemeFontOverride("font", MenuPalette.Text);
        _title.AddThemeFontSizeOverride("font_size", 18);
        _title.AddThemeColorOverride("font_color", MenuPalette.Platinum);
        titles.AddChild(_title);

        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _close = new Button
        {
            Text = "×",
            CustomMinimumSize = new Vector2(28, 28),
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        _close.AddThemeFontOverride("font", MenuPalette.Text);
        _close.AddThemeFontSizeOverride("font_size", 18);
        _close.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.75f));
        _close.AddThemeColorOverride("font_hover_color", MenuPalette.Platinum);
        _close.AddThemeColorOverride("font_pressed_color", MenuPalette.Signal);
        _close.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _close.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        _close.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        _close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        _close.Pressed += Close;
        header.AddChild(_close);

        body.AddChild(VSpace(16));
        body.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.75f });
        body.AddChild(VSpace(18));

        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 0);
        body.AddChild(_content);

        body.AddChild(VSpace(18));
        body.AddChild(Meta("ESC  关闭", 9, MenuPalette.Alpha(MenuPalette.Ash, 0.35f), 1.4f));
    }

    private static TrackedText Meta(string text, int size, Color color, float tracking) => new()
    {
        Content = text,
        FontSize = size,
        Tint = color,
        Tracking = tracking,
    };

    private static Control VSpace(float height) => new Control
    {
        CustomMinimumSize = new Vector2(0, height),
        MouseFilter = MouseFilterEnum.Ignore,
    };

    /// <summary>打开弹层并重建内容。</summary>
    public void Open(string kicker, string title, Action<VBoxContainer> build)
    {
        _tween?.Kill();

        foreach (Node child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }

        _kicker.SetText(kicker);
        _title.Text = title;
        build(_content);

        Visible = true;
        Modulate = MenuPalette.Alpha(Colors.White, 0f);
        _tween = CreateTween();
        _tween.TweenProperty(this, "modulate:a", 1f, 0.16).SetEase(Tween.EaseType.Out);
        _close.GrabFocus();
    }

    public void Close()
    {
        if (!Visible) return;

        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(this, "modulate:a", 0f, 0.12);
        _tween.TweenCallback(Callable.From(() =>
        {
            Visible = false;
            Modulate = Colors.White;
            Closed?.Invoke();
        }));
    }

    private void DimInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Close();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
