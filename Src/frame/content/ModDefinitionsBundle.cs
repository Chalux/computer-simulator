using KemoCard.Frame.Content.Definitions;
using KemoCard.Frame.Content.Simulator;

namespace KemoCard.Frame.Content;

public sealed record ModDefinitionsBundle(
    IReadOnlyDictionary<string, CharacterDto> Characters,
    IReadOnlyDictionary<string, EnemyDto> Enemies,
    IReadOnlyDictionary<string, BattleDto> Battles,
    IReadOnlyDictionary<string, EventDto> Events,
    IReadOnlyDictionary<string, ItemDto> Items,
    IReadOnlyDictionary<string, CardDto> Cards,
    IReadOnlyDictionary<string, SkillDto> Skills,
    IReadOnlyDictionary<string, BuffDto> Buffs,
    IReadOnlyDictionary<string, EffectDto> Effects,
    IReadOnlyDictionary<string, AttributeDefDto> Attributes,
    IReadOnlyDictionary<string, GameplayEffectDefDto> GameplayEffects,
    IReadOnlyDictionary<string, GameplayTagDefDto> GameplayTags,
    IReadOnlyDictionary<string, SkillActionDto> SkillActions,
    IReadOnlyDictionary<string, StoryDto> Stories,
    /// <summary>充能球类型（内建 6 种由 base-game 声明；Mod 在此注册特殊球）。</summary>
    IReadOnlyDictionary<string, OrbTypeDto> OrbTypes,
    IReadOnlyDictionary<string, HardwareCaseDto> Cases,
    IReadOnlyDictionary<string, HardwareMotherboardDto> Motherboards,
    IReadOnlyDictionary<string, HardwareProcessorDto> Processors,
    IReadOnlyDictionary<string, HardwareGraphicsCardDto> GraphicsCards,
    IReadOnlyDictionary<string, HardwareMemoryDto> MemoryModules,
    IReadOnlyDictionary<string, HardwareStorageDto> StorageDevices,
    IReadOnlyDictionary<string, HardwareCoolerDto> Coolers,
    IReadOnlyDictionary<string, HardwarePowerSupplyDto> PowerSupplies,
    IReadOnlyDictionary<string, CommissionDefinitionDto> Commissions,
    IReadOnlyDictionary<string, DiagnosisCaseDefinitionDto> DiagnosisCases,
    IReadOnlyDictionary<string, BenchmarkDefinitionDto> Benchmarks,
    IReadOnlyDictionary<string, GameModeDefinitionDto> GameModes,
    IReadOnlyDictionary<string, ChallengeDefinitionDto> Challenges)
{
    public static ModDefinitionsBundle Empty { get; } = new(
        new Dictionary<string, CharacterDto>(StringComparer.Ordinal),
        new Dictionary<string, EnemyDto>(StringComparer.Ordinal),
        new Dictionary<string, BattleDto>(StringComparer.Ordinal),
        new Dictionary<string, EventDto>(StringComparer.Ordinal),
        new Dictionary<string, ItemDto>(StringComparer.Ordinal),
        new Dictionary<string, CardDto>(StringComparer.Ordinal),
        new Dictionary<string, SkillDto>(StringComparer.Ordinal),
        new Dictionary<string, BuffDto>(StringComparer.Ordinal),
        new Dictionary<string, EffectDto>(StringComparer.Ordinal),
        new Dictionary<string, AttributeDefDto>(StringComparer.Ordinal),
        new Dictionary<string, GameplayEffectDefDto>(StringComparer.Ordinal),
        new Dictionary<string, GameplayTagDefDto>(StringComparer.Ordinal),
        new Dictionary<string, SkillActionDto>(StringComparer.Ordinal),
        new Dictionary<string, StoryDto>(StringComparer.Ordinal),
        new Dictionary<string, OrbTypeDto>(StringComparer.Ordinal),
        EmptySim<HardwareCaseDto>(),
        EmptySim<HardwareMotherboardDto>(),
        EmptySim<HardwareProcessorDto>(),
        EmptySim<HardwareGraphicsCardDto>(),
        EmptySim<HardwareMemoryDto>(),
        EmptySim<HardwareStorageDto>(),
        EmptySim<HardwareCoolerDto>(),
        EmptySim<HardwarePowerSupplyDto>(),
        EmptySim<CommissionDefinitionDto>(),
        EmptySim<DiagnosisCaseDefinitionDto>(),
        EmptySim<BenchmarkDefinitionDto>(),
        EmptySim<GameModeDefinitionDto>(),
        EmptySim<ChallengeDefinitionDto>());

    private static Dictionary<string, T> EmptySim<T>() => new(StringComparer.Ordinal);
}
