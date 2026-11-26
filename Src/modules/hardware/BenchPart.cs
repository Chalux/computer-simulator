using Godot;

namespace ComputerSimulator.Modules.Hardware;

public record Volume(float[] Origin, float[] Size)
{
    public Vector3 Corner => new(Origin[0], Origin[1], Origin[2]);
    public Vector3 Extent => new(Size[0], Size[1], Size[2]);
    public Vector3 Center => Corner + Extent / 2;
}
public sealed record BenchPart(string Id, string Role, float[] Origin, float[] Size, int Fans, bool Rgb, float FanThickness) : Volume(Origin, Size);
