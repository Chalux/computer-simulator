using Godot;
using KemoCard.Frame.UI.Base;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 主菜单页面：布局与持久控件来自 <c>main_menu.tscn</c>，本类只负责主题、
/// 自绘装饰、动效与把菜单意图转发给组合根。
/// </summary>
public partial class MainMenuScreen : BaseWin
{
    public event Action? ModesRequested;
    public event Action? SettingsRequested;
    public event Action? CreditsRequested;

    private readonly List<TechMenuItem> _items = [];
    private MenuController _controller = null!;
    private VBoxContainer _menu = null!;
    private TechTitle _title = null!;
    private TechLine _rule = null!;
    private Control _left = null!;
    private Control _footer = null!;
    private TechShowcase _showcase = null!;

    public override string UIId => UiRoutes.MainMenu;
    public override string UIDir => UiRoutes.MenuDir;

    protected override void OnCreate()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = MenuPalette.CreateTheme();
        MouseFilter = MouseFilterEnum.Stop;

        _controller = Facade<MenuController>();
        _left = GetNode<Control>("%Left");
        _footer = GetNode<Control>("%Footer");
        _showcase = GetNode<TechShowcase>("%Showcase");
        _menu = GetNode<VBoxContainer>("%Menu");

        BuildHero();
        BuildItems();
        BuildFooter();

        GetNode<Label>("%Subtitle").AddThemeFontOverride("font", MenuPalette.Text);
        GetNode<Label>("%Subtitle").AddThemeFontSizeOverride("font_size", 16);
        GetNode<Label>("%Subtitle").AddThemeColorOverride("font_color",
            MenuPalette.Alpha(MenuPalette.Ash, 0.78f));
    }

    protected override void InitEvent()
    {
        OnClicks(_items[0], RequestModes);
        OnClicks(_items[1], OpenSettings);
        OnClicks(_items[2], OpenCredits);
        OnClicks(_items[3], _controller.RequestQuit);
    }

    protected override void OnOpen()
    {
        PlayIntro();
    }

    protected override void UpdateView()
    {
    }

    protected override void OnLayerVisibleUpdate()
    {
        if (!Visible || _items.Count == 0) return;
        if (GetViewport().GuiGetFocusOwner() != null) return;
        _items[0].GrabFocus();
    }

    /// <summary>弹层关闭后恢复菜单焦点；由组合根在 Dlg 关闭事件里调用。</summary>
    internal void FocusInitial()
    {
        if (!IsInstanceValid(this) || !Visible || _items.Count == 0) return;
        _items[0].GrabFocus();
    }

    private void BuildHero()
    {
        var kicker = new TrackedText
        {
            Content = "HARDWARE ASSEMBLY SIMULATOR",
            FontSize = 9,
            Tint = MenuPalette.Alpha(MenuPalette.Ash, 0.78f),
            Tracking = 3.0f,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        GetNode<HBoxContainer>("%KickerHost").AddChild(kicker);

        _title = new TechTitle
        {
            Text = "PC ARCHITECT",
            FontSize = 76,
            Tracking = 0.5f,
            SplitAt = 2,
            Prefix = MenuPalette.Ash,
            Tint = MenuPalette.Platinum,
        };
        GetNode<VBoxContainer>("%TitleHost").AddChild(_title);

        _rule = new TechLine
        {
            Tint = MenuPalette.Steel,
            Solid = false,
            Strength = 0.95f,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = SizeFlags.Fill,
            PivotOffset = Vector2.Zero,
        };
        GetNode<VBoxContainer>("%HeroRuleHost").AddChild(_rule);

        GetNode<VBoxContainer>("%MenuRuleHost").AddChild(new TechLine
        {
            CustomMinimumSize = new Vector2(0, 1),
            Strength = 0.75f,
        });
    }

    private void BuildItems()
    {
        AddItem(GetNode<TechMenuItem>("%StartItem"), "01", "开始装配", "START BUILD");
        AddItem(GetNode<TechMenuItem>("%SettingsItem"), "02", "系统设置", "SYSTEM CONFIG");
        AddItem(GetNode<TechMenuItem>("%CreditsItem"), "03", "制作名单", "CREDITS");
        AddItem(GetNode<TechMenuItem>("%QuitItem"), "04", "退出游戏", "EXIT");
        _menu.AddThemeConstantOverride("separation", 0);
    }

    private void AddItem(TechMenuItem item, string index, string title, string subtitle)
    {
        item.Index = index;
        item.Title = title;
        item.Subtitle = subtitle;
        item.QueueRedraw();
        _items.Add(item);
    }

    private void BuildFooter()
    {
        GetNode<VBoxContainer>("%FooterRuleHost").AddChild(new TechLine
        {
            CustomMinimumSize = new Vector2(0, 1),
            Strength = 0.55f,
        });

        var row = GetNode<HBoxContainer>("%FooterRow");
        var dot = new StatusDot
        {
            Radius = 2f,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        row.AddChild(dot);
        row.MoveChild(dot, 0);

        var build = Tracked("BUILD 0.1.0", 9, MenuPalette.Alpha(MenuPalette.Ash, 0.58f), 1.4f,
            vertical: SizeFlags.ShrinkCenter);
        row.AddChild(build);
        row.MoveChild(build, 1);

        var copyright = Tracked("© 2026 PC ARCHITECT", 9, MenuPalette.Alpha(MenuPalette.Ash, 0.48f), 1.4f,
            vertical: SizeFlags.ShrinkCenter);
        row.AddChild(copyright);
    }

    private void RequestModes() => ModesRequested?.Invoke();

    private void OpenSettings() => SettingsRequested?.Invoke();

    private void OpenCredits() => CreditsRequested?.Invoke();

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
}
