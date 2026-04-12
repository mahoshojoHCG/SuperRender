using System.Globalization;

namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// Base class for CSS transform functions. Each subclass represents a specific
/// CSS transform function (translate, rotate, scale, skew, matrix, etc.).
/// </summary>
public abstract class TransformFunction
{
    /// <summary>Converts this transform function to a 4x4 transformation matrix.</summary>
    public abstract TransformMatrix ToMatrix();
}

public sealed class TranslateXFunction : TransformFunction
{
    public float X { get; }
    public TranslateXFunction(float x) => X = x;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateTranslation(X, 0);
}

public sealed class TranslateYFunction : TransformFunction
{
    public float Y { get; }
    public TranslateYFunction(float y) => Y = y;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateTranslation(0, Y);
}

public sealed class TranslateFunction : TransformFunction
{
    public float X { get; }
    public float Y { get; }
    public TranslateFunction(float x, float y) { X = x; Y = y; }
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateTranslation(X, Y);
}

public sealed class Translate3DFunction : TransformFunction
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public Translate3DFunction(float x, float y, float z) { X = x; Y = y; Z = z; }
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateTranslation(X, Y, Z);
}

public sealed class ScaleXFunction : TransformFunction
{
    public float Sx { get; }
    public ScaleXFunction(float sx) => Sx = sx;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateScale(Sx, 1);
}

public sealed class ScaleYFunction : TransformFunction
{
    public float Sy { get; }
    public ScaleYFunction(float sy) => Sy = sy;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateScale(1, Sy);
}

public sealed class ScaleFunction : TransformFunction
{
    public float Sx { get; }
    public float Sy { get; }
    public ScaleFunction(float sx, float sy) { Sx = sx; Sy = sy; }
    public ScaleFunction(float s) { Sx = s; Sy = s; }
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateScale(Sx, Sy);
}

public sealed class Scale3DFunction : TransformFunction
{
    public float Sx { get; }
    public float Sy { get; }
    public float Sz { get; }
    public Scale3DFunction(float sx, float sy, float sz) { Sx = sx; Sy = sy; Sz = sz; }
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateScale(Sx, Sy, Sz);
}

public sealed class RotateFunction : TransformFunction
{
    /// <summary>Angle in radians.</summary>
    public float Angle { get; }
    public RotateFunction(float radians) => Angle = radians;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateRotateZ(Angle);
}

public sealed class RotateXFunction : TransformFunction
{
    public float Angle { get; }
    public RotateXFunction(float radians) => Angle = radians;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateRotateX(Angle);
}

public sealed class RotateYFunction : TransformFunction
{
    public float Angle { get; }
    public RotateYFunction(float radians) => Angle = radians;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateRotateY(Angle);
}

public sealed class RotateZFunction : TransformFunction
{
    public float Angle { get; }
    public RotateZFunction(float radians) => Angle = radians;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateRotateZ(Angle);
}

public sealed class Rotate3DFunction : TransformFunction
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public float Angle { get; }
    public Rotate3DFunction(float x, float y, float z, float radians)
    { X = x; Y = y; Z = z; Angle = radians; }
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateRotate3D(X, Y, Z, Angle);
}

public sealed class SkewXFunction : TransformFunction
{
    public float Angle { get; }
    public SkewXFunction(float radians) => Angle = radians;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateSkewX(Angle);
}

public sealed class SkewYFunction : TransformFunction
{
    public float Angle { get; }
    public SkewYFunction(float radians) => Angle = radians;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreateSkewY(Angle);
}

public sealed class MatrixFunction : TransformFunction
{
    public float A { get; }
    public float B { get; }
    public float C { get; }
    public float D { get; }
    public float Tx { get; }
    public float Ty { get; }
    public MatrixFunction(float a, float b, float c, float d, float tx, float ty)
    { A = a; B = b; C = c; D = d; Tx = tx; Ty = ty; }
    public override TransformMatrix ToMatrix() => TransformMatrix.FromMatrix2D(A, B, C, D, Tx, Ty);
}

public sealed class Matrix3DFunction : TransformFunction
{
    public float[] Values { get; }
    public Matrix3DFunction(float[] values)
    {
        Values = values.Length == 16 ? (float[])values.Clone() : new float[16];
    }
    public override TransformMatrix ToMatrix() => TransformMatrix.FromMatrix3D(Values);
}

public sealed class PerspectiveFunction : TransformFunction
{
    public float Distance { get; }
    public PerspectiveFunction(float distance) => Distance = distance;
    public override TransformMatrix ToMatrix() => TransformMatrix.CreatePerspective(Distance);
}

/// <summary>
/// Helper to parse angle values from CSS (deg, rad, grad, turn).
/// </summary>
public static class AngleParser
{
    /// <summary>Parses a CSS angle string to radians. Returns 0 on failure.</summary>
    public static float ParseToRadians(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();

        if (trimmed.EndsWith("deg", StringComparison.Ordinal))
        {
            if (float.TryParse(trimmed[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out float deg))
                return deg * MathF.PI / 180f;
        }
        else if (trimmed.EndsWith("grad", StringComparison.Ordinal))
        {
            if (float.TryParse(trimmed[..^4], NumberStyles.Float, CultureInfo.InvariantCulture, out float grad))
                return grad * MathF.PI / 200f;
        }
        else if (trimmed.EndsWith("rad", StringComparison.Ordinal))
        {
            if (float.TryParse(trimmed[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out float rad))
                return rad;
        }
        else if (trimmed.EndsWith("turn", StringComparison.Ordinal))
        {
            if (float.TryParse(trimmed[..^4], NumberStyles.Float, CultureInfo.InvariantCulture, out float turn))
                return turn * 2 * MathF.PI;
        }
        else
        {
            // Bare number treated as degrees for rotate
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float bare))
                return bare * MathF.PI / 180f;
        }

        return 0;
    }
}
