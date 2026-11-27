using System.Text.Json;
using System.Text.Json.Serialization;
using KemoCard.Frame.Content;
using KemoCard.Frame.Content.Simulator;
using KemoCard.Frame.Scripting;
using KemoCard.Frame.Logging;
using KemoCard.Fixed.Godot;

namespace ComputerSimulator.Modules.Hardware;

/// <summary>订单条款的已解析形式（硬件 id 已变成稳定下标与展示名）。</summary>
public sealed record CommissionContentDefinition(
    string Id,
    string Title,
    string CustomerMessage,
    string ModeLabel,
    int RequiredGpuIndex,
    string RequiredGpuName,
    int Budget,
    int BuildFee,
    int MinimumMemoryGb,
    int WorkloadIndex,
    string WorkloadName,
    bool RequiresFront360);

/// <summary>故障诊断题面的已解析形式。</summary>
public sealed record DiagnosisCaseContent(
    string Id,
    string Title,
    string Subtitle,
    string CustomerBrief,
    int CaseIndex,
    BenchSetup Setup,
    int FaultyPsuIndex,
    int ReplacementAllowance);

/// <summary>给定硬件挑战（神秘箱）的已解析形式。</summary>
public sealed record CrateChallengeContent(
    string Id,
    string Title,
    int[] CaseIndices,
    int[] GpuIndices,
    int[] PsuIndices,
    BenchSetup Initial,
    BenchSetup Fixed,
    int SubmitWorkloadIndex);

/// <summary>
/// 由内容 Mod 管线加载并校验过的全部装机模拟器业务数据。
/// <see cref="Load"/> 是唯一入口：默认注册表加载 <c>user://mods</c>（可写、优先）与
/// <c>res://mods</c>（PCK 内默认 basemod），随后把硬件目录、订单、诊断、挑战与模式
/// 解析成强类型对象。任何必需内容缺失都会直接抛出，不提供静态目录兜底。
/// </summary>
public sealed class GameContent
{
    public const string DefaultModId = "basemod";
    public const string DefaultBenchmarkId = "standard";

    public static readonly IReadOnlyList<string> DefaultModRoots =
        new[] { "user://mods", "res://mods" };

    private GameContent(
        BenchCatalog catalog,
        IReadOnlyList<GameModeDefinitionDto> gameModes,
        CommissionContentDefinition? commission,
        DiagnosisCaseContent? diagnosisCase,
        CrateChallengeContent? crateChallenge,
        ContentLoadReport loadReport)
    {
        Catalog = catalog;
        GameModes = gameModes;
        Commission = commission;
        DiagnosisCase = diagnosisCase;
        CrateChallenge = crateChallenge;
        LoadReport = loadReport;
    }

    /// <summary>视图与规则共用的装配硬件目录。</summary>
    public BenchCatalog Catalog { get; }

    public IReadOnlyList<GameModeDefinitionDto> GameModes { get; }

    public CommissionContentDefinition? Commission { get; }

    public DiagnosisCaseContent? DiagnosisCase { get; }

    public CrateChallengeContent? CrateChallenge { get; }

    public ContentLoadReport LoadReport { get; }

    /// <summary>供规则脚本使用的完整目录 JSON（每次加载按内容重新生成，顺序稳定）。</summary>
    public string RuleCatalogJson => Catalog.RuleCatalogJson;

    /// <summary>
    /// 加载默认内容（basemod + 已安装第三方 Mod）。<paramref name="enabledModIds"/> 为 <c>null</c>
    /// 时启用全部扫描到的 Mod；显式传入时按依赖展开与 loadOrder 排序。
    /// </summary>
    public static GameContent Load(IReadOnlyList<string>? enabledModIds = null) =>
        Load(DefaultModRoots, enabledModIds, GodotContentModFileSystem.Instance);

