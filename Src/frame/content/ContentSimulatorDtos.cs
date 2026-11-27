using System.Text.Json.Serialization;

namespace KemoCard.Frame.Content.Simulator;

/// <summary>
/// 装机模拟器专用的内容定义。全部由 <see cref="ContentModLoader"/> 从
/// <c>content/hardware/**</c>、<c>content/commissions/**</c> 等目录逐文件加载，
/// 记录 id 一律由文件名注入（与既有内容类别一致）。
/// </summary>
public sealed class HardwareCaseDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("price")]
    public int Price { get; init; }

    [JsonPropertyName("heightLimit")]
    public int HeightLimit { get; init; }

    [JsonPropertyName("gpuLimit")]
    public int GpuLimit { get; init; }

    [JsonPropertyName("psuLimit")]
    public int PsuLimit { get; init; }

    [JsonPropertyName("psuAnchor")]
    public float[]? PsuAnchor { get; init; }

    [JsonPropertyName("naturalAirflow")]
    public float NaturalAirflow { get; init; }

    [JsonPropertyName("color")]
    public string Color { get; init; } = "";

    [JsonPropertyName("formFactor")]
    public string FormFactor { get; init; } = "";

    [JsonPropertyName("asset")]
    public string Asset { get; init; } = "";
}

public sealed class MotherboardSlotDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("channel")]
    public string Channel { get; init; } = "";

    [JsonPropertyName("anchor")]
    public int[] Anchor { get; init; } = [0, 0, 0];
}

public sealed class HardwareMotherboardDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("price")]
    public int Price { get; init; }

    [JsonPropertyName("dimensions")]
    public int[] Dimensions { get; init; } = [0, 0, 0];

    [JsonPropertyName("slots")]
    public MotherboardSlotDto[] Slots { get; init; } = [];

    [JsonPropertyName("processorAnchor")]
    public float[]? ProcessorAnchor { get; init; }

    [JsonPropertyName("storageAnchor")]
    public float[]? StorageAnchor { get; init; }

    [JsonPropertyName("maxRate")]
    public int MaxRate { get; init; }

    [JsonPropertyName("vrmWatts")]
    public int VrmWatts { get; init; }

    [JsonPropertyName("color")]
    public string Color { get; init; } = "";

    [JsonPropertyName("formFactor")]
    public string FormFactor { get; init; } = "";

    [JsonPropertyName("asset")]
    public string Asset { get; init; } = "";
}

public sealed class HardwareProcessorDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("price")]
    public int Price { get; init; }

    [JsonPropertyName("performance")]
    public int Performance { get; init; }

    [JsonPropertyName("throttle")]
    public int Throttle { get; init; }

    [JsonPropertyName("asset")]
    public string Asset { get; init; } = "";
}

public sealed class HardwareGraphicsCardDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("price")]
    public int Price { get; init; }

    [JsonPropertyName("dimensions")]
    public int[] Dimensions { get; init; } = [0, 0, 0];

    [JsonPropertyName("fanCount")]
    public int FanCount { get; init; }

    [JsonPropertyName("performance")]
    public int Performance { get; init; }

    [JsonPropertyName("resistance")]
    public float Resistance { get; init; }

    [JsonPropertyName("throttle")]
    public int Throttle { get; init; }

    [JsonPropertyName("maxFanDb")]
    public int MaxFanDb { get; init; }

    [JsonPropertyName("color")]
    public string Color { get; init; } = "";

    [JsonPropertyName("lighting")]
    public string Lighting { get; init; } = "";

    [JsonPropertyName("testWatts")]
    public int[] TestWatts { get; init; } = [];

    [JsonPropertyName("asset")]
    public string Asset { get; init; } = "";
}

public sealed class HardwareMemoryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("dimensions")]
    public int[] Dimensions { get; init; } = [0, 0, 0];

    [JsonPropertyName("price")]
    public int Price { get; init; }

    [JsonPropertyName("color")]
    public string Color { get; init; } = "";

    [JsonPropertyName("lighting")]
    public string Lighting { get; init; } = "";

    [JsonPropertyName("rgb")]
    public bool Rgb { get; init; }

    [JsonPropertyName("gb")]
    public int Gb { get; init; }

    [JsonPropertyName("rate")]
    public int Rate { get; init; }

    [JsonPropertyName("asset")]
    public string Asset { get; init; } = "";
}

