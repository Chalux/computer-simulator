namespace KemoCard.Frame.Content;

/// <summary>
/// Content Mod 读盘抽象。编辑器与导出包内的 <c>res://</c> 无法用 <see cref="System.IO"/>
/// 枚举（PCK 内目录不是真实文件系统），因此发现、清单与定义加载统一走本接口；
/// 纯逻辑测试可以使用默认的 <see cref="SystemContentModFileSystem"/>。
/// </summary>
public interface IContentModFileSystem
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    /// <summary>返回子目录完整路径，按序数排序由调用方负责。</summary>
    IReadOnlyList<string> EnumerateDirectories(string path);

    /// <summary>返回目录下的文件完整路径，按序数排序由调用方负责。</summary>
    IReadOnlyList<string> EnumerateFiles(string path);

    string ReadAllText(string path);

    byte[] ReadAllBytes(string path);
}

/// <summary>普通文件系统实现；用于单元测试、工具链与绝对路径 Mod 根。</summary>
public sealed class SystemContentModFileSystem : IContentModFileSystem
{
    public static SystemContentModFileSystem Instance { get; } = new();

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public IReadOnlyList<string> EnumerateDirectories(string path) =>
        Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>();

    public IReadOnlyList<string> EnumerateFiles(string path) =>
        Directory.Exists(path) ? Directory.GetFiles(path) : Array.Empty<string>();

    public string ReadAllText(string path) => File.ReadAllText(path);

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
}

/// <summary>
/// 资源路径以 <c>/</c> 拼接，普通路径使用平台分隔符。Godot 的 <c>res://</c> / <c>user://</c>
/// 不能用 <see cref="Path.Combine(string, string)"/>，否则会混入反斜杠。
/// </summary>
public static class ContentModPath
{
    public static string Combine(string left, string right)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(left);
        ArgumentException.ThrowIfNullOrWhiteSpace(right);

        if (IsResourcePath(left) || left.Contains('/'))
        {
            return left.TrimEnd('/') + "/" + right.TrimStart('/', '\\');
        }

        return Path.Combine(left, right);
    }

    public static string GetFileName(string path)
    {
        var normalized = path.Replace('\\', '/');
        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    public static string GetFileNameWithoutExtension(string path)
    {
        var name = GetFileName(path);
        var dot = name.LastIndexOf('.');
        return dot <= 0 ? name : name[..dot];
    }

    public static bool IsResourcePath(string path) =>
        path.StartsWith("res://", StringComparison.Ordinal) ||
        path.StartsWith("user://", StringComparison.Ordinal);
}
