using Godot;

namespace ComputerSimulator.Modules.Modeling;

/// <summary>
/// 一个已加载的外部硬件实例。用模型可见 AABB 对齐到装配坐标给出的包围盒，
/// 并处理机箱 <c>SidePanel</c> 节点的透明切换与 ARGB 节点可见性。不生成任何硬件几何。
/// </summary>
internal sealed class HardwareModel
{
    private readonly Node3D _instance;
    public HardwareModel(Node3D instance) => _instance = instance;

    /// <summary>把资产可见 AABB 的最小角点对齐到当前节点原点，并缩放到 <paramref name="expected"/>。</summary>
    public bool Fit(Vector3 expected, bool preserveHeight, bool alignTop)
    {
        if (!ModelBounds.TryCompute(_instance, out var bounds)) return false;

        var scale = ModelBounds.FitScale(bounds, expected);
        // 主板规则包围盒只描述 PCB 厚度。保留 Blender 资产中的插槽、散热片和机箱托盘高度。
        if (preserveHeight) scale.Y = 1;
        var basis = new Basis(Vector3.Right * scale.X, Vector3.Up * scale.Y, Vector3.Back * scale.Z);
        var origin = -bounds.Position * scale;
        if (alignTop) origin.Y = expected.Y - bounds.End.Y;
        _instance.Transform = new Transform3D(basis, origin);
        return true;
    }

    public void SetSidePanelVisible(bool visible)
    {
        if (_instance.FindChild("SidePanel", true, false) is Node3D panel) panel.Visible = visible;
    }

    public void SetRgb(bool enabled)
    {
        foreach (var geometry in CollectAll(_instance))
        {
            string name = geometry.Name.ToString().ToLowerInvariant();
            if (!name.Contains("led") && !name.Contains("argb") && !name.Contains("rgb")) continue;
            geometry.Visible = enabled;
        }
    }

    private static IEnumerable<GeometryInstance3D> CollectAll(Node node)
    {
        if (node is GeometryInstance3D geometry) yield return geometry;
        foreach (var child in node.GetChildren())
            foreach (var nested in CollectAll(child))
                yield return nested;
    }
}
