namespace KemoCard.Frame.Content;

public static class ContentCategoryPaths
{
    public static string Folder(EContentCategory category) => category switch
    {
        EContentCategory.Character => "characters",
        EContentCategory.Enemy => "enemies",
        EContentCategory.Battle => "battles",
        EContentCategory.Event => "events",
        EContentCategory.Card => "cards",
        EContentCategory.Item => "items",
        EContentCategory.Skill => "skills",
        EContentCategory.Buff => "buffs",
        EContentCategory.Effect => "effects",
        EContentCategory.SkillAction => "skill_actions",
        EContentCategory.Attribute => "attributes",
        EContentCategory.GameplayEffect => "gameplay_effects",
        EContentCategory.GameplayTag => "tags",
        EContentCategory.Story => "stories",
        EContentCategory.OrbType => "orbs",

        // 硬件按大类分目录；路径使用 '/'，由 ContentModPath.Combine 保持资源路径格式。
        EContentCategory.SimCase => "hardware/cases",
        EContentCategory.SimMotherboard => "hardware/motherboards",
        EContentCategory.SimProcessor => "hardware/processors",
        EContentCategory.SimGraphicsCard => "hardware/graphics_cards",
        EContentCategory.SimMemory => "hardware/memory",
        EContentCategory.SimStorage => "hardware/storage",
        EContentCategory.SimCooler => "hardware/coolers",
        EContentCategory.SimPowerSupply => "hardware/power_supplies",
        EContentCategory.SimCommission => "commissions",
        EContentCategory.SimDiagnosisCase => "diagnosis_cases",
        EContentCategory.SimBenchmark => "benchmarks",
        EContentCategory.SimGameMode => "game_modes",
        EContentCategory.SimChallenge => "challenges",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}
