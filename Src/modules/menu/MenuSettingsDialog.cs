using Godot;
using KemoCard.Frame.UI;
using KemoCard.Frame.UI.Base;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 系统设置对话框：路由 <see cref="UiRoutes.MenuSettings"/>，由 UIManager 以 Dlg 层打开。
/// 只把控件意图转发给 <see cref="MenuController"/>；关闭时统一落盘。
/// </summary>
public partial class MenuSettingsDialog : BaseDlg
{
    private MenuController _controller = null!;
    private ColorRect _dim = null!;
    private PanelContainer _panel = null!;
    private Label _title = null!;
    private VBoxContainer _content = null!;
    private Button _close = null!;

    public override string UIId => UiRoutes.MenuSettings;
    public override string UIDir => UiRoutes.MenuDir;

    protected override void OnCreate()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = MenuPalette.CreateTheme();
        MouseFilter = MouseFilterEnum.Stop;

        _controller = Facade<MenuController>();
        _dim = GetNode<ColorRect>("%Dim");
        _panel = GetNode<PanelContainer>("%Panel");
        _title = GetNode<Label>("%Title");
        _content = GetNode<VBoxContainer>("%Content");
        _close = GetNode<Button>("%CloseButton");

        _dim.Color = MenuPalette.Alpha(MenuPalette.Ink, 0.92f);

        StyleBoxFlat panelStyle = MenuPalette.Box(
            MenuPalette.Alpha(MenuPalette.Graphite, 0.985f),
            MenuPalette.Alpha(MenuPalette.Steel, 0.95f), 1);
        panelStyle.ContentMarginLeft = 32;
        panelStyle.ContentMarginRight = 32;
        panelStyle.ContentMarginTop = 26;
        panelStyle.ContentMarginBottom = 26;
        _panel.AddThemeStyleboxOverride("panel", panelStyle);

        _title.AddThemeFontOverride("font", MenuPalette.Text);
        _title.AddThemeFontSizeOverride("font_size", 18);
        _title.AddThemeColorOverride("font_color", MenuPalette.Platinum);

        GetNode<HBoxContainer>("%KickerHost").AddChild(new TrackedText
        {
            Content = "SYSTEM",
            FontSize = 9,
            Tracking = 2.2f,
            Tint = MenuPalette.Alpha(MenuPalette.Signal, 0.75f),
        });

        _close.Flat = true;
        _close.FocusMode = FocusModeEnum.All;
        _close.MouseDefaultCursorShape = CursorShape.PointingHand;
        _close.AddThemeFontOverride("font", MenuPalette.Text);
        _close.AddThemeFontSizeOverride("font_size", 18);
        _close.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.75f));
        _close.AddThemeColorOverride("font_hover_color", MenuPalette.Platinum);
        _close.AddThemeColorOverride("font_pressed_color", MenuPalette.Signal);
        _close.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _close.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        _close.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        _close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        Label hint = GetNode<Label>("%FooterHint");
        hint.AddThemeFontOverride("font", MenuPalette.Mono);
        hint.AddThemeFontSizeOverride("font_size", 9);
        hint.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.35f));
    }

    protected override void InitEvent()
    {
        OnClicks(_close, CloseDialog);
        Binder.OnGuiInputLeftClick(_dim, CloseDialog);
    }

    protected override void OnOpen()
    {
        RebuildRows();
        _title.Text = "系统设置";
        Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 1f, 0.16).SetEase(Tween.EaseType.Out);
        _close.GrabFocus();
    }

    protected override void UpdateView()
    {
    }

    protected override void OnClose()
    {
        _controller.SaveSettings();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        CloseDialog();
    }

    private void CloseDialog() => Close();

    private void RebuildRows()
    {
        foreach (Node child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }

        MenuSettings settings = _controller.Settings;

        _content.AddChild(SliderRow("主音量", "MASTER VOLUME", settings.MasterVolume * 100f,
            value => _controller.SetMasterVolume(value / 100f)));
        _content.AddChild(ToggleRow("全屏显示", "FULLSCREEN", settings.Fullscreen, _controller.SetFullscreen));
        _content.AddChild(ToggleRow("垂直同步", "V-SYNC", settings.Vsync, _controller.SetVsync));
        _content.AddChild(ToggleRow("帧率显示", "FRAME COUNTER", settings.ShowFps, _controller.SetShowFps));

        _content.AddChild(VSpace(12));
        _content.AddChild(Text("配置写入 user://menu.cfg，下次启动自动恢复。", 10,
            MenuPalette.Alpha(MenuPalette.Ash, 0.40f)));
    }

    private Control SliderRow(string title, string tag, float value, Action<float> changed)
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

        var readout = new TrackedText
        {
            Content = $"{value:0}%",
            FontSize = 11,
            Tint = MenuPalette.Alpha(MenuPalette.Platinum, 0.85f),
            Tracking = 1.2f,
            Align = HorizontalAlignment.Right,
            MinWidth = 46f,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };

        Binder.OnValueChanged(slider, v =>
        {
            readout.SetText($"{v:0}%");
            changed((float)v);
        });

        row.AddChild(slider);
        row.AddChild(readout);
        return row;
    }

    private Control ToggleRow(string title, string tag, bool value, Action<bool> changed)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48) };
        row.AddThemeConstantOverride("separation", 16);
        row.AddChild(RowLabel(title, tag));
        row.AddChild(Expand());

        var toggle = new TechToggle { On = value, SizeFlagsVertical = SizeFlags.ShrinkCenter };

        void Handler(bool on) => changed(on);
        Binder.Bind(
            () => toggle.SwitchToggled += Handler,
            () =>
            {
                if (GodotObject.IsInstanceValid(toggle)) toggle.SwitchToggled -= Handler;
            });

        row.AddChild(toggle);
        return row;
    }

    private static Control RowLabel(string title, string tag)
    {
        var box = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        box.AddThemeConstantOverride("separation", 2);
        box.AddChild(Text(title, 14, MenuPalette.Alpha(MenuPalette.Platinum, 0.92f)));
        box.AddChild(new TrackedText
        {
            Content = tag,
            FontSize = 9,
            Tint = MenuPalette.Alpha(MenuPalette.Ash, 0.45f),
            Tracking = 1.6f,
        });
        return box;
    }

    private static Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", MenuPalette.Text);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
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
}