public sealed class HardwareStorageDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("price")]
    public int Price { get; init; }

    [JsonPropertyName("asset")]
    public string Asset { get; init; } = "";
}

public sealed class HardwareVolumeDto
{
    [JsonPropertyName("origin")]
    public float[] Origin { get; init; } = [0, 0, 0];

    [JsonPropertyName("size")]
    public float[] Size { get; init; } = [0, 0, 0];
}

public sealed class HardwareCoolerDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary><c>air</c> 或 <c>liquid</c>。目录中的风冷 / 水冷记录靠它区分。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "air";

    [JsonPropertyName("price")]
    public int Price { get; init; }

    [JsonPropertyName("resistance")]
    public float Resistance { get; init; }

    [JsonPropertyName("maxFanDb")]
    public int MaxFanDb { get; init; }

    [JsonPropertyName("pumpDb")]
    public int PumpDb { get; init; }

    [JsonPropertyName("color")]
    public string Color { get; init; } = "";

    [JsonPropertyName("lighting")]
    public string Lighting { get; init; } = "";

    [JsonPropertyName("coreThickness")]
    public int CoreThickness { get; init; }

    [JsonPropertyName("fanThickness")]
    public int FanThickness { get; init; }

    [JsonPropertyName("fanSize")]
    public int FanSize { get; init; }

    [JsonPropertyName("fanCount")]
    public int FanCount { get; init; }

    [JsonPropertyName("body_aabb")]
    public HardwareVolumeDto[] BodyAabb { get; init; } = [];

    [JsonPropertyName("fan_aabb")]
    public HardwareVolumeDto? FanAabb { get; init; }

    [JsonPropertyName("adjustable_y_max")]
    public int AdjustableYMax { get; init; }

    [JsonPropertyName("asset")]
    public string Asset { get; init; } = "";

    [JsonPropertyName("fanAsset")]
    public string FanAsset { get; init; } = "";

    [JsonPropertyName("pumpAsset")]
    public string PumpAsset { get; init; } = "";
}

public sealed class HardwarePowerSupplyDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("continuous")]
    public int Continuous { get; init; }

    [JsonPropertyName("pulse")]
    public int Pulse { get; init; }

    [JsonPropertyName("recommended")]
    public int Recommended { get; init; }

    [JsonPropertyName("length")]
    public int Length { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    [JsonPropertyName("price")]
    public int Price { get; init; }

    [JsonPropertyName("cable")]
    public string Cable { get; init; } = "";

    [JsonPropertyName("noiseDb")]
    public int NoiseDb { get; init; }

    [JsonPropertyName("asset")]
    public string Asset { get; init; } = "";
}

public sealed class BenchSetupDto
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = "";

    [JsonPropertyName("ramId")]
    public string RamId { get; init; } = "";

    [JsonPropertyName("slots")]
    public int[] Slots { get; init; } = [];

    [JsonPropertyName("offset")]
    public int Offset { get; init; }

    [JsonPropertyName("front360")]
    public bool Front360 { get; init; }

    [JsonPropertyName("gpuId")]
    public string GpuId { get; init; } = "";

    [JsonPropertyName("airflowId")]
    public string AirflowId { get; init; } = "";

    [JsonPropertyName("workloadId")]
    public string WorkloadId { get; init; } = "";

    [JsonPropertyName("psuId")]
    public string PsuId { get; init; } = "";
}

public sealed class BenchmarkAirflowDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("intake")]
    public int Intake { get; init; }

    [JsonPropertyName("exhaust")]
    public int Exhaust { get; init; }

    [JsonPropertyName("caseNoise")]
    public float? CaseNoise { get; init; }
}