    /// <summary>
    /// 可注入根目录 / 文件系统的加载入口（供工具链与编辑器外测试使用）。
    /// 根目录顺序即覆盖优先级，第一个根目录中的同名 Mod 胜出。
    /// </summary>
    internal static GameContent Load(
        IReadOnlyList<string> modRootDirectories,
        IReadOnlyList<string>? enabledModIds,
        IContentModFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(modRootDirectories);
        ArgumentNullException.ThrowIfNull(fileSystem);

        var registry = new GameDefinitionRegistry();
        var pipeline = new ContentModPipeline(
            modRootDirectories,
            registry,
            new GodotContentModLogger(new StaticAppLogBridge()),
            new NullContentModUserNotifier(),
            NullScriptRuntimeResetter.Instance,
            new ModScriptCatalog(),
            fileSystem: fileSystem);

        var enabled = enabledModIds is null
            ? null
            : enabledModIds.Prepend(DefaultModId).Distinct(StringComparer.Ordinal).ToArray();
        var report = pipeline.Rebuild(enabled);
        var store = registry.Store;
        var builder = new CatalogBuilder(store, registry, pipeline);

        var catalog = builder.BuildCatalog();
        var gameModes = store.GameModes.Values
            .OrderBy(static mode => mode.Order)
            .ThenBy(static mode => mode.Id, StringComparer.Ordinal)
            .ToArray();

        var commission = BuildCommission(store, builder);
        var diagnosis = BuildDiagnosis(store, builder);
        var challenge = BuildChallenge(store, builder);

        return new GameContent(catalog, gameModes, commission, diagnosis, challenge, report);
    }

