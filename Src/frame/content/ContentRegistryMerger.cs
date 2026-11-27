namespace KemoCard.Frame.Content;

public sealed class ContentRegistryMerger
{
    public ContentRegistryMergeResult Merge(
        IReadOnlyList<ModContentBundle> bundles,
        GameDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(bundles);
        ArgumentNullException.ThrowIfNull(store);

        var conflicts = new List<ContentIdConflictEntry>();
        var ownerById = new Dictionary<(EContentCategory Category, string Id), string>();

        store.Clear();
        MergeInto(bundles, store, ownerById, conflicts, excludedModIds: null);

        return new ContentRegistryMergeResult(conflicts, ownerById);
    }

    /// <summary>
    /// 把 bundles 合并进现有 store（不清空）。<paramref name="excludedModIds"/> 用于内容准入阶段的
    /// 回退：某个 Mod 的同名定义被判非法并移除后，再次合并时跳过该 Mod 的这条定义，
    /// 让低优先级的同名定义（通常是 basemod）重新生效。
    /// </summary>
    internal void MergeInto(
        IReadOnlyList<ModContentBundle> bundles,
        GameDefinitionStore store,
        Dictionary<(EContentCategory Category, string Id), string> ownerById,
        List<ContentIdConflictEntry>? conflicts,
        IReadOnlyDictionary<(EContentCategory Category, string Id), HashSet<string>>? excludedModIds)
    {
        ArgumentNullException.ThrowIfNull(bundles);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(ownerById);

        foreach (var bundle in bundles)
        {
            MergeBundle(bundle, store, ownerById, conflicts, excludedModIds);
        }
    }

    private static void MergeBundle(
        ModContentBundle bundle,
        GameDefinitionStore store,
        Dictionary<(EContentCategory Category, string Id), string> ownerById,
        List<ContentIdConflictEntry>? conflicts,
        IReadOnlyDictionary<(EContentCategory Category, string Id), HashSet<string>>? excludedModIds)
    {
        var definitions = bundle.Definitions;
        TryAddAll(bundle.ModId, EContentCategory.Character, definitions.Characters, store.CharactersMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.Enemy, definitions.Enemies, store.EnemiesMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.Battle, definitions.Battles, store.BattlesMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.Event, definitions.Events, store.EventsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.Item, definitions.Items, store.ItemsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.Card, definitions.Cards, store.CardsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.Skill, definitions.Skills, store.SkillsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.Buff, definitions.Buffs, store.BuffsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.Effect, definitions.Effects, store.EffectsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.Attribute, definitions.Attributes, store.AttributesMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.GameplayEffect, definitions.GameplayEffects, store.GameplayEffectsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.GameplayTag, definitions.GameplayTags, store.GameplayTagsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SkillAction, definitions.SkillActions, store.SkillActionsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.Story, definitions.Stories, store.StoriesMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.OrbType, definitions.OrbTypes, store.OrbTypesMutable, ownerById, conflicts, excludedModIds);

        TryAddAll(bundle.ModId, EContentCategory.SimCase, definitions.Cases, store.CasesMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimMotherboard, definitions.Motherboards, store.MotherboardsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimProcessor, definitions.Processors, store.ProcessorsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimGraphicsCard, definitions.GraphicsCards, store.GraphicsCardsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimMemory, definitions.MemoryModules, store.MemoryModulesMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimStorage, definitions.StorageDevices, store.StorageDevicesMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimCooler, definitions.Coolers, store.CoolersMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimPowerSupply, definitions.PowerSupplies, store.PowerSuppliesMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimCommission, definitions.Commissions, store.CommissionsMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimDiagnosisCase, definitions.DiagnosisCases, store.DiagnosisCasesMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimBenchmark, definitions.Benchmarks, store.BenchmarksMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimGameMode, definitions.GameModes, store.GameModesMutable, ownerById, conflicts, excludedModIds);
        TryAddAll(bundle.ModId, EContentCategory.SimChallenge, definitions.Challenges, store.ChallengesMutable, ownerById, conflicts, excludedModIds);
    }

    private static void TryAddAll<T>(
        string modId,
        EContentCategory category,
        IReadOnlyDictionary<string, T> source,
        Dictionary<string, T> target,
        Dictionary<(EContentCategory Category, string Id), string> ownerById,
        List<ContentIdConflictEntry>? conflicts,
        IReadOnlyDictionary<(EContentCategory Category, string Id), HashSet<string>>? excludedModIds)
    {
        foreach (var (id, dto) in source)
        {
            var key = (category, id);
            if (excludedModIds is not null &&
                excludedModIds.TryGetValue(key, out var excluded) &&
                excluded.Contains(modId))
            {
                continue;
            }

            if (target.TryAdd(id, dto))
            {
                ownerById[key] = modId;
                continue;
            }

            // 回退合并只补齐被移除的 id，已有的冲突在首次合并时已经记录过。
            if (conflicts is not null && ownerById.TryGetValue(key, out var winnerModId))
            {
                conflicts.Add(new ContentIdConflictEntry(category, id, winnerModId, modId));
            }
        }
    }
}