public sealed class BenchmarkWorkloadDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("computeWeights")]
    public float[] ComputeWeights { get; init; } = [];

    [JsonPropertyName("weights")]
    public float[] Weights { get; init; } = [];

    [JsonPropertyName("referenceRoi")]
    public float ReferenceRoi { get; init; }

    [JsonPropertyName("targetMargin")]
    public float TargetMargin { get; init; }

    [JsonPropertyName("targetNoise")]
    public float TargetNoise { get; init; }
}

public sealed class BenchmarkLoadCurveDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("cpu")]
    public int Cpu { get; init; }

    [JsonPropertyName("other")]
    public int Other { get; init; }

    [JsonPropertyName("pulse")]
    public bool Pulse { get; init; }
}

public sealed class BenchmarkDefinitionDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("ambient")]
    public float Ambient { get; init; }

    [JsonPropertyName("referenceBandwidth")]
    public float ReferenceBandwidth { get; init; }

    [JsonPropertyName("cpuMaxRate")]
    public int CpuMaxRate { get; init; }

    [JsonPropertyName("airflows")]
    public BenchmarkAirflowDto[] Airflows { get; init; } = [];

    [JsonPropertyName("workloads")]
    public BenchmarkWorkloadDto[] Workloads { get; init; } = [];

    [JsonPropertyName("loadCurve")]
    public BenchmarkLoadCurveDto[] LoadCurve { get; init; } = [];

    [JsonPropertyName("initial")]
    public BenchSetupDto Initial { get; init; } = new();
}

public sealed class CommissionDefinitionDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("customerMessage")]
    public string CustomerMessage { get; init; } = "";

    [JsonPropertyName("modeLabel")]
    public string ModeLabel { get; init; } = "";

    [JsonPropertyName("requiredGpuId")]
    public string RequiredGpuId { get; init; } = "";

    [JsonPropertyName("budget")]
    public int Budget { get; init; }

    [JsonPropertyName("buildFee")]
    public int BuildFee { get; init; }

    [JsonPropertyName("minimumMemoryGb")]
    public int MinimumMemoryGb { get; init; }

    [JsonPropertyName("workloadId")]
    public string WorkloadId { get; init; } = "";

    [JsonPropertyName("requiresFront360")]
    public bool RequiresFront360 { get; init; } = true;
}

public sealed class DiagnosisCaseDefinitionDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; init; } = "";

    [JsonPropertyName("customerBrief")]
    public string CustomerBrief { get; init; } = "";

    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = "";

    [JsonPropertyName("setup")]
    public BenchSetupDto Setup { get; init; } = new();

    [JsonPropertyName("faultyPsuId")]
    public string FaultyPsuId { get; init; } = "";

    [JsonPropertyName("replacementAllowance")]
    public int ReplacementAllowance { get; init; }
}

public sealed class GameModeDefinitionDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    /// <summary>与 <c>ComputerSimulator.Modules.Menu.GameMode</c> 的枚举名一致（大小写不敏感）。</summary>
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "";

    [JsonPropertyName("index")]
    public string Index { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("code")]
    public string Code { get; init; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "";

    [JsonPropertyName("available")]
    public bool Available { get; init; }
}

public sealed class ChallengeDefinitionDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("crate")]
    public bool Crate { get; init; }

    [JsonPropertyName("caseIds")]
    public string[] CaseIds { get; init; } = [];

    [JsonPropertyName("gpuIds")]
    public string[] GpuIds { get; init; } = [];

    [JsonPropertyName("psuIds")]
    public string[] PsuIds { get; init; } = [];

    [JsonPropertyName("initialCaseId")]
    public string InitialCaseId { get; init; } = "";

    [JsonPropertyName("initialGpuId")]
    public string InitialGpuId { get; init; } = "";

    [JsonPropertyName("initialPsuId")]
    public string InitialPsuId { get; init; } = "";

    [JsonPropertyName("fixed")]
    public BenchSetupDto Fixed { get; init; } = new();

    [JsonPropertyName("submitWorkloadId")]
    public string SubmitWorkloadId { get; init; } = "";
}
