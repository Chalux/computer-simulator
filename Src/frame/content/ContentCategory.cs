namespace KemoCard.Frame.Content;

public enum EContentCategory
{
    Character,
    Enemy,
    Battle,
    Event,
    Card,
    Item,
    Skill,
    Buff,
    Effect,
    SkillAction,
    Attribute,
    GameplayEffect,
    GameplayTag,
    Story,
    /// <summary>充能球类型（内建 6 种 + Mod 特殊球注册）。</summary>
    OrbType,

    // 装机模拟器类别：追加在既有类别之后，保留旧枚举数值。
    SimCase,
    SimMotherboard,
    SimProcessor,
    SimGraphicsCard,
    SimMemory,
    SimStorage,
    SimCooler,
    SimPowerSupply,
    SimCommission,
    SimDiagnosisCase,
    SimBenchmark,
    SimGameMode,
    SimChallenge,
}
