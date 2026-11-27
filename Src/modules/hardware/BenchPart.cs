namespace ComputerSimulator.Modules.Hardware;

public record Volume(float[] Origin, float[] Size);

public sealed record BenchPart(string Id, string Role, float[] Origin, float[] Size, int Fans, bool Rgb, float FanThickness) : Volume(Origin, Size);
