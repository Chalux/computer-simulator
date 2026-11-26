using Godot;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 主菜单：极简排版。左侧轻字重标题 + 纯文字菜单，右侧一台静默线框机箱，
/// 底部一条分割线收口。除 Signal 外不使用任何彩色装饰。
/// </summary>
public partial class MainMenuScreen : Control
{
    /// <summary>“开始装配”不再直接进工作台，先交给组合根打开模式选择。</summary>
    public event Action? ModesRequested;

    private readonly List<TechMenuItem> _items = [];
    private readonly VBoxContainer _menu = new();

    private MenuOverlay _overlay = null!;
    private TechTitle _title = null!;
    private TechLine _rule = null!;
    private Control _left = null!;
    private Control _footer = null!;
    private TechShowcase _showcase = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = MenuPalette.CreateTheme();
        MouseFilter = MouseFilterEnum.Stop;

        MenuSettings.Load();
        MenuSettings.Apply();

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

        // 版心分隔：1px 竖线把排版区与线框区分开，空出两侧各 24px。
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

        _overlay = new MenuOverlay();
        AddChild(_overlay);
        _overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _overlay.Closed += () =>
        {
            MenuSettings.Save();
            if (_items.Count > 0) _items[0].GrabFocus();
        };
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

        column.AddChild(BuildHero());
        column.AddChild(VSpace(44));

        column.AddChild(new TechLine
        {
            CustomMinimumSize = new Vector2(0, 1),
            Strength = 0.75f,
        });

        _menu.AddThemeConstantOverride("separation", 0);
        column.AddChild(_menu);

