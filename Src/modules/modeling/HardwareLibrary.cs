using System.IO;
using Godot;

namespace ComputerSimulator.Modules.Modeling;

/// <summary>
/// 外部 Blender 资产库。res:// 资产走 Godot 已导入的 <see cref="PackedScene"/>；
/// 磁盘上的 .glb/.gltf（内容 Mod、user:// 或绝对路径）走 <see cref="GltfDocument"/> 运行时导入。
/// 模板按路径缓存，调用方拿到的是独立实例，并负责在卸载时释放。
/// </summary>
internal sealed class HardwareLibrary : IDisposable
{
    private readonly Dictionary<string, PackedScene> _imported = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExternalEntry> _external = new(StringComparer.Ordinal);
    private bool _disposed;

    private readonly record struct ExternalEntry(DateTime Stamp, Node3D Template);

    /// <summary>创建资产实例；路径无效、缺失或导入失败时返回 null（由调用方跳过并稍后重试）。</summary>
    public Node3D? TryCreate(string? path)
    {
        if (_disposed || !IsSupported(path)) return null;
        string value = path!;
        try
        {
            return IsImported(value)
                ? CreateImported(value)
                : CreateExternal(value);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"加载硬件资产失败：{path}（{ex.Message}）");
            return null;
        }
    }

    /// <summary>校验路径是否指向一个当前可用的资产（res:// 已导入，或磁盘文件存在）。</summary>
    public static bool IsAvailable(string? path)
    {
        if (!IsSupported(path)) return false;
        string value = path!;
        try
        {
            return IsImported(value)
                ? ResourceLoader.Exists(value)
                : File.Exists(ToSystemPath(value));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>.glb/.gltf，且拒绝包含 .. 的路径穿越。</summary>
    public static bool IsSupported(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".glb" or ".gltf")) return false;
        foreach (string segment in path.Replace('\\', '/').Split('/'))
            if (segment == "..") return false;
        return true;
    }

    private Node3D? CreateImported(string path)
    {
        if (!_imported.TryGetValue(path, out var scene))
        {
            if (!ResourceLoader.Exists(path)) return null;
            scene = ResourceLoader.Load<PackedScene>(path);
            if (scene is null) return null;
            _imported[path] = scene;
        }
        return scene.Instantiate() as Node3D;
    }

    private Node3D? CreateExternal(string path)
    {
        string file = ToSystemPath(path);
        if (!File.Exists(file)) return null;

        var stamp = File.GetLastWriteTimeUtc(file);
        if (!_external.TryGetValue(file, out var entry) || entry.Stamp != stamp)
        {
            if (entry.Template is not null && GodotObject.IsInstanceValid(entry.Template))
                entry.Template.Free();
            var template = LoadGltf(file);
            if (template is null)
            {
                _external.Remove(file);
                return null;
            }
            entry = new ExternalEntry(stamp, template);
            _external[file] = entry;
        }

        return entry.Template.Duplicate() as Node3D;
    }

    private static Node3D? LoadGltf(string file)
    {
        var document = new GltfDocument();
        var state = new GltfState();
        var error = document.AppendFromFile(file, state);
        if (error != Error.Ok)
        {
            GD.PushWarning($"GLTF 导入失败：{file}（{error}）");
            return null;
        }
        return document.GenerateScene(state) as Node3D;
    }

    private static bool IsImported(string path) => path.StartsWith("res://", StringComparison.Ordinal);

    private static string ToSystemPath(string path)
        => path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : path;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var entry in _external.Values)
            if (entry.Template is not null && GodotObject.IsInstanceValid(entry.Template))
                entry.Template.Free();
        _external.Clear();
        _imported.Clear();
    }
}
