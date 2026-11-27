using Godot;
using KemoCard.Frame.Logging;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 主菜单可调选项的不可变快照：音量 / 全屏 / 垂直同步 / 帧率显示。
/// 只描述状态，不承担读写；运行期值由 <see cref="MenuMod"/> 持有，交互经 <see cref="MenuController"/>。
/// </summary>
public readonly record struct MenuSettings(
    float MasterVolume,
    bool Fullscreen,
    bool Vsync,
    bool ShowFps)
{
    /// <summary>首次启动 / 配置缺失时的默认值，与原静态设置字段的初始值一致。</summary>
    public static MenuSettings Default => new(0.8f, false, true, true);
}

/// <summary>
/// <c>user://menu.cfg</c> 的专用存储：负责加载与落盘，沿用原有 section / key 与默认值，
/// 保证旧配置文件可继续读取，且写出的文件结构不变。
/// </summary>
internal sealed class MenuSettingsStore
{
    private const string ConfigPath = "user://menu.cfg";
    private const string Section = "menu";

    /// <summary>读取配置；文件不存在或损坏时回退默认值。</summary>
    public MenuSettings Load()
    {
        MenuSettings fallback = MenuSettings.Default;

        var file = new ConfigFile();
        if (file.Load(ConfigPath) != Error.Ok) return fallback;

        return new MenuSettings(
            Mathf.Clamp((float)file.GetValue(Section, "master_volume", fallback.MasterVolume).AsDouble(), 0f, 1f),
            file.GetValue(Section, "fullscreen", fallback.Fullscreen).AsBool(),
            file.GetValue(Section, "vsync", fallback.Vsync).AsBool(),
            file.GetValue(Section, "show_fps", fallback.ShowFps).AsBool());
    }

    /// <summary>把当前值写入 <c>user://menu.cfg</c>；连续拖动的音量由调用方在关闭面板时统一落盘。</summary>
    public void Save(MenuSettings settings)
    {
        var file = new ConfigFile();
        file.SetValue(Section, "master_volume", settings.MasterVolume);
        file.SetValue(Section, "fullscreen", settings.Fullscreen);
        file.SetValue(Section, "vsync", settings.Vsync);
        file.SetValue(Section, "show_fps", settings.ShowFps);

        Error error = file.Save(ConfigPath);
        if (error != Error.Ok)
        {
            AppLog.Warning($"主菜单设置保存失败：{error}", "Menu");
        }
    }
}
