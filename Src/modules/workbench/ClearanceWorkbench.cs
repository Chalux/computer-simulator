using System.Reflection;
using ComputerSimulator.Modules.Benchmark;
using ComputerSimulator.Modules.Commission;
using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Inspection;
using Godot;
using KemoCard.Frame.Mvc;
using KemoCard.Frame.UI;
using KemoCard.Frame.UI.Base;

namespace ComputerSimulator.Modules.Workbench;

/// <summary>
/// 工作台页面：装配与委托共用一份场景与一套控件，控制器由会话门面提供。
/// 布局、持久控件与列表容器来自 <c>workbench.tscn</c>；动态选项与插槽按硬件目录生成。
/// </summary>
public partial class ClearanceWorkbench : BaseWin
{
    /// <summary>请求返回上一层（由组合根接管路由）。</summary>
    public event Action? BackRequested;

    internal CommissionController? CommissionController { get; private set; }
    public bool CrateMode => _facade.Assembly?.CrateMode == true;
    public bool CommissionMode => _facade.CommissionMode;

    private BenchCatalog Catalog => _facade.Commission?.Catalog ?? _facade.Assembly!.Catalog;

    private WorkbenchUiFacade _facade = null!;
    private BenchmarkReportView _report = null!;

    private BenchViewport _viewport = null!;
    private SectionDrawing _section = null!;
    private OptionButton _case = null!, _memory = null!, _scenario = null!, _gpu = null!;
    private VBoxContainer _air = null!, _water = null!;
    private Label _goal = null!, _hardwareName = null!;
    private Label _crateInventory = null!, _crateResult = null!;
    private readonly List<CheckBox> _slots = [];
    private HBoxContainer _slotsRow = null!;
    private HSlider _offset = null!;
    private Label _offsetText = null!, _status = null!, _metrics = null!, _messages = null!;
    private VBoxContainer _scenarioField = null!, _memoryField = null!, _slotsField = null!, _conditionsField = null!;
    private VBoxContainer _power = null!;
    private HBoxContainer _conditions = null!;
    private OptionButton _psu = null!, _workload = null!, _airflow = null!;
    private TabContainer _views = null!;
    private Button _runLoad = null!;
    private Label _powerResult = null!;
    private Button _submit = null!, _replay = null!, _reset = null!;
    private PanelContainer _statusChip = null!, _loadPanel = null!;
    private HBoxContainer _sectionCell = null!;
    private GridContainer _stripGrid = null!;

    private Button _menuButton = null!, _powerRunLoad = null!, _commissionRunLoad = null!;
    private CheckBox _sidePanel = null!, _rgb = null!;
    private readonly Button[] _viewButtons = new Button[5];

    public override string UIId => UiRoutes.Workbench;
    public override string UIDir => UiRoutes.WorkbenchDir;

    protected override void OnCreate()
    {
        Theme = WorkbenchStyle.CreateTheme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        _facade = Facade<WorkbenchUiFacade>();
        SetControllersFromFacade();

        CacheNodes();
        var viewGroup = new ButtonGroup();
        foreach (Button button in _viewButtons) button.ButtonGroup = viewGroup;
        ApplyWorkbenchStyle();
        PopulateCatalog();
        ConfigureModeVisibility();
        if (CommissionMode) InitializeCommissionView();
    }

