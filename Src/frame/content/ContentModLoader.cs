using System.Text.Json;
using KemoCard.Frame.Content.Definitions;
using KemoCard.Frame.Content.Simulator;

namespace KemoCard.Frame.Content;

public sealed class ContentModLoader
{
    /// <summary>
    /// 预解析（重写注入 id）阶段使用的宽松选项，必须与 <see cref="ContentDefinitionJson.Options"/> 保持一致：
    /// <see cref="JsonDocument.Parse(string)"/> 不继承 <c>JsonSerializerOptions</c>，
    /// 不显式传入会让允许尾逗号 / 跳过注释的配置形同虚设。
    /// </summary>
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static ModContentBundle Load(DiscoveredModEntry entry) =>
        Load(entry, SystemContentModFileSystem.Instance);

    public static ModContentBundle Load(DiscoveredModEntry entry, IContentModFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(fileSystem);

        var manifest = entry.Manifest;
        var contentRoot = ContentModPath.Combine(entry.FolderPath, manifest.ContentRoot);
        try
        {
            var definitions = new ModDefinitionsBundle(
                LoadDefinitions<CharacterDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Character)),
                LoadDefinitions<EnemyDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Enemy)),
                LoadDefinitions<BattleDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Battle)),
                LoadDefinitions<EventDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Event)),
                LoadDefinitions<ItemDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Item)),
                LoadDefinitions<CardDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Card)),
                LoadDefinitions<SkillDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Skill)),
                LoadDefinitions<BuffDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Buff)),
                LoadDefinitions<EffectDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Effect)),
                LoadDefinitions<AttributeDefDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Attribute)),
                LoadDefinitions<GameplayEffectDefDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.GameplayEffect)),
                LoadDefinitions<GameplayTagDefDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.GameplayTag)),
                LoadDefinitions<SkillActionDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SkillAction)),
                LoadDefinitions<StoryDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.Story)),
                LoadDefinitions<OrbTypeDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.OrbType)),
                LoadDefinitions<HardwareCaseDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimCase)),
                LoadDefinitions<HardwareMotherboardDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimMotherboard)),
                LoadDefinitions<HardwareProcessorDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimProcessor)),
                LoadDefinitions<HardwareGraphicsCardDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimGraphicsCard)),
                LoadDefinitions<HardwareMemoryDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimMemory)),
                LoadDefinitions<HardwareStorageDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimStorage)),
                LoadDefinitions<HardwareCoolerDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimCooler)),
                LoadDefinitions<HardwarePowerSupplyDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimPowerSupply)),
                LoadDefinitions<CommissionDefinitionDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimCommission)),
                LoadDefinitions<DiagnosisCaseDefinitionDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimDiagnosisCase)),
                LoadDefinitions<BenchmarkDefinitionDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimBenchmark)),
                LoadDefinitions<GameModeDefinitionDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimGameMode)),
                LoadDefinitions<ChallengeDefinitionDto>(fileSystem, contentRoot, ContentCategoryPaths.Folder(EContentCategory.SimChallenge)));

            return new ModContentBundle(manifest.ModId, definitions);
        }
        catch (Exception ex)
        {
            throw new ContentModLoadException(manifest.ModId, ex.Message, ex);
        }
    }

    private static Dictionary<string, T> LoadDefinitions<T>(
        IContentModFileSystem fileSystem,
        string contentRoot,
        string folderName)
    {
        var dir = ContentModPath.Combine(contentRoot, folderName);
        if (!fileSystem.DirectoryExists(dir))
        {
            return new Dictionary<string, T>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var filePath in fileSystem
                     .EnumerateFiles(dir)
                     .Where(static path => string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            var id = ContentModPath.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            result[id] = LoadFile<T>(fileSystem, filePath, id);
        }

        return result;
    }

    private static T LoadFile<T>(IContentModFileSystem fileSystem, string filePath, string id)
    {
        try
        {
            var json = fileSystem.ReadAllText(filePath);
            return DeserializeWithId<T>(json, id);
        }
        catch (Exception ex) when (ex is not ContentModLoadException)
        {
            // 带上文件路径：JsonException 默认只报行列号，多文件 mod 下无法定位是哪个定义写错。
            throw new ContentModLoadException(
                id,
                $"definition file '{filePath}' is invalid: {ex.Message}",
                ex);
        }
    }

    private static T DeserializeWithId<T>(string json, string id)
    {
        using var document = JsonDocument.Parse(json, DocumentOptions);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (IsIdProperty(property))
                {
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return JsonSerializer.Deserialize<T>(stream.ToArray(), ContentDefinitionJson.Options)
            ?? throw new JsonException($"Failed to deserialize definition '{id}'.");
    }

    /// <summary>
    /// 内容定义 id 一律由文件名推导（content-mod-manager 规格 §4.3）。
    /// 这里必须大小写无关地跳过：反序列化使用 <c>PropertyNameCaseInsensitive</c>，
    /// 若只跳过小写 <c>id</c>，文件中写成 <c>"Id"</c> 就会覆盖文件名推导出的 id，
    /// 同一字段出现两种大小写两种行为。
    /// </summary>
    private static bool IsIdProperty(JsonProperty property) =>
        string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase);
}
