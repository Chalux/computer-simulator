using Godot;
using KemoCard.Frame.UI.Base;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 模式选择页：静态骨架在 <c>mode_select.tscn</c>，模式条目按目录数据生成。
/// 组合根接管模式路由与返回。
/// </summary>
public partial class ModeSelectScreen : BaseWin
{
    public event Action<GameMode>? ModeSelected;
    public event Action? BackRequested;

    private readonly List<(GameMode Mode, TechMenuItem Item)> _items = [];
    private MenuController _controller = null!;
    private GridContainer _grid = null!;
    private Control _left = null!;
    private Control _footer = null!;
    private TechShowcase _showcase = null!;
    private TechLine _rule = null!;

    public override string UIId => UiRoutes.ModeSelect;
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
        _grid = GetNode<GridContainer>("%Grid");

        var title = new TechTitle
        {
            Text = "MODE SELECT",
            FontSize = 54,
            Tracking = 0.5f,
            SplitAt = 4,
            Prefix = MenuPalette.Ash,
            Tint = MenuPalette.Platinum,
        };
        GetNode<VBoxContainer>("%TitleHost").AddChild(title);

        _rule = new TechLine
        {
            Tint = MenuPalette.Steel,
            Solid = false,
            Strength = 0.95f,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = SizeFlags.Fill,
            PivotOffset = Vector2.Zero,
        };
        GetNode<VBoxContainer>("%RuleHost").AddChild(_rule);

        GetNode<VBoxContainer>("%MenuRuleHost").AddChild(new TechLine
        {
            CustomMinimumSize = new Vector2(0, 1),
            Strength = 0.75f,
        });

        Label subtitle = GetNode<Label>("%Subtitle");
        subtitle.AddThemeFontOverride("font", MenuPalette.Text);
        subtitle.AddThemeFontSizeOverride("font_size", 16);
        subtitle.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.78f));

        StyleTextButton(GetNode<Button>("%BackButton"));
        BuildItems();
    }

    protected override void InitEvent()
    {
        foreach ((GameMode mode, TechMenuItem item) in _items)
        {
            GameMode captured = mode;
            OnClicks(item, () => Choose(captured));
        }

        OnClicks(GetNode<Button>("%BackButton"), RequestBack);
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
        if (!Visible) return;
        if (GetViewport().GuiGetFocusOwner() != null) return;
        GrabInitialFocus();
    }

    private void BuildItems()
    {
        _grid.Columns = 2;
        _grid.AddThemeConstantOverride("h_separation", 24);
        _grid.AddThemeConstantOverride("v_separation", 0);

        foreach (GameModeInfo info in _controller.Modes)
        {
            var item = new TechMenuItem
            {
                Index = info.Index,
                Title = info.Title,
                Subtitle = info.Available ? "OPEN" : "SOON",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _grid.AddChild(item);
            _items.Add((info.Mode, item));
        }
    }

    private void Choose(GameMode mode) => ModeSelected?.Invoke(mode);

    private void RequestBack() => BackRequested?.Invoke();

    private void GrabInitialFocus()
    {
        if (!IsInstanceValid(this)) return;
        if (GetViewport().GuiGetFocusOwner() != null) return;

        TechMenuItem? target = null;
        if (_controller.LastSelectedMode is { } wanted)
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

        Tween focus = CreateTween();
        focus.TweenInterval(0.42);
        focus.TweenCallback(Callable.From(GrabInitialFocus));
    }

    private static void StyleTextButton(Button button)
    {
        button.Flat = true;
        button.FocusMode = FocusModeEnum.All;
        button.MouseDefaultCursorShape = CursorShape.PointingHand;
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
        };
        button.AddThemeStyleboxOverride("focus", focus);
    }
}
