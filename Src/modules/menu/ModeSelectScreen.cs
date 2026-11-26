using Godot;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 模式选择：主菜单“开始装配”后的第一跳。六个入口来自同一份目录数据，
/// 沙盒与神秘箱进入工作台，故障诊断进入专用案例，其余三个交给模式简报页。
/// </summary>
public partial class ModeSelectScreen : Control
{
    public event Action<GameMode>? ModeSelected;
    public event Action? BackRequested;

    /// <summary>返回本页时优先聚焦的模式；为空时聚焦第一个入口。</summary>
    public GameMode? InitialFocus { get; set; }

    private readonly List<(GameMode Mode, TechMenuItem Item)> _items = [];
    private readonly GridContainer _grid = new();
    private Control _left = null!;
    private Control _footer = null!;
    private TechShowcase _showcase = null!;
    private TechLine _rule = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = MenuPalette.CreateTheme();
        MouseFilter = MouseFilterEnum.Stop;

        Build();
        PlayIntro();
    }

    #region 布局

    private void Build()
    {
        var backdrop = new TechBackdrop();
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 72);
        margin.AddThemeConstantOverride("margin_right", 72);
        margin.AddThemeConstantOverride("margin_top", 60);
        margin.AddThemeConstantOverride("margin_bottom", 34);
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var page = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        page.AddThemeConstantOverride("separation", 0);
        margin.AddChild(page);

        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 24);
        page.AddChild(content);

        _left = BuildLeftColumn();
        content.AddChild(_left);

        content.AddChild(new ColorRect
        {
            Color = MenuPalette.Alpha(MenuPalette.Steel, 0.45f),
            CustomMinimumSize = new Vector2(1, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });

        _showcase = new TechShowcase
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        content.AddChild(_showcase);

        _footer = BuildFooter();
        page.AddChild(_footer);
    }

    private Control BuildLeftColumn()
    {
        var column = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(540, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.Fill,
        };
        column.AddThemeConstantOverride("separation", 0);

        var title = new TechTitle
        {
            Text = "MODE SELECT",
            FontSize = 54,
            Tracking = 0.5f,
            SplitAt = 4,
            Prefix = MenuPalette.Ash,
            Tint = MenuPalette.Platinum,
        };
        column.AddChild(title);

        column.AddChild(VSpace(8));

        _rule = new TechLine
        {
            Tint = MenuPalette.Steel,
            Solid = false,
            Strength = 0.95f,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = SizeFlags.Fill,
            PivotOffset = Vector2.Zero,
        };
        column.AddChild(_rule);

        column.AddChild(VSpace(12));
        column.AddChild(Text("选 择 模 式", 16, MenuPalette.Alpha(MenuPalette.Ash, 0.78f)));
        column.AddChild(VSpace(24));
        column.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.75f });

        _grid.Columns = 2;
        _grid.AddThemeConstantOverride("h_separation", 24);
        _grid.AddThemeConstantOverride("v_separation", 0);
        column.AddChild(_grid);

        foreach (GameModeInfo info in GameModeCatalog.All)
        {
            GameMode mode = info.Mode;
            var item = new TechMenuItem
            {
                Index = info.Index,
                Title = info.Title,
                Subtitle = info.Available ? "OPEN" : "SOON",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _grid.AddChild(item);
            item.Pressed += () => Choose(mode);
            _items.Add((mode, item));
        }

        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        return column;
    }

    private Control BuildFooter()
    {
        var footer = new VBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        footer.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.55f });

        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 24) };
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Expand());
        row.AddChild(TextButton("返回主菜单   ESC", RequestBack));
        footer.AddChild(row);
        return footer;
    }

    #endregion

    #region 行为

    private void Choose(GameMode mode)
    {
        ModeSelected?.Invoke(mode);
    }

    private void RequestBack()
    {
        BackRequested?.Invoke();
    }

    private void GrabInitialFocus()
    {
        if (!IsInstanceValid(this)) return;
        if (GetViewport().GuiGetFocusOwner() != null) return;

        TechMenuItem? target = null;
        if (InitialFocus is { } wanted)
        {
            foreach (var entry in _items)
            {
                if (entry.Mode != wanted) continue;
                target = entry.Item;
                break;
            }
        }

        target ??= _items.Count > 0 ? _items[0].Item : null;
        target?.GrabFocus();
    }

    #endregion

    #region 动效

    private void PlayIntro()
    {
        _left.Modulate = MenuPalette.Alpha(Colors.White, 0f);
        CreateTween().TweenProperty(_left, "modulate:a", 1f, 0.50).SetEase(Tween.EaseType.Out);

        _rule.Scale = new Vector2(0f, 1f);
        Tween rule = CreateTween();
        rule.TweenInterval(0.28);
        rule.TweenProperty(_rule, "scale:x", 1f, 0.70)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);

        for (int i = 0; i < _items.Count; i++)
        {
            TechMenuItem item = _items[i].Item;
            item.Modulate = MenuPalette.Alpha(Colors.White, 0f);
            Tween tween = CreateTween();
            tween.TweenInterval(0.24 + i * 0.045);
            tween.TweenProperty(item, "modulate:a", 1f, 0.38).SetEase(Tween.EaseType.Out);
        }

        _showcase.Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween showcase = CreateTween();
        showcase.TweenInterval(0.10);
        showcase.TweenProperty(_showcase, "modulate:a", 1f, 1.10).SetEase(Tween.EaseType.Out);

        _footer.Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween footer = CreateTween();
        footer.TweenInterval(0.50);
        footer.TweenProperty(_footer, "modulate:a", 1f, 0.38).SetEase(Tween.EaseType.Out);

        // 等黑场完全退去后再抓焦点，避免转场动画期间误触造成重复跳转。
        Tween focus = CreateTween();
        focus.TweenInterval(0.42);
        focus.TweenCallback(Callable.From(GrabInitialFocus));
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

    private static Button TextButton(string text, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            Flat = true,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(0, 24),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        button.AddThemeFontOverride("font", MenuPalette.Text);
        button.AddThemeFontSizeOverride("font_size", 11);
        button.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.65f));
        button.AddThemeColorOverride("font_hover_color", MenuPalette.Platinum);
        button.AddThemeColorOverride("font_pressed_color", MenuPalette.Signal);
        button.AddThemeColorOverride("font_focus_color", MenuPalette.Platinum);
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());

        var focus = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = MenuPalette.Alpha(MenuPalette.Signal, 0.55f),
            BorderWidthBottom = 1,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
        };
        button.AddThemeStyleboxOverride("focus", focus);

        button.Pressed += pressed;
        return button;
    }

    private static Control Expand() => new Control
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        MouseFilter = MouseFilterEnum.Ignore,
    };

    private static Control VSpace(float height) => new Control
    {
        CustomMinimumSize = new Vector2(0, height),
        MouseFilter = MouseFilterEnum.Ignore,
    };

    #endregion
}