    private static CommissionContentDefinition? BuildCommission(GameDefinitionStore store, CatalogBuilder builder)
    {
        var dto = store.Commissions.Values
            .OrderBy(static item => item.Order)
            .ThenBy(static item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (dto is null)
        {
            return null;
        }

        int gpuIndex = builder.IndexOfGpu(dto.RequiredGpuId);
        string gpuName = builder.GpuName(gpuIndex);

        (int workloadIndex, string workloadName) = builder.ResolveWorkload(dto.WorkloadId);
        return new CommissionContentDefinition(
            dto.Id,
            dto.Title,
            dto.CustomerMessage,
            dto.ModeLabel,
            gpuIndex,
            gpuName,
            dto.Budget,
            dto.BuildFee,
            dto.MinimumMemoryGb,
            workloadIndex,
            workloadName,
            dto.RequiresFront360);
    }

    private static DiagnosisCaseContent? BuildDiagnosis(GameDefinitionStore store, CatalogBuilder builder)
    {
        var dto = store.DiagnosisCases.Values
            .OrderBy(static item => item.Order)
            .ThenBy(static item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (dto is null)
        {
            return null;
        }

        var setup = builder.ResolveSetup(dto.Setup ?? new BenchSetupDto(), $"诊断案件 '{dto.Id}'");
        int caseIndex = store.TryGetCase(dto.CaseId, out var chassis)
            ? builder.IndexOfCase(chassis.Id)
            : setup.CaseIndex;
        int faultyPsu = builder.IndexOfPsu(dto.FaultyPsuId);
        if (dto.ReplacementAllowance < 1)
            throw new InvalidDataException($"诊断案件 '{dto.Id}' 的 replacementAllowance 必须大于零。");
        return new DiagnosisCaseContent(
            dto.Id,
            dto.Title,
            dto.Subtitle,
            dto.CustomerBrief,
            caseIndex,
            setup,
            faultyPsu,
            dto.ReplacementAllowance);
    }

    private static CrateChallengeContent? BuildChallenge(GameDefinitionStore store, CatalogBuilder builder)
    {
        var dto = store.Challenges.Values
            .Where(static item => item.Crate)
            .OrderBy(static item => item.Order)
            .ThenBy(static item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (dto is null)
        {
            return null;
        }

        var caseIds = dto.CaseIds ?? Array.Empty<string>();
        var gpuIds = dto.GpuIds ?? Array.Empty<string>();
        var psuIds = dto.PsuIds ?? Array.Empty<string>();
        var fixedDto = dto.Fixed ?? new BenchSetupDto();

        var initial = builder.ResolveSetup(
            new BenchSetupDto
            {
                CaseId = string.IsNullOrWhiteSpace(dto.InitialCaseId) ? caseIds.FirstOrDefault() ?? "" : dto.InitialCaseId,
                GpuId = string.IsNullOrWhiteSpace(dto.InitialGpuId) ? gpuIds.FirstOrDefault() ?? "" : dto.InitialGpuId,
                PsuId = string.IsNullOrWhiteSpace(dto.InitialPsuId) ? psuIds.FirstOrDefault() ?? "" : dto.InitialPsuId,
                RamId = fixedDto.RamId,
                Slots = fixedDto.Slots,
                Offset = fixedDto.Offset,
                Front360 = fixedDto.Front360,
                AirflowId = fixedDto.AirflowId,
                WorkloadId = fixedDto.WorkloadId,
            },
            $"挑战 '{dto.Id}'");

        var fixedSetup = builder.ResolveSetup(fixedDto, $"挑战 '{dto.Id}'");
        int submitWorkload = string.IsNullOrWhiteSpace(dto.SubmitWorkloadId)
            ? fixedSetup.WorkloadIndex
            : builder.ResolveWorkload(dto.SubmitWorkloadId).Index;

        return new CrateChallengeContent(
            dto.Id,
            dto.Title,
            caseIds.Select(builder.IndexOfCase).ToArray(),
            gpuIds.Select(builder.IndexOfGpu).ToArray(),
            psuIds.Select(builder.IndexOfPsu).ToArray(),
            initial,
            fixedSetup,
            submitWorkload);
    }

    private sealed class CatalogBuilder
    {
        private readonly GameDefinitionRegistry _registry;
        private readonly ContentModPipeline _pipeline;
        private readonly HardwareCaseDto[] _cases;
        private readonly HardwareMemoryDto[] _ram;
        private readonly HardwareGraphicsCardDto[] _graphics;
        private readonly HardwarePowerSupplyDto[] _psus;
        private readonly HardwareCoolerDto? _air;
        private readonly HardwareCoolerDto? _liquid;
        private readonly HardwareMotherboardDto _board;
        private readonly HardwareProcessorDto _processor;
        private readonly HardwareStorageDto _storage;
        private readonly BenchmarkDefinitionDto _benchmark;

        public CatalogBuilder(GameDefinitionStore store, GameDefinitionRegistry registry, ContentModPipeline pipeline)
        {
            _registry = registry;
            _pipeline = pipeline;

            _cases = Ordered(store.Cases.Values).ToArray();
            _ram = Ordered(store.MemoryModules.Values).ToArray();
            _graphics = Ordered(store.GraphicsCards.Values).ToArray();
            _psus = Ordered(store.PowerSupplies.Values).ToArray();
            _air = Ordered(store.Coolers.Values)
                .FirstOrDefault(static c => !string.Equals(c.Kind, "liquid", StringComparison.OrdinalIgnoreCase));
            _liquid = Ordered(store.Coolers.Values)
                .FirstOrDefault(static c => string.Equals(c.Kind, "liquid", StringComparison.OrdinalIgnoreCase));
            _board = Ordered(store.Motherboards.Values).FirstOrDefault()
                ?? throw new InvalidDataException("内容缺少主板定义。");
            _processor = Ordered(store.Processors.Values).FirstOrDefault()
                ?? throw new InvalidDataException("内容缺少处理器定义。");
            _storage = Ordered(store.StorageDevices.Values).FirstOrDefault()
                ?? throw new InvalidDataException("内容缺少存储定义。");
            _benchmark = Ordered(store.Benchmarks.Values).FirstOrDefault()
                ?? throw new InvalidDataException("内容缺少基准定义（content/benchmarks）。");

            Require(_cases.Length, "机箱");
            Require(_ram.Length, "内存");
            Require(_graphics.Length, "显卡");
            Require(_psus.Length, "电源");
            if (_air is null) throw new InvalidDataException("内容缺少风冷散热器定义。");
            if (_liquid is null) throw new InvalidDataException("内容缺少水冷散热器定义。");
        }

        public BenchCatalog BuildCatalog()
        {
            var cases = _cases.Select(static item => new CaseInfo(
                item.Name, item.HeightLimit, item.GpuLimit, item.PsuLimit, item.Price, item.PsuAnchor)).ToArray();
            var ram = _ram.Select(static item => new MemoryInfo(item.Name, item.Dimensions ?? Array.Empty<int>(), item.Price)).ToArray();
            var graphics = _graphics.Select(static item => new CardInfo(item.Name, item.Dimensions ?? Array.Empty<int>(), item.Price)).ToArray();
            var psus = _psus.Select(static item => new PsuInfo(
                item.Name, item.Continuous, item.Pulse, item.Length, item.Cable, item.Recommended, item.Price,
                item.Width, item.Height)).ToArray();
            var slots = (_board.Slots ?? Array.Empty<MotherboardSlotDto>()).Select(static slot => new SlotInfo(slot.Name, slot.Channel)).ToArray();
            var board = new BoardInfo(_board.Name, slots, _board.Price,
                _board.ProcessorAnchor, _board.StorageAnchor);
            var cooler = new CoolerInfo(_air!.Name, _air.AdjustableYMax);
            var liquid = new LiquidInfo(_liquid!.Name, _liquid.CoreThickness, _liquid.FanThickness, _liquid.Price);
            var benchmark = new BenchmarkOptions(
                AirflowDtos(_benchmark).Select(static item => new TestPreset(item.Name)).ToArray(),
                WorkloadDtos(_benchmark).Select(static item => new TestPreset(item.Name)).ToArray());
            var initial = ResolveSetup(_benchmark.Initial ?? new BenchSetupDto(), "基准初始配置");
            var processor = new ProcessorInfo(_processor.Name, _processor.Price);
            var storage = new StorageInfo(_storage.Name, _storage.Price);

            string json = BuildRuleCatalog(board, cooler, liquid, initial);

            return new BenchCatalog(
                board,
                cooler,
                cases,
                ram,
                graphics,
                liquid,
                psus,
                benchmark,
                initial,
                processor,
                storage,
                json,
                _cases.Select(item => Asset(EContentCategory.SimCase, item.Id, item.Asset)).ToArray(),
                _graphics.Select(item => Asset(EContentCategory.SimGraphicsCard, item.Id, item.Asset)).ToArray(),
                _ram.Select(item => Asset(EContentCategory.SimMemory, item.Id, item.Asset)).ToArray(),
                _psus.Select(item => Asset(EContentCategory.SimPowerSupply, item.Id, item.Asset)).ToArray(),
                Asset(EContentCategory.SimMotherboard, _board.Id, _board.Asset),
                Asset(EContentCategory.SimProcessor, _processor.Id, _processor.Asset),
                Asset(EContentCategory.SimStorage, _storage.Id, _storage.Asset),
                Asset(EContentCategory.SimCooler, _air!.Id, _air.Asset),
                Asset(EContentCategory.SimCooler, _air.Id, string.IsNullOrWhiteSpace(_air.FanAsset) ? _air.Asset : _air.FanAsset),
                Asset(EContentCategory.SimCooler, _liquid!.Id, _liquid.Asset),
                Asset(EContentCategory.SimCooler, _liquid.Id, string.IsNullOrWhiteSpace(_liquid.PumpAsset) ? _liquid.Asset : _liquid.PumpAsset));
        }

        public int IndexOfCase(string id) => IndexOf(_cases, item => item.Id, id, "机箱");
        public int IndexOfGpu(string id) => IndexOf(_graphics, item => item.Id, id, "显卡");
        public int IndexOfPsu(string id) => IndexOf(_psus, item => item.Id, id, "电源");
        public int IndexOfRam(string id) => IndexOf(_ram, item => item.Id, id, "内存");

        public string GpuName(int index) => _graphics[index].Name;

        public (int Index, string Name) ResolveWorkload(string id)
        {
            var workloads = WorkloadDtos(_benchmark).ToArray();
            if (string.IsNullOrWhiteSpace(id))
            {
                return (0, workloads[0].Name);
            }

            for (int i = 0; i < workloads.Length; i++)
            {
                if (string.Equals(workloads[i].Id, id, StringComparison.Ordinal))
                {
                    return (i, workloads[i].Name);
                }
            }

            throw new InvalidDataException($"未知工作负载 id '{id}'。");
        }

        public BenchSetup ResolveSetup(BenchSetupDto dto, string context)
        {
            var airflows = AirflowDtos(_benchmark).ToArray();
            var workloads = WorkloadDtos(_benchmark).ToArray();
            int airflowIndex = string.IsNullOrWhiteSpace(dto.AirflowId)
                ? 0
                : Array.FindIndex(airflows, item => string.Equals(item.Id, dto.AirflowId, StringComparison.Ordinal));
            int workloadIndex = string.IsNullOrWhiteSpace(dto.WorkloadId)
                ? 0
                : Array.FindIndex(workloads, item => string.Equals(item.Id, dto.WorkloadId, StringComparison.Ordinal));
            if (airflowIndex < 0) throw new InvalidDataException($"{context} 的 airflowId '{dto.AirflowId}' 不存在。");
            if (workloadIndex < 0) throw new InvalidDataException($"{context} 的 workloadId '{dto.WorkloadId}' 不存在。");

            return new BenchSetup(
                string.IsNullOrWhiteSpace(dto.CaseId) ? 0 : IndexOfCase(dto.CaseId),
                string.IsNullOrWhiteSpace(dto.RamId) ? 0 : IndexOfRam(dto.RamId),
                [.. (dto.Slots ?? Array.Empty<int>())],
                dto.Offset,
                dto.Front360,
                string.IsNullOrWhiteSpace(dto.GpuId) ? 0 : IndexOfGpu(dto.GpuId),
                airflowIndex,
                workloadIndex,
                string.IsNullOrWhiteSpace(dto.PsuId) ? 0 : IndexOfPsu(dto.PsuId));
        }

        private HardwareAssetRef? Asset(EContentCategory category, string id, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                !_registry.TryGetOwnerModId(category, id, out var modId) ||
                !_pipeline.TryGetModFolderPath(modId, out var folder))
            {
                return null;
            }

            return new HardwareAssetRef(modId, folder, relativePath);
        }

        private string BuildRuleCatalog(
            BoardInfo board,
            CoolerInfo cooler,
            LiquidInfo liquid,
            BenchSetup initial)
        {
            var airflows = AirflowDtos(_benchmark).ToArray();
            var workloads = WorkloadDtos(_benchmark).ToArray();
            var catalog = new RuleCatalog
            {
                Board = new RuleBoard
                {
                    Name = _board.Name,
                    Dimensions = [.. (_board.Dimensions ?? Array.Empty<int>())],
                    Slots = (_board.Slots ?? Array.Empty<MotherboardSlotDto>()).Select(static slot => new RuleSlot
                    {
                        Name = slot.Name,
                        Channel = slot.Channel,
                        Anchor = [.. (slot.Anchor ?? Array.Empty<int>())],
                    }).ToArray(),
                    MaxRate = _board.MaxRate,
                    Price = _board.Price,
                    VrmWatts = _board.VrmWatts,
                    Color = _board.Color,
                    FormFactor = _board.FormFactor,
                },
                Cooler = new RuleCooler
                {
                    Name = cooler.Name,
                    BodyAabb = _air!.BodyAabb ?? [],
                    FanAabb = _air.FanAabb,
                    AdjustableYMax = cooler.Adjustable_y_max,
                },
                Cases = _cases.Select(static item => new RuleCase
                {
                    Name = item.Name,
                    HeightLimit = item.HeightLimit,
                    GpuLimit = item.GpuLimit,
                    PsuLimit = item.PsuLimit,
                    NaturalAirflow = item.NaturalAirflow,
                    Price = item.Price,
                    Color = item.Color,
                    FormFactor = item.FormFactor,
                }).ToArray(),
                Ram = _ram.Select(static item => new RuleMemory
                {
                    Name = item.Name,
                    Dimensions = [.. (item.Dimensions ?? Array.Empty<int>())],
                    Price = item.Price,
                    Color = item.Color,
                    Lighting = item.Lighting,
                    Rgb = item.Rgb,
                    Gb = item.Gb,
                    Rate = item.Rate,
                }).ToArray(),
                Graphics = _graphics.Select(static item => new RuleGraphics
                {
                    Name = item.Name,
                    Dimensions = [.. (item.Dimensions ?? Array.Empty<int>())],
                    FanCount = item.FanCount,
                    Price = item.Price,
                    Performance = item.Performance,
                    Resistance = item.Resistance,
                    Throttle = item.Throttle,
                    MaxFanDb = item.MaxFanDb,
                    Color = item.Color,
                    Lighting = item.Lighting,
                    TestWatts = [.. (item.TestWatts ?? Array.Empty<int>())],
                }).ToArray(),
                Liquid = new RuleLiquid
                {
                    Name = liquid.Name,
                    Price = liquid.Price,
                    Resistance = _liquid!.Resistance,
                    MaxFanDb = _liquid.MaxFanDb,
                    PumpDb = _liquid.PumpDb,
                    Color = _liquid.Color,
                    Lighting = _liquid.Lighting,
                    CoreThickness = _liquid.CoreThickness,
                    FanThickness = _liquid.FanThickness,
                    FanSize = _liquid.FanSize,
                    FanCount = _liquid.FanCount,
                },
                PowerSupplies = _psus.Select(static item => new RulePowerSupply
                {
                    Name = item.Name,
                    Continuous = item.Continuous,
                    Pulse = item.Pulse,
                    Recommended = item.Recommended,
                    Length = item.Length,
                    Price = item.Price,
                    Cable = item.Cable,
                    NoiseDb = item.NoiseDb,
                }).ToArray(),
                LoadCurve = (_benchmark.LoadCurve ?? Array.Empty<BenchmarkLoadCurveDto>()).Select(static point => new RuleLoadPoint
                {
                    Name = point.Name,
                    Cpu = point.Cpu,
                    Other = point.Other,
                    Pulse = point.Pulse,
                }).ToArray(),
                Processor = new RuleProcessor
                {
                    Name = _processor.Name,
                    Price = _processor.Price,
                    Performance = _processor.Performance,
                    Throttle = _processor.Throttle,
                },
                Storage = new RuleStorage
                {
                    Name = _storage.Name,
                    Price = _storage.Price,
                },
                Benchmark = new RuleBenchmark
                {
                    Ambient = _benchmark.Ambient,
                    ReferenceBandwidth = _benchmark.ReferenceBandwidth,
                    Airflows = airflows.Select(static item => new RuleAirflow
                    {
                        Name = item.Name,
                        Intake = item.Intake,
                        Exhaust = item.Exhaust,
                        CaseNoise = item.CaseNoise,
                    }).ToArray(),
                    Workloads = workloads.Select(static item => new RuleWorkload
                    {
                        Name = item.Name,
                        ComputeWeights = [.. (item.ComputeWeights ?? [])],
                        Weights = [.. (item.Weights ?? [])],
                        ReferenceRoi = item.ReferenceRoi,
                        TargetMargin = item.TargetMargin,
                        TargetNoise = item.TargetNoise,
                    }).ToArray(),
                },
                CpuMaxRate = _benchmark.CpuMaxRate,
                Initial = initial,
            };

            return JsonSerializer.Serialize(catalog, RuleJsonOptions);
        }

        private static readonly JsonSerializerOptions RuleJsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        private static IEnumerable<T> Ordered<T>(IEnumerable<T> source) =>
            source.OrderBy(static item => GetOrder<T>(item))
                .ThenBy(static item => GetId<T>(item), StringComparer.Ordinal);

        private static int GetOrder<T>(T item) => item switch
        {
            HardwareCaseDto dto => dto.Order,
            HardwareMotherboardDto dto => dto.Order,
            HardwareProcessorDto dto => dto.Order,
            HardwareGraphicsCardDto dto => dto.Order,
            HardwareMemoryDto dto => dto.Order,
            HardwareStorageDto dto => dto.Order,
            HardwareCoolerDto dto => dto.Order,
            HardwarePowerSupplyDto dto => dto.Order,
            _ => 0,
        };

        private static string GetId<T>(T item) => item switch
        {
            HardwareCaseDto dto => dto.Id,
            HardwareMotherboardDto dto => dto.Id,
            HardwareProcessorDto dto => dto.Id,
            HardwareGraphicsCardDto dto => dto.Id,
            HardwareMemoryDto dto => dto.Id,
            HardwareStorageDto dto => dto.Id,
            HardwareCoolerDto dto => dto.Id,
            HardwarePowerSupplyDto dto => dto.Id,
            _ => string.Empty,
        };

        private static BenchmarkAirflowDto[] AirflowDtos(BenchmarkDefinitionDto benchmark) =>
            (benchmark.Airflows ?? [])
                .Where(static item => item is not null)
                .OrderBy(static item => item.Order)
                .ThenBy(static item => item.Id, StringComparer.Ordinal)
                .ToArray();

        private static BenchmarkWorkloadDto[] WorkloadDtos(BenchmarkDefinitionDto benchmark) =>
            (benchmark.Workloads ?? [])
                .Where(static item => item is not null)
                .OrderBy(static item => item.Order)
                .ThenBy(static item => item.Id, StringComparer.Ordinal)
                .ToArray();

        private static int IndexOf<T>(T[] items, Func<T, string> id, string value, string label)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (string.Equals(id(items[i]), value, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            throw new InvalidDataException($"未知{label} id '{value}'。");
        }

        private static void Require(int count, string label)
        {
            if (count == 0)
            {
                throw new InvalidDataException($"内容缺少{label}定义。");
            }
        }
    }

    #region 规则目录 DTO（序列化给 JS 的完整业务数据）

    private sealed class RuleCatalog
    {
        public RuleBoard Board { get; init; } = new();
        public RuleCooler Cooler { get; init; } = new();
        public RuleCase[] Cases { get; init; } = [];
        public RuleMemory[] Ram { get; init; } = [];
        public RuleGraphics[] Graphics { get; init; } = [];
        public RuleLiquid Liquid { get; init; } = new();
        public RulePowerSupply[] PowerSupplies { get; init; } = [];
        public RuleLoadPoint[] LoadCurve { get; init; } = [];
        public RuleProcessor Processor { get; init; } = new();
        public RuleStorage Storage { get; init; } = new();
        public RuleBenchmark Benchmark { get; init; } = new();
        public int CpuMaxRate { get; init; }
        public BenchSetup Initial { get; init; } = new(0, 0, [], 0);
    }

    private sealed class RuleBoard
    {
        public string Name { get; init; } = "";
        public int[] Dimensions { get; init; } = [];
        public RuleSlot[] Slots { get; init; } = [];
        public int MaxRate { get; init; }
        public int Price { get; init; }
        public int VrmWatts { get; init; }
        public string Color { get; init; } = "";
        public string FormFactor { get; init; } = "";
    }

    private sealed class RuleSlot
    {
        public string Name { get; init; } = "";
        public string Channel { get; init; } = "";
        public int[] Anchor { get; init; } = [];
    }

    private sealed class RuleCooler
    {
        public string Name { get; init; } = "";

        [JsonPropertyName("body_aabb")]
        public HardwareVolumeDto[] BodyAabb { get; init; } = [];

        [JsonPropertyName("fan_aabb")]
        public HardwareVolumeDto? FanAabb { get; init; }

        [JsonPropertyName("adjustable_y_max")]
        public int AdjustableYMax { get; init; }
    }

    private sealed class RuleCase
    {
        public string Name { get; init; } = "";
        public int HeightLimit { get; init; }
        public int GpuLimit { get; init; }
        public int PsuLimit { get; init; }
        public float NaturalAirflow { get; init; }
        public int Price { get; init; }
        public string Color { get; init; } = "";
        public string FormFactor { get; init; } = "";
    }

    private sealed class RuleMemory
    {
        public string Name { get; init; } = "";
        public int[] Dimensions { get; init; } = [];
        public int Price { get; init; }
        public string Color { get; init; } = "";
        public string Lighting { get; init; } = "";
        public bool Rgb { get; init; }
        public int Gb { get; init; }
        public int Rate { get; init; }
    }

    private sealed class RuleGraphics
    {
        public string Name { get; init; } = "";
        public int[] Dimensions { get; init; } = [];
        public int FanCount { get; init; }
        public int Price { get; init; }
        public int Performance { get; init; }
        public float Resistance { get; init; }
        public int Throttle { get; init; }
        public int MaxFanDb { get; init; }
        public string Color { get; init; } = "";
        public string Lighting { get; init; } = "";
        public int[] TestWatts { get; init; } = [];
    }

    private sealed class RuleLiquid
    {
        public string Name { get; init; } = "";
        public int Price { get; init; }
        public float Resistance { get; init; }
        public int MaxFanDb { get; init; }
        public int PumpDb { get; init; }
        public string Color { get; init; } = "";
        public string Lighting { get; init; } = "";
        public int CoreThickness { get; init; }
        public int FanThickness { get; init; }
        public int FanSize { get; init; }
        public int FanCount { get; init; }
    }

    private sealed class RulePowerSupply
    {
        public string Name { get; init; } = "";
        public int Continuous { get; init; }
        public int Pulse { get; init; }
        public int Recommended { get; init; }
        public int Length { get; init; }
        public int Price { get; init; }
        public string Cable { get; init; } = "";
        public int NoiseDb { get; init; }
    }

    private sealed class RuleLoadPoint
    {
        public string Name { get; init; } = "";
        public int Cpu { get; init; }
        public int Other { get; init; }
        public bool Pulse { get; init; }
    }

    private sealed class RuleProcessor
    {
        public string Name { get; init; } = "";
        public int Price { get; init; }
        public int Performance { get; init; }
        public int Throttle { get; init; }
    }

    private sealed class RuleStorage
    {
        public string Name { get; init; } = "";
        public int Price { get; init; }
    }

    private sealed class RuleBenchmark
    {
        public float Ambient { get; init; }
        public float ReferenceBandwidth { get; init; }
        public RuleAirflow[] Airflows { get; init; } = [];
        public RuleWorkload[] Workloads { get; init; } = [];
    }

    private sealed class RuleAirflow
    {
        public string Name { get; init; } = "";
        public int Intake { get; init; }
        public int Exhaust { get; init; }
        public float? CaseNoise { get; init; }
    }

    private sealed class RuleWorkload
    {
        public string Name { get; init; } = "";
        public float[] ComputeWeights { get; init; } = [];
        public float[] Weights { get; init; } = [];
        public float ReferenceRoi { get; init; }
        public float TargetMargin { get; init; }
        public float TargetNoise { get; init; }
    }

    #endregion
}
