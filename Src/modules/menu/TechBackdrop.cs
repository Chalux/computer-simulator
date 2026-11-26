using Godot;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 主菜单背景：一层近黑底色 + 一层着色器冷光。没有任何装饰性框架，
/// 让排版与右侧线框成为唯一视觉内容。
/// </summary>
public partial class TechBackdrop : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        var baseFill = new ColorRect { Color = MenuPalette.Ink, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(baseFill);
        baseFill.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        Shader? shader = GD.Load<Shader>("res://Resource/ui/menu-backdrop.gdshader");
        if (shader == null) return;

        var layer = new ColorRect
        {
            Color = Colors.White,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = shader },
        };
        AddChild(layer);
        layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }
}