        AddItem("01", "开始装配", "START BUILD", RequestModes);
        AddItem("02", "系统设置", "SYSTEM CONFIG", OpenSettings);
        AddItem("03", "制作名单", "CREDITS", OpenCredits);
        AddItem("04", "退出游戏", "EXIT", () => GetTree().Quit());

        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        return column;
    }

    private Control BuildHero()
    {
        var hero = new VBoxContainer();
        hero.AddThemeConstantOverride("separation", 0);

        var kicker = new HBoxContainer { CustomMinimumSize = new Vector2(0, 14) };
        kicker.AddThemeConstantOverride("separation", 12);
        kicker.AddChild(new ColorRect
        {
            Color = MenuPalette.Signal,
            CustomMinimumSize = new Vector2(2, 11),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        kicker.AddChild(Tracked("HARDWARE ASSEMBLY SIMULATOR", 9, MenuPalette.Alpha(MenuPalette.Ash, 0.78f), 3.0f,
            vertical: SizeFlags.ShrinkCenter));
        hero.AddChild(kicker);

        hero.AddChild(VSpace(16));

        _title = new TechTitle
        {
            Text = "PC ARCHITECT",
            FontSize = 76,
            Tracking = 0.5f,
            SplitAt = 2,
            Prefix = MenuPalette.Ash,
            Tint = MenuPalette.Platinum,
        };
        hero.AddChild(_title);

        hero.AddChild(VSpace(10));

        _rule = new TechLine
        {
            Tint = MenuPalette.Steel,
            Solid = false,
            Strength = 0.95f,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = SizeFlags.Fill,
            PivotOffset = Vector2.Zero,
        };
        hero.AddChild(_rule);

        hero.AddChild(VSpace(14));

        hero.AddChild(Text("装 机 模 拟 器", 16, MenuPalette.Alpha(MenuPalette.Ash, 0.78f)));

        return hero;
    }

    private Control BuildFooter()
    {
        var footer = new VBoxContainer();
        footer.AddThemeConstantOverride("separation", 12);

        footer.AddChild(new TechLine
        {
            CustomMinimumSize = new Vector2(0, 1),
            Strength = 0.55f,
        });

        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 16) };
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new StatusDot
        {
            Radius = 2f,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        row.AddChild(Tracked("BUILD 0.1.0", 9, MenuPalette.Alpha(MenuPalette.Ash, 0.58f), 1.4f,
            vertical: SizeFlags.ShrinkCenter));
        row.AddChild(Expand());
        row.AddChild(Tracked("© 2026 PC ARCHITECT", 9, MenuPalette.Alpha(MenuPalette.Ash, 0.48f), 1.4f,
            vertical: SizeFlags.ShrinkCenter));
        footer.AddChild(row);

        return footer;
    }

    private void AddItem(string index, string title, string subtitle, Action action)
    {
        var item = new TechMenuItem
        {
            Index = index,
            Title = title,
            Subtitle = subtitle,
        };
        _menu.AddChild(item);
        item.Pressed += action;
        _items.Add(item);
    }

    #endregion

    #region 行为

    private void RequestModes()
    {
        ModesRequested?.Invoke();
    }

    private void OpenSettings()
    {
        _overlay.Open("SYSTEM", "系统设置", body =>
        {
            body.AddChild(SliderRow("主音量", "MASTER VOLUME", MenuSettings.MasterVolume * 100f,
                value => MenuSettings.SetMasterVolume(value / 100f)));
            body.AddChild(ToggleRow("全屏显示", "FULLSCREEN", MenuSettings.Fullscreen, MenuSettings.SetFullscreen));
            body.AddChild(ToggleRow("垂直同步", "V-SYNC", MenuSettings.Vsync, MenuSettings.SetVsync));
            body.AddChild(ToggleRow("帧率显示", "FRAME COUNTER", MenuSettings.ShowFps, MenuSettings.SetShowFps));

            body.AddChild(VSpace(12));
            body.AddChild(Text("配置写入 user://menu.cfg，下次启动自动恢复。", 10, MenuPalette.Alpha(MenuPalette.Ash, 0.40f)));
        });
    }

    private void OpenCredits()
    {
        _overlay.Open("CREDITS", "制作名单", body =>
        {
            var intro = Text("聚焦空间干涉、协议逻辑与性能平衡的硬件装配模拟。", 13,
                MenuPalette.Alpha(MenuPalette.Platinum, 0.88f));
            intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.AddChild(intro);

            body.AddChild(VSpace(18));
            body.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.55f });
            body.AddChild(VSpace(14));

            body.AddChild(KeyValue("ENGINE", "Godot 4.7 · C#"));
            body.AddChild(KeyValue("RULES", "TypeScript 规则引擎"));
            body.AddChild(KeyValue("DATA", "JSON 硬件参数库"));
            body.AddChild(KeyValue("BUILD", "0.1.0 · INTERNAL PREVIEW"));

            body.AddChild(VSpace(14));
            body.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.55f });
            body.AddChild(VSpace(12));
            body.AddChild(Tracked("© 2026 PC ARCHITECT", 9, MenuPalette.Alpha(MenuPalette.Ash, 0.42f), 1.4f));
        });
    }

    private static Control SliderRow(string title, string tag, float value, Action<float> changed)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 52) };
        row.AddThemeConstantOverride("separation", 16);
        row.AddChild(RowLabel(title, tag));
        row.AddChild(Expand());

        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 1,
            Value = value,
            CustomMinimumSize = new Vector2(170, 16),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            FocusMode = FocusModeEnum.All,
        };

        StyleBoxFlat track = MenuPalette.Box(MenuPalette.Alpha(MenuPalette.Steel, 0.9f), Colors.Transparent, 0);
        track.ContentMarginTop = 1;
        track.ContentMarginBottom = 1;
        StyleBoxFlat fill = MenuPalette.Box(MenuPalette.Alpha(MenuPalette.Signal, 0.75f), Colors.Transparent, 0);
        fill.ContentMarginTop = 1;
        fill.ContentMarginBottom = 1;
        StyleBoxFlat fillHot = MenuPalette.Box(MenuPalette.Signal, Colors.Transparent, 0);
        fillHot.ContentMarginTop = 1;
        fillHot.ContentMarginBottom = 1;

        slider.AddThemeStyleboxOverride("slider", track);
        slider.AddThemeStyleboxOverride("grabber_area", fill);
        slider.AddThemeStyleboxOverride("grabber_area_highlight", fillHot);
        slider.AddThemeIconOverride("grabber", MenuPalette.SliderHandle);

        var readout = Tracked($"{value:0}%", 11, MenuPalette.Alpha(MenuPalette.Platinum, 0.85f), 1.2f,
            align: HorizontalAlignment.Right);
        readout.MinWidth = 46f;
        readout.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        slider.ValueChanged += v =>
        {
            readout.SetText($"{v:0}%");
            changed((float)v);
        };

        row.AddChild(slider);
        row.AddChild(readout);
        return row;
    }

    private static Control ToggleRow(string title, string tag, bool value, Action<bool> changed)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48) };
        row.AddThemeConstantOverride("separation", 16);
        row.AddChild(RowLabel(title, tag));
        row.AddChild(Expand());

        var toggle = new TechToggle { On = value, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        toggle.SwitchToggled += on => changed(on);
        row.AddChild(toggle);
        return row;
    }

    private static Control RowLabel(string title, string tag)
    {
        var box = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        box.AddThemeConstantOverride("separation", 2);
        box.AddChild(Text(title, 14, MenuPalette.Alpha(MenuPalette.Platinum, 0.92f)));
        box.AddChild(Tracked(tag, 9, MenuPalette.Alpha(MenuPalette.Ash, 0.45f), 1.6f));
        return box;
    }

    private static Control KeyValue(string key, string value)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
        row.AddThemeConstantOverride("separation", 18);
        var label = Tracked(key, 9, MenuPalette.Alpha(MenuPalette.Ash, 0.50f), 1.4f);
        label.MinWidth = 92f;
        row.AddChild(label);
        row.AddChild(Text(value, 12, MenuPalette.Alpha(MenuPalette.Platinum, 0.85f)));
        return row;
    }

    #endregion

    #region 动效

    private void PlayIntro()
    {
        _left.Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween left = CreateTween();
        left.TweenProperty(_left, "modulate:a", 1f, 0.55).SetEase(Tween.EaseType.Out);

        _rule.Scale = new Vector2(0f, 1f);
        Tween rule = CreateTween();
        rule.TweenInterval(0.32);
        rule.TweenProperty(_rule, "scale:x", 1f, 0.70)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);

        for (int i = 0; i < _items.Count; i++)
        {
            TechMenuItem item = _items[i];
            item.Modulate = MenuPalette.Alpha(Colors.White, 0f);
            Tween tween = CreateTween();
            tween.TweenInterval(0.28 + i * 0.06);
            tween.TweenProperty(item, "modulate:a", 1f, 0.42).SetEase(Tween.EaseType.Out);
        }

        _showcase.Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween showcase = CreateTween();
        showcase.TweenInterval(0.10);
        showcase.TweenProperty(_showcase, "modulate:a", 1f, 1.10).SetEase(Tween.EaseType.Out);

        _footer.Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween footer = CreateTween();
        footer.TweenInterval(0.55);
        footer.TweenProperty(_footer, "modulate:a", 1f, 0.40).SetEase(Tween.EaseType.Out);

        Tween focus = CreateTween();
        focus.TweenInterval(0.90);
        focus.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(this) && _items.Count > 0) _items[0].GrabFocus();
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

    private static TrackedText Tracked(string text, int size, Color color, float tracking,
        Control.SizeFlags vertical = Control.SizeFlags.Fill, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        return new TrackedText
        {
            Content = text,
            FontSize = size,
            Tint = color,
            Tracking = tracking,
            Align = align,
            SizeFlagsVertical = vertical,
        };
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
