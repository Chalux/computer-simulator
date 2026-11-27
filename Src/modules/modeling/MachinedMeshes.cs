using Godot;

namespace ComputerSimulator.Modules.Modeling;

/// <summary>
/// 只读取外部模型可见网格的 AABB，用于把 Blender 资产对齐到装配坐标；
/// 不生成任何硬件几何。Blender 资产按毫米建模，且最小角点靠近原点。
/// </summary>
internal static class ModelBounds
{
    private const float Epsilon = 0.0001f;

    /// <summary>计算 <paramref name="root"/> 子树相对根的合并 AABB。</summary>
    public static bool TryCompute(Node3D root, out Aabb bounds)
    {
        var inverse = root.GlobalTransform.AffineInverse();
        bool found = false;
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        Visit(root, node =>
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh is null) return;
            var box = Transform(inverse * mesh.GlobalTransform, mesh.GetAabb());
            min = Min(min, box.Position);
            max = Max(max, box.End);
            found = true;
        });

        bounds = found ? new Aabb(min, max - min) : default;
        return found;
    }

    /// <summary>按轴计算把可见 AABB 映射到期望包围盒的缩放。</summary>
    public static Vector3 FitScale(in Aabb bounds, Vector3 expected)
    {
        var size = bounds.Size;
        return new Vector3(
            size.X > Epsilon && expected.X > 0 ? expected.X / size.X : 1f,
            size.Y > Epsilon && expected.Y > 0 ? expected.Y / size.Y : 1f,
            size.Z > Epsilon && expected.Z > 0 ? expected.Z / size.Z : 1f);
    }

    private static void Visit(Node node, Action<Node> action)
    {
        action(node);
        foreach (var child in node.GetChildren()) Visit(child, action);
    }

    private static Aabb Transform(in Transform3D transform, in Aabb box)
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var end = box.End;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? box.Position.X : end.X,
                (i & 2) == 0 ? box.Position.Y : end.Y,
                (i & 4) == 0 ? box.Position.Z : end.Z);
            var point = transform * corner;
            min = Min(min, point);
            max = Max(max, point);
        }
        return new Aabb(min, max - min);
    }

    private static Vector3 Min(Vector3 a, Vector3 b)
        => new(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y), Mathf.Min(a.Z, b.Z));

    private static Vector3 Max(Vector3 a, Vector3 b)
        => new(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y), Mathf.Max(a.Z, b.Z));
}
