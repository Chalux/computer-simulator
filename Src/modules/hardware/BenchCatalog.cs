namespace ComputerSimulator.Modules.Hardware;

public sealed record BenchSetup(int CaseIndex, int RamIndex, int[] Slots, int Offset, bool Front360 = false, int GpuIndex = 0, int AirflowIndex = 0, int WorkloadIndex = 0, int PsuIndex = 0);
public sealed record SlotInfo(string Name, string Channel);
public sealed record BoardInfo(string Name, SlotInfo[] Slots);
public sealed record CoolerInfo(string Name, int Adjustable_y_max);
public sealed record CaseInfo(string Name, int HeightLimit, int GpuLimit, int PsuLimit);
public sealed record MemoryInfo(string Name, int[] Dimensions);
public sealed record CardInfo(string Name, int[] Dimensions);
public sealed record PsuInfo(string Name, int Continuous, int Pulse, int Length, string Cable, int Recommended = 0);
public sealed record LiquidInfo(string Name, int CoreThickness, int FanThickness);
public sealed record TestPreset(string Name);
public sealed record BenchmarkOptions(TestPreset[] Airflows, TestPreset[] Workloads);
public sealed record BenchCatalog(BoardInfo Board, CoolerInfo Cooler, CaseInfo[] Cases, MemoryInfo[] Ram, CardInfo[] Graphics, LiquidInfo Liquid, PsuInfo[] PowerSupplies, BenchmarkOptions Benchmark, BenchSetup Initial);