    protected override void InitEvent()
    {
        SetControllersFromFacade();

        Binder.OnPressed(_menuButton, () => BackRequested?.Invoke());
        Binder.OnPressed(_powerRunLoad, RunLoad);
        Binder.OnPressed(_commissionRunLoad, RunLoad);
        Binder.OnPressed(_submit, SubmitCrate);
        Binder.OnPressed(_replay, ReplayCrate);
        Binder.OnPressed(_reset, Reset);

        Binder.OnItemSelected(_scenario, _ => Reset());
        Binder.OnItemSelected(_case, _ => Refresh());
        Binder.OnItemSelected(_gpu, _ => Refresh());
        Binder.OnItemSelected(_memory, _ => Refresh());
        Binder.OnItemSelected(_workload, _ => Refresh());
        Binder.OnItemSelected(_airflow, _ => Refresh());
        Binder.OnItemSelected(_psu, _ => Refresh());
        Binder.OnValueChanged(_offset, _ => Refresh());
        Binder.OnResized(_views, UpdateSectionVisibility);

        Binder.OnSignal<BaseButton.ToggledEventHandler>(
            _viewport.ShowPanel,
            handler => _sidePanel.Toggled += handler,
            handler => _sidePanel.Toggled -= handler);
        _viewport.ShowPanel(_sidePanel.ButtonPressed);
        _rgb.SetPressedNoSignal(true);
        _viewport.SetRgb(_rgb.ButtonPressed);
        Binder.OnSignal<BaseButton.ToggledEventHandler>(
            _viewport.SetRgb,
            handler => _rgb.Toggled += handler,
            handler => _rgb.Toggled -= handler);

        for (int i = 0; i < _viewButtons.Length; i++)
        {
            int view = i;
            Binder.OnPressed(_viewButtons[i], () => _viewport.SetView(view));
        }

        Binder.OnSignal<Action<int>>(
            OnViewPresetChanged,
            handler => _viewport.ViewPresetChanged += handler,
            handler => _viewport.ViewPresetChanged -= handler);

        foreach (CheckBox check in _slots)
        {
            Binder.OnSignal<BaseButton.ToggledEventHandler>(
                OnSlotToggled,
                handler => check.Toggled += handler,
                handler => check.Toggled -= handler);
        }

        if (CommissionMode) BindCommissionInputs();

        BindState();
        UpdateSectionVisibility();
    }

    protected override void OnOpen()
    {
        if (_facade.Report is { } report) _report.Bind(report);
        UpdateSectionVisibility();
    }

    protected override void UpdateView()
    {
        UpdateSectionVisibility();
    }

    private void CacheNodes()
    {
        _viewport = GetNode<BenchViewport>("%Viewport");
        _section = GetNode<SectionDrawing>("%Section");
        _case = GetNode<OptionButton>("%CaseChoice");
        _memory = GetNode<OptionButton>("%MemoryChoice");
        _scenario = GetNode<OptionButton>("%ScenarioChoice");
        _gpu = GetNode<OptionButton>("%GpuChoice");
        _air = GetNode<VBoxContainer>("%AirFields");
        _water = GetNode<VBoxContainer>("%WaterFields");
        _goal = GetNode<Label>("%Goal");
        _hardwareName = GetNode<Label>("%HardwareName");
        _crateInventory = GetNode<Label>("%CrateInventory");
        _crateResult = GetNode<Label>("%CrateResult");
        _slotsRow = GetNode<HBoxContainer>("%SlotsRow");
        _offset = GetNode<HSlider>("%Offset");
        _offsetText = GetNode<Label>("%OffsetText");
        _status = GetNode<Label>("%Status");
        _metrics = GetNode<Label>("%Metrics");
        _messages = GetNode<Label>("%Messages");
        _scenarioField = GetNode<VBoxContainer>("%ScenarioField");
        _memoryField = GetNode<VBoxContainer>("%MemoryField");
        _slotsField = GetNode<VBoxContainer>("%SlotsField");
        _conditionsField = GetNode<VBoxContainer>("%ConditionsField");
        _power = GetNode<VBoxContainer>("%Power");
        _conditions = GetNode<HBoxContainer>("%Conditions");
        _psu = GetNode<OptionButton>("%PsuChoice");
        _workload = GetNode<OptionButton>("%WorkloadChoice");
        _airflow = GetNode<OptionButton>("%AirflowChoice");
        _views = GetNode<TabContainer>("%Views");
        _runLoad = GetNode<Button>("%PowerRunLoad");
        _powerResult = GetNode<Label>("%PowerResult");
        _submit = GetNode<Button>("%SubmitButton");
        _replay = GetNode<Button>("%ReplayButton");
        _reset = GetNode<Button>("%ResetButton");
        _statusChip = GetNode<PanelContainer>("%StatusChip");
        _loadPanel = GetNode<PanelContainer>("%LoadPanel");
        _sectionCell = GetNode<HBoxContainer>("%SectionCell");
        _stripGrid = GetNode<GridContainer>("%StripGrid");
        _menuButton = GetNode<Button>("%MenuButton");
        _powerRunLoad = GetNode<Button>("%PowerRunLoad");
        _commissionRunLoad = GetNode<Button>("%CommissionRunLoad");
        _sidePanel = GetNode<CheckBox>("%SidePanelCheck");
        _rgb = GetNode<CheckBox>("%RgbCheck");
        _report = GetNode<BenchmarkReportView>("%ReportView");

        for (int i = 0; i < _viewButtons.Length; i++)
        {
            _viewButtons[i] = GetNode<Button>($"%View{i}");
        }

        _runLoad = CommissionMode ? _commissionRunLoad : _powerRunLoad;
    }

