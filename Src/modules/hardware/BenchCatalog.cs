using Godot;
using KemoCard.Frame.Content;

namespace ComputerSimulator.Modules.Hardware;

public sealed record BenchSetup(int CaseIndex, int RamIndex, int[] Slots, int Offset, bool Front360 = false, int GpuIndex = 0, int AirflowIndex = 0, int WorkloadIndex = 0, int PsuIndex = 0);
public sealed record SlotInfo(string Name, string Channel);
public sealed record BoardInfo(string Name, SlotInfo[] Slots, int Price = 0,
    float[]? ProcessorAnchor = null, float[]? StorageAnchor = null);
public sealed record CoolerInfo(string Name, int Adjustable_y_max);
public sealed record CaseInfo(string Name, int HeightLimit, int GpuLimit, int PsuLimit, int Price = 0,
    float[]? PsuAnchor = null);
public sealed record MemoryInfo(string Name, int[] Dimensions, int Price = 0);
public sealed record CardInfo(string Name, int[] Dimensions, int Price = 0);
public sealed record PsuInfo(string Name, int Continuous, int Pulse, int Length, string Cable,
    int Recommended = 0, int Price = 0, int Width = 0, int Height = 0);
public sealed record LiquidInfo(string Name, int CoreThickness, int FanThickness, int Price = 0);
public sealed record TestPreset(string Name);
public sealed record BenchmarkOptions(TestPreset[] Airflows, TestPreset[] Workloads);
public sealed record ProcessorInfo(string Name, int Price = 0);
public sealed record StorageInfo(string Name, int Price = 0);

/// <summary>一条硬件资产 URI 及其所属 Mod 根目录；相对路径由内容定义提供。</summary>
internal sealed record HardwareAssetRef(string ModId, string ModFolder, string RelativePath);

/// <summary>
/// 数据驱动的装配硬件目录。它是视图与规则共用的只读查询面：
/// <list type="bullet">
/// <item>属性（<see cref="Board"/>、<see cref="Cases"/>……）保持既有类型，视图无需改签名；</item>
/// <item><see cref="RuleCatalogJson"/> 是由加载内容重新生成的完整规则目录 JSON，注入规则脚本；</item>
/// <item><see cref="GetModelPath"/> 按角色把当前 <see cref="BenchSetup"/> 解析为模型资产路径。</item>
/// </list>
/// 没有静态目录兜底：未加载内容时构造器直接抛出。
/// </summary>
public sealed class BenchCatalog
{
    private readonly HardwareAssetRef?[] _caseAssets;
    private readonly HardwareAssetRef?[] _gpuAssets;
    private readonly HardwareAssetRef?[] _ramAssets;
    private readonly HardwareAssetRef?[] _psuAssets;
    private readonly HardwareAssetRef? _boardAsset;
    private readonly HardwareAssetRef? _processorAsset;
    private readonly HardwareAssetRef? _storageAsset;
    private readonly HardwareAssetRef? _airTowerAsset;
    private readonly HardwareAssetRef? _airFanAsset;
    private readonly HardwareAssetRef? _liquidRadiatorAsset;
    private readonly HardwareAssetRef? _liquidPumpAsset;

    internal BenchCatalog(
        BoardInfo board,
        CoolerInfo cooler,
        CaseInfo[] cases,
        MemoryInfo[] ram,
        CardInfo[] graphics,
        LiquidInfo liquid,
        PsuInfo[] powerSupplies,
        BenchmarkOptions benchmark,
        BenchSetup initial,
        ProcessorInfo? processor,
        StorageInfo? storage,
        string ruleCatalogJson,
        HardwareAssetRef?[] caseAssets,
        HardwareAssetRef?[] gpuAssets,
        HardwareAssetRef?[] ramAssets,
        HardwareAssetRef?[] psuAssets,
        HardwareAssetRef? boardAsset,
        HardwareAssetRef? processorAsset,
        HardwareAssetRef? storageAsset,
        HardwareAssetRef? airTowerAsset,
        HardwareAssetRef? airFanAsset,
        HardwareAssetRef? liquidRadiatorAsset,
        HardwareAssetRef? liquidPumpAsset)
    {
        Board = board;
        Cooler = cooler;
        Cases = cases;
        Ram = ram;
        Graphics = graphics;
        Liquid = liquid;
        PowerSupplies = powerSupplies;
        Benchmark = benchmark;
        Initial = initial;
        Processor = processor;
        Storage = storage;
        RuleCatalogJson = ruleCatalogJson;
        _caseAssets = caseAssets;
        _gpuAssets = gpuAssets;
        _ramAssets = ramAssets;
        _psuAssets = psuAssets;
        _boardAsset = boardAsset;
        _processorAsset = processorAsset;
        _storageAsset = storageAsset;
        _airTowerAsset = airTowerAsset;
        _airFanAsset = airFanAsset;
        _liquidRadiatorAsset = liquidRadiatorAsset;
        _liquidPumpAsset = liquidPumpAsset;
    }

