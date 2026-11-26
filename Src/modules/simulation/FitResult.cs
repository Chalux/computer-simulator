using ComputerSimulator.Modules.Hardware;

namespace ComputerSimulator.Modules.Simulation;

public sealed record FitIssue(string Status, string Message, string[] Parts, Volume? Overlap, float? Excess);
public sealed record FitResult(BenchPart[] Parts, FitIssue[] Issues, string Status, int HeightLimit,
    int Headroom, int Channels, int Capacity, float Bandwidth, int? GpuLimit, int? GpuMargin,
    int? PsuLimit = null, int? PsuMargin = null);