    private void SetControllersFromFacade()
    {
        // StateView 由渲染 worker 维护，其 AssemblyController / ReportController 声明为 init-only；
        // 场景实例由 UIManager 创建，无法使用对象初始化器。这里沿部分类边界写入其 init 访问器，
        // 避免改动画布渲染代码或把控制器塞进路由载荷。
        SetInitProperty(this, nameof(AssemblyController), _facade.Assembly);
        SetInitProperty(this, nameof(ReportController), _facade.Report);
        this.CommissionController = _facade.Commission;
    }

    private static void SetInitProperty<T>(object target, string name, T value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"工作台界面缺少控制器属性：{name}");
        property.SetValue(target, value);
    }

    private void ApplyWorkbenchStyle()
    {
        GetNode<PanelContainer>("%Frame").AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.Canvas, WorkbenchStyle.Canvas, 14, 12, 0, 0));

        StyleLabel("%TitleLabel", 24, WorkbenchStyle.Text, WorkbenchStyle.TextFont);
        StyleLabel("%PcWordmark", 10, WorkbenchStyle.Mint, WorkbenchStyle.MonoFont);
        StyleLabel("%ArchWordmark", 10, WorkbenchStyle.Muted, WorkbenchStyle.DisplayFont);
        StyleLabel("%SectionTitle", 13, WorkbenchStyle.Muted);
        StyleLabel("%SectionNote", 11, WorkbenchStyle.Coral);
        StyleLabel("%MetricsTitle", 13, WorkbenchStyle.Muted);

        _status.AddThemeFontOverride("font", WorkbenchStyle.MonoFont);
        _status.AddThemeFontSizeOverride("font_size", 13);
        _status.AddThemeColorOverride("font_color", WorkbenchStyle.Text);
        _status.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        _goal.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _goal.AddThemeFontSizeOverride("font_size", 13);
        _goal.AddThemeColorOverride("font_color", WorkbenchStyle.TextSoft);
        _goal.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _goal.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _hardwareName.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _hardwareName.AddThemeFontSizeOverride("font_size", 11);
        _hardwareName.AddThemeColorOverride("font_color", WorkbenchStyle.Muted);
        _hardwareName.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hardwareName.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _crateInventory.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _crateInventory.AddThemeFontSizeOverride("font_size", 11);
        _crateInventory.AddThemeColorOverride("font_color", WorkbenchStyle.Muted);
        _crateInventory.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _metrics.AddThemeFontOverride("font", WorkbenchStyle.MonoFont);
        _metrics.AddThemeFontSizeOverride("font_size", 13);
        _metrics.AddThemeColorOverride("font_color", WorkbenchStyle.TextSoft);
        _metrics.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _metrics.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _messages.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _messages.AddThemeFontSizeOverride("font_size", 14);
        _messages.AddThemeColorOverride("font_color", WorkbenchStyle.Text);
        _messages.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _messages.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _messages.SizeFlagsVertical = SizeFlags.ExpandFill;
        _messages.VerticalAlignment = VerticalAlignment.Top;
        _messages.MouseFilter = MouseFilterEnum.Pass;

        _offsetText.AddThemeFontOverride("font", WorkbenchStyle.MonoFont);
        _offsetText.AddThemeFontSizeOverride("font_size", 11);
        _offsetText.AddThemeColorOverride("font_color", WorkbenchStyle.Mint);
        _offsetText.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        _powerResult.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _powerResult.AddThemeFontSizeOverride("font_size", 12);
        _powerResult.AddThemeColorOverride("font_color", WorkbenchStyle.Text);
        _powerResult.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _crateResult.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _crateResult.AddThemeFontSizeOverride("font_size", 11);
        _crateResult.AddThemeColorOverride("font_color", WorkbenchStyle.Text);
        _crateResult.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _crateResult.Visible = CrateMode;

        foreach (string name in new[]
                 {
                     "%ScenarioCaption", "%CaseCaption", "%GpuCaption", "%MemoryCaption",
                     "%SlotsCaption", "%OffsetCaption", "%ConditionsCaption", "%PsuCaption",
                 })
        {
            StyleLabel(name, 13, WorkbenchStyle.Muted, WorkbenchStyle.TextFont);
        }

        StyleLabel("%LoadHeading", 15, WorkbenchStyle.Text, WorkbenchStyle.TextFont);

        foreach (string panel in new[]
                 {
                     "%StagePanel", "%InstrumentStrip", "%SectionFrame", "%InventoryPanel",
                     "%LoadPanel",
                 })
        {
            GetNode<PanelContainer>(panel).AddThemeStyleboxOverride("panel",
                WorkbenchStyle.Box(WorkbenchStyle.Surface, WorkbenchStyle.BorderSoft, 10, 10, 2));
        }

        GetNode<PanelContainer>("%StagePanel").AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.Inset, WorkbenchStyle.BorderSoft, 1, 1, 2));
        GetNode<PanelContainer>("%SectionFrame").AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.Inset, WorkbenchStyle.BorderSoft, 0, 0, 2));
        GetNode<PanelContainer>("%InstrumentStrip").AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.Surface, WorkbenchStyle.BorderSoft, 10, 10, 2));
        GetNode<PanelContainer>("%InventoryPanel").AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.Inset, WorkbenchStyle.BorderSoft, 10, 8, 2));
        GetNode<PanelContainer>("%LoadPanel").AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.SurfaceRaised, WorkbenchStyle.BorderSoft, 10, 10, 3));
        GetNode<PanelContainer>("%Inspector").AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.Surface, WorkbenchStyle.Border, 0, 0, 3));
        GetNode<PanelContainer>("%ViewRail").AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.Inset, WorkbenchStyle.BorderSoft, 3, 3, 2));

        WorkbenchStyle.ApplyStatusChip(_statusChip, WorkbenchStyle.Mint);
        WorkbenchStyle.ApplyGhost(_menuButton, 28);
        WorkbenchStyle.ApplyPrimary(_powerRunLoad, 34);
        WorkbenchStyle.ApplyPrimary(_commissionRunLoad, 34);
        WorkbenchStyle.ApplyPrimary(_submit, 34);
        WorkbenchStyle.ApplyGhost(_reset, 30);

        _replay.CustomMinimumSize = new Vector2(0, 30);
        _reset.CustomMinimumSize = new Vector2(0, 30);

        foreach (Button button in _viewButtons)
        {
            WorkbenchStyle.ApplySegment(button);
        }
    }

    private void StyleLabel(string name, int size, Color color, Font? font = null)
    {
        var label = GetNode<Label>(name);
        label.AddThemeFontOverride("font", font ?? WorkbenchStyle.TextFont);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
    }

    private void PopulateCatalog()
    {
        BenchCatalog catalog = Catalog;

        _scenario.AddItem("双塔风冷与内存");
        _scenario.AddItem("前置 360 冷排与显卡");
        StyleSelect(_scenario);
        _scenario.Visible = !CrateMode;

        foreach (var item in catalog.Cases)
            _case.AddItem(CommissionMode
                ? $"{item.Name} · ¥{item.Price:N0}"
                : $"{item.Name} · 高 {item.HeightLimit} / 长 {item.GpuLimit} mm");
        StyleSelect(_case);

        foreach (var card in catalog.Graphics)
            _gpu.AddItem($"{card.Name} · {card.Dimensions[0]} mm" + (CommissionMode ? $" · ¥{card.Price:N0}" : ""));
        StyleSelect(_gpu);

        foreach (var item in catalog.Ram)
            _memory.AddItem($"{item.Name} · {item.Dimensions[2]} mm" + (CommissionMode ? $" · ¥{item.Price:N0}/条" : ""));
        StyleSelect(_memory);
        _memory.Visible = !CrateMode;

        _slotsRow.AddThemeConstantOverride("separation", 4);
        _slotsRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        foreach (var slot in catalog.Board.Slots)
        {
            var check = new CheckBox
            {
                Text = slot.Name,
                FocusMode = FocusModeEnum.All,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _slotsRow.AddChild(check);
            _slots.Add(check);
        }

        _offset.MaxValue = catalog.Cooler.Adjustable_y_max;
        _offset.CustomMinimumSize = new Vector2(0, 22);

        foreach (var profile in catalog.Benchmark.Workloads) _workload.AddItem(profile.Name);
        foreach (var preset in catalog.Benchmark.Airflows) _airflow.AddItem(preset.Name);
        StyleSelect(_workload);
        StyleSelect(_airflow);

        foreach (var psu in catalog.PowerSupplies)
            _psu.AddItem(CommissionMode
                ? $"{psu.Name} · ¥{psu.Price:N0}"
                : $"{psu.Name} · 持续 {psu.Continuous} / 短时 {psu.Pulse} W");
        StyleSelect(_psu);

        _views.SetTabTitle(0, "装配视图");
        _views.SetTabTitle(1, "负载报告");
        _views.CurrentTab = 0;
        _views.SetTabDisabled(1, true);

        if (_facade.Assembly?.Challenge is { } challenge)
        {
            RestrictChoices(_case, challenge.CaseIndices);
            RestrictChoices(_gpu, challenge.GpuIndices);
            RestrictChoices(_psu, challenge.PsuIndices);
            var kit = challenge.Fixed;
            var slots = kit.Slots.Select(index => catalog.Board.Slots[index].Name);
            var cooler = kit.Front360 ? catalog.Liquid.Name : catalog.Cooler.Name;
            _crateInventory.Text =
                $"箱内配件\n机箱 {string.Join(" / ", challenge.CaseIndices.Select(index => catalog.Cases[index].Name))}\n" +
                $"显卡 {string.Join(" / ", challenge.GpuIndices.Select(index => catalog.Graphics[index].Name))}\n" +
                $"电源 {string.Join(" / ", challenge.PsuIndices.Select(index => catalog.PowerSupplies[index].Name))}\n" +
                $"固定件 {cooler}、{catalog.Ram[kit.RamIndex].Name} ×{kit.Slots.Length}（{string.Join(" + ", slots)}）\n" +
                $"{catalog.Benchmark.Workloads[kit.WorkloadIndex].Name}、{catalog.Benchmark.Airflows[kit.AirflowIndex].Name}";
        }
    }

    private static void RestrictChoices(OptionButton option, IReadOnlyCollection<int> allowed)
    {
        for (int i = 0; i < option.ItemCount; i++)
            option.SetItemDisabled(i, !allowed.Contains(i));
    }

    private void ConfigureModeVisibility()
    {
        _scenarioField.Visible = !CrateMode && !CommissionMode;
        _memoryField.Visible = !CrateMode;
        _slotsField.Visible = !CrateMode;
        _conditions.Visible = !CrateMode;
        _conditionsField.Visible = !CrateMode;
        _workload.Visible = !CommissionMode;
        _powerRunLoad.Visible = !CommissionMode;
        _commissionRunLoad.Visible = CommissionMode;
        _runLoad = CommissionMode ? _commissionRunLoad : _powerRunLoad;
        _runLoad.Text = CommissionMode ? "运行负载测试" : "运行整机负载测试";
        _menuButton.Text = CommissionMode ? "返回委托" : "模式选择";
        _menuButton.TooltipText = CommissionMode ? "返回委托（ESC）" : "返回模式选择（ESC）";
        GetNode<Label>("%ConditionsCaption").Text =
            CommissionMode ? "通风工况" : "目标用途与通风工况";

        string title = CommissionMode
            ? $"委托 {_facade.Commission!.Order.Id}"
            : _facade.Assembly?.Challenge?.Title ?? "沙盒工作台";
        GetNode<Label>("%TitleLabel").Text = title;

        _submit.Visible = false;
        _replay.Visible = false;
        GetNode<PanelContainer>("%InventoryPanel").Visible = CrateMode;
        GetNode<PanelContainer>("%CommissionChecklist").Visible = CommissionMode;
    }

    private static void StyleSelect(OptionButton option)
    {
        option.FitToLongestItem = false;
        option.ClipText = true;
        option.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        option.FocusMode = FocusModeEnum.All;
    }

    private void UpdateSectionVisibility()
    {
        if (_sectionCell == null) return;
        if (_stripGrid != null && _views.Size.X > 0) _stripGrid.Columns = _views.Size.X < 560 ? 1 : 2;
    }

    #region 工作台交互

    private void BindState()
    {
        if (_facade.Report is { } report) _report.Bind(report);

        if (_facade.Assembly is { } assembly)
        {
            Binder.Add(assembly.SubscribeState(RenderWorkbenchState, this).Dispose);
            RenderWorkbenchState(assembly.State);
            return;
        }

        if (_facade.Commission is { } commission)
        {
            var events = ((IEventBusProvider)commission).EventBus;
            var subscription = events.On(
                CommissionEvents.StateChanged,
                (state, _) => RenderCommissionState(state),
                this);
            Binder.Add(subscription.Off);
            RenderCommissionState(commission.Snapshot);
        }
    }

    private void OnSlotToggled(bool _) => Refresh();

    private void OnViewPresetChanged(int active)
    {
        for (int i = 0; i < _viewButtons.Length; i++)
            _viewButtons[i].SetPressedNoSignal(active == i);
    }

    private void Reset()
    {
        if (_facade.Commission is { } commission)
            commission.ResetSetup();
        else
            _facade.Assembly!.Reset(_scenario.Selected == 1);
    }

    private BenchSetup Selection()
    {
        var slots = Enumerable.Range(0, _slots.Count).Where(i => _slots[i].ButtonPressed).ToArray();
        return new BenchSetup(
            _case.Selected, _memory.Selected, slots, (int)_offset.Value,
            _scenario.Selected == 1, _gpu.Selected, _airflow.Selected, _workload.Selected, _psu.Selected);
    }

    private void RunLoad()
    {
        if (_facade.Commission is { } commission)
            commission.RunBenchmark();
        else
            _facade.Assembly!.RunLoad();
    }

    private void SubmitCrate() => _facade.Assembly!.SubmitCrate();

    private void ReplayCrate() => _facade.Assembly!.ReplayCrate();

    private void Refresh()
    {
        if (_renderingSetup) return;

        var setup = Selection();
        if (_facade.Commission is { } commission)
            commission.SetSetup(setup);
        else
            _facade.Assembly!.ChangeSetup(setup);
    }

    private static void SetTextColor(Label label, Color color)
    {
        label.Modulate = Colors.White;
        label.AddThemeColorOverride("font_color", color);
    }

    private static Color StateColor(string status) => status switch
    {
        "BLOCKED" => WorkbenchStyle.Coral,
        "DEGRADED" => WorkbenchStyle.Amber,
        _ => WorkbenchStyle.Mint,
    };

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        SetTextColor(_status, color);
        WorkbenchStyle.ApplyStatusChip(_statusChip, color);
    }

    private static void SyncOptionTooltip(OptionButton option)
    {
        int index = option.Selected;
        option.TooltipText = index >= 0 && index < option.ItemCount ? option.GetItemText(index) : "";
    }

    #endregion
}
