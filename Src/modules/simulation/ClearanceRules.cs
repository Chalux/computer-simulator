using System.Text.Json;
using ComputerSimulator.Modules.Hardware;
using KemoCard.Frame.Scripting;
using Puerts;

namespace ComputerSimulator.Modules.Simulation;

/// <summary>
/// 装配 / 负载规则宿主。目录数据与规则脚本所需的完整目录 JSON 都来自
/// <see cref="GameContent"/>；本类不再读取任何嵌入资源或静态目录。
/// </summary>
internal sealed class ClearanceRules : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ScriptEnv _env;
    private readonly Func<string, string, string> _inspect;
    private readonly Func<string, string, int, string> _testLoad;
    private readonly string _catalogJson;

    public BenchCatalog Catalog { get; }

    public ClearanceRules(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Catalog = content.Catalog;
        _catalogJson = content.RuleCatalogJson;
        _env = new ScriptEnv(new BackendV8(new RuleLoader()));
        try
        {
            var module = _env.ExecuteModule(RuleLoader.Module);
            _inspect = module.Get<Func<string, string, string>>("inspectJson");
            _testLoad = _env.ExecuteModule(RuleLoader.Benchmark).Get<Func<string, string, int, string>>("benchmarkJson");
        }
        catch
        {
            _env.Dispose();
            throw;
        }
    }

    public FitResult Inspect(BenchSetup setup)
    {
        var report = _inspect(_catalogJson, JsonSerializer.Serialize(setup, JsonOptions));
        return JsonSerializer.Deserialize<FitResult>(report, JsonOptions)!;
    }

    public LoadResult TestLoad(BenchSetup setup, int psuIndex)
    {
        var report = _testLoad(_catalogJson, JsonSerializer.Serialize(setup, JsonOptions), psuIndex);
        return JsonSerializer.Deserialize<LoadResult>(report, JsonOptions)!;
    }

    public void Dispose() => _env.Dispose();

    private static string ReadResource(string name)
    {
        using var stream = typeof(ClearanceRules).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"缺少装配资源 {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class RuleLoader : ILoader
    {
        public const string Module = "assembly-fit.mjs";
        public const string Benchmark = "benchmark.mjs";
        private readonly ModScriptLoader _fallback = new(new ModScriptCatalog());
        public bool FileExists(string filepath) => filepath is Module or Benchmark || _fallback.FileExists(filepath);
        public string ReadFile(string filepath, out string debugpath)
        {
            if (filepath is not (Module or Benchmark)) return _fallback.ReadFile(filepath, out debugpath);
            debugpath = filepath;
            return ReadResource(filepath == Module ? "Clearance.Rules" : "Benchmark.Rules");
        }
    }
}