    public BoardInfo Board { get; }

    public CoolerInfo Cooler { get; }

    public CaseInfo[] Cases { get; }

    public MemoryInfo[] Ram { get; }

    public CardInfo[] Graphics { get; }

    public LiquidInfo Liquid { get; }

    public PsuInfo[] PowerSupplies { get; }

    public BenchmarkOptions Benchmark { get; }

    public BenchSetup Initial { get; }

    public ProcessorInfo? Processor { get; }

    public StorageInfo? Storage { get; }

    /// <summary>完整规则目录 DTO 的 JSON；内容变化后仅在重新加载时重建。</summary>
    public string RuleCatalogJson { get; }

    /// <summary>视图查询面（保留独立成员名，便于 UI 只依赖目录而不接触加载器）。</summary>
    public BenchCatalog Catalog => this;

    /// <summary>
    /// 解析当前装配在指定角色上的模型资产。
    /// 返回 Godot <c>res://</c> 路径（随包 Mod）或安全的文件系统路径（<c>user://</c> / 绝对路径 Mod）。
    /// <c>slot</c> 等烘进主板的角色返回 <c>null</c>；未知角色或索引越界同样返回 <c>null</c>。
    /// </summary>
    public string? GetModelPath(BenchSetup setup, string role)
    {
        ArgumentNullException.ThrowIfNull(setup);

        return role switch
        {
            // 兼容既有视口的 "case"；内容契约占位名是 "panel"。
            "panel" or "case" => Resolve(At(_caseAssets, setup.CaseIndex)),
            "board" => Resolve(_boardAsset),
            "cpu" or "processor" => Resolve(_processorAsset),
            "gpu" => Resolve(At(_gpuAssets, setup.GpuIndex)),
            "ram" => Resolve(At(_ramAssets, setup.RamIndex)),
            "storage" => Resolve(_storageAsset),
            "tower" => Resolve(_airTowerAsset),
            "fan" => Resolve(_airFanAsset),
            "radiator" => Resolve(_liquidRadiatorAsset),
            "pump" => Resolve(_liquidPumpAsset),
            "psu" => Resolve(At(_psuAssets, setup.PsuIndex)),
            // 插槽几何烘进主板模型，不需要独立资产。
            "slot" => null,
            _ => null,
        };
    }

    private static HardwareAssetRef? At(HardwareAssetRef?[] assets, int index) =>
        index >= 0 && index < assets.Length ? assets[index] : null;

    private static string? Resolve(HardwareAssetRef? asset)
    {
        if (asset is null || string.IsNullOrWhiteSpace(asset.RelativePath))
        {
            return null;
        }

        var relative = asset.RelativePath.Replace('\\', '/').TrimStart('/');
        if (relative.Length == 0 || Path.IsPathRooted(relative) ||
            relative.Split('/').Any(static segment => segment is ".." or "."))
        {
            return null;
        }

        var combined = ContentModPath.Combine(asset.ModFolder, relative);
        if (asset.ModFolder.StartsWith("res://", StringComparison.Ordinal))
        {
            return combined;
        }

        if (asset.ModFolder.StartsWith("user://", StringComparison.Ordinal))
        {
            return ProjectSettings.GlobalizePath(combined);
        }

        return Path.GetFullPath(combined);
    }
}
