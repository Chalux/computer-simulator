using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Simulation;

namespace ComputerSimulator.Modules.Commission;

/// <summary>交付前逐项验收；<see cref="CanDeliver"/> 仅在全部明示条件与实测结果都通过时为真。</summary>
public sealed record CommissionChecklist(int PartsCost, bool WithinBudget, bool GpuMet, bool MemoryMet, bool LoadMet)
{
    public bool CanDeliver => WithinBudget && GpuMet && MemoryMet && LoadMet;
}

/// <summary>
/// 01-01 交付回执。保存原订单条款、实际装机（含插槽下标）、可读配件名与完整负载测试，
/// 供再次进入委托时复核；配件价格口径与 benchmark 规则完全一致。
/// </summary>
public sealed record CommissionReceipt(
    string Id,
    string Title,
    string RequiredGpu,
    int Budget,
    int BuildFee,
    int MinimumMemoryGb,
    int WorkloadIndex,
    string WorkloadName,
    BenchSetup Setup,
    int PartsCost,
    int CustomerTotal,
    string GpuName,
    int MemoryGb,
    string BoardName,
    string CoolerName,
    string CaseName,
    string PowerSupplyName,
    string MemoryName,
    string[] MemorySlotNames,
    string ProcessorName,
    string StorageName,
    string AssemblyStatus,
    LoadResult LoadResult,
    BenchmarkReport Report,
    string DeliveredAtUtc);

/// <summary>
/// 委托验收规则。订单条款由 <see cref="CommissionContentDefinition"/> 提供（内容 Mod 加载），
/// 本类只描述计算方式，不持有状态、不读写文件；状态由 <see cref="CommissionMod"/> 保存，
/// 流程由 <see cref="CommissionController"/> 编排。
/// </summary>
public static class CommissionOrder
{
    /// <summary>
    /// 计算验收清单，不修改任何状态。预算只约束配件账单，<see cref="CommissionContentDefinition.BuildFee"/>
    /// 另行计入客户实付。负载项要求：前置 360、本委托指定工况、无装配/负载 BLOCKED、存在对应工况的完整报告。
    /// </summary>
    public static CommissionChecklist Evaluate(
        CommissionContentDefinition order,
        BenchCatalog catalog,
        BenchSetup setup,
        FitResult fit,
        LoadResult? currentTest)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(fit);

        int partsCost = CalculatePartsCost(catalog, setup);
        bool withinBudget = partsCost <= order.Budget;

        bool gpuMet = setup.GpuIndex == order.RequiredGpuIndex;
        bool memoryMet = fit.Capacity >= order.MinimumMemoryGb;

        TestPreset? workload = At(catalog.Benchmark?.Workloads, order.WorkloadIndex);
        bool loadMet = (!order.RequiresFront360 || setup.Front360)
            && setup.WorkloadIndex == order.WorkloadIndex
            && !IsBlocked(fit.Status)
            && currentTest is { Report: not null } test
            && !IsBlocked(test.Status)
            && workload is not null
            && test.Report.Price == partsCost
            && string.Equals(test.Report.Workload, workload.Name, StringComparison.Ordinal);

        return new CommissionChecklist(partsCost, withinBudget, gpuMet, memoryMet, loadMet);
    }

    /// <summary>
    /// 条件全部满足时按当前配置生成回执对象；不写文件、不检查既有回执。
    /// 持久化与不覆盖语义由 <see cref="CommissionController"/> 通过内部回执仓库完成。
    /// </summary>
    internal static CommissionReceipt CreateReceipt(
        CommissionContentDefinition order,
        BenchCatalog catalog,
        BenchSetup setup,
        FitResult fit,
        LoadResult currentTest)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(fit);
        ArgumentNullException.ThrowIfNull(currentTest);

        CommissionChecklist checklist = Evaluate(order, catalog, setup, fit, currentTest);
        if (!checklist.CanDeliver)
            throw new InvalidOperationException("委托验收条件尚未全部满足，不能交付。");
        if (currentTest.Report is not { } report)
            throw new InvalidOperationException("缺少完整的负载测试报告，不能交付。");

        CardInfo? gpu = At(catalog.Graphics, setup.GpuIndex);
        CaseInfo? chassis = At(catalog.Cases, setup.CaseIndex);
        MemoryInfo? memory = At(catalog.Ram, setup.RamIndex);
        PsuInfo? psu = At(catalog.PowerSupplies, setup.PsuIndex);
        SlotInfo[] slots = catalog.Board?.Slots ?? [];
        int[] installedSlots = setup.Slots ?? [];
        string[] slotNames = installedSlots
            .Select(index => index >= 0 && index < slots.Length ? slots[index].Name : $"#{index}")
            .ToArray();

        return new CommissionReceipt(
            order.Id,
            order.Title,
            order.RequiredGpuName,
            order.Budget,
            order.BuildFee,
            order.MinimumMemoryGb,
            order.WorkloadIndex,
            At(catalog.Benchmark?.Workloads, order.WorkloadIndex)?.Name ?? string.Empty,
            setup,
            checklist.PartsCost,
            checklist.PartsCost + order.BuildFee,
            gpu?.Name ?? string.Empty,
            fit.Capacity,
            catalog.Board?.Name ?? string.Empty,
            catalog.Liquid?.Name ?? string.Empty,
            chassis?.Name ?? string.Empty,
            psu?.Name ?? string.Empty,
            memory?.Name ?? string.Empty,
            slotNames,
            catalog.Processor?.Name ?? string.Empty,
            catalog.Storage?.Name ?? string.Empty,
            fit.Status,
            currentTest,
            report,
            DateTimeOffset.UtcNow.ToString("O"));
    }

    /// <summary>
    /// 与 benchmark 规则一致的配件账单：
    /// 处理器 + 存储 + 主板 + 机箱 + 前置 360 + 显卡 + 电源 + 每根已装内存。
    /// 装机费不计入配件预算；价格全部来自内容 Mod 加载后的目录。
    /// </summary>
    private static int CalculatePartsCost(BenchCatalog catalog, BenchSetup setup)
    {
        ProcessorInfo processor = catalog.Processor
            ?? throw new InvalidOperationException("硬件目录缺少处理器参考价。");
        StorageInfo storage = catalog.Storage
            ?? throw new InvalidOperationException("硬件目录缺少存储参考价。");

        int cost = processor.Price + storage.Price;
        cost += catalog.Board?.Price ?? 0;
        cost += PriceAt(catalog.Cases, setup.CaseIndex, part => part.Price);
        cost += catalog.Liquid?.Price ?? 0;
        cost += PriceAt(catalog.Graphics, setup.GpuIndex, part => part.Price);
        cost += PriceAt(catalog.PowerSupplies, setup.PsuIndex, part => part.Price);
        cost += (setup.Slots?.Length ?? 0) * PriceAt(catalog.Ram, setup.RamIndex, part => part.Price);
        return cost;
    }

    private static int PriceAt<T>(T[]? items, int index, Func<T, int> price) where T : class
        => At(items, index) is { } item ? price(item) : 0;

    private static T? At<T>(T[]? items, int index) where T : class
        => items is not null && index >= 0 && index < items.Length ? items[index] : null;

    private static bool IsBlocked(string? status)
        => string.Equals(status, "BLOCKED", StringComparison.Ordinal);
}
