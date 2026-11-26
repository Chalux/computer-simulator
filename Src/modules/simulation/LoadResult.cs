namespace ComputerSimulator.Modules.Simulation;

public sealed record GradedScore(float Score, string Grade);
public sealed record ChipReading(float Temperature, int FanPercent, float LossPercent, bool Throttled);
public sealed record BenchmarkReport(GradedScore[] Axes, GradedScore Total, string Workload, string Airflow,
    float Ambient, float Noise, int Price, ChipReading Cpu, ChipReading Gpu);

public sealed record LoadSample(string Name, bool Pulse, int Watts);
public sealed record LoadResult(string Status, int Sustained, int Peak, string Message, BenchmarkReport? Report,
    LoadSample[]? Samples = null);
