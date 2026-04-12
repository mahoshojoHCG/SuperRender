using System.Globalization;

namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// 4x4 transformation matrix stored in column-major order (OpenGL/Vulkan convention).
/// Elements: M[row, col] where row 0..3, col 0..3.
/// </summary>
public sealed class TransformMatrix
{
    /// <summary>Column-major 16-element array (col0 row0-3, col1 row0-3, ...).</summary>
    public float[] Elements { get; }

    public TransformMatrix()
    {
        Elements = Identity().Elements.ToArray();
    }

    public TransformMatrix(float[] elements)
    {
        if (elements.Length != 16)
            throw new ArgumentException("Matrix requires 16 elements.", nameof(elements));
        Elements = (float[])elements.Clone();
    }

    // Indexer: M[row, col]
    private static int Idx(int row, int col) => col * 4 + row;

    public float this[int row, int col]
    {
        get => Elements[Idx(row, col)];
        set => Elements[Idx(row, col)] = value;
    }

    public static TransformMatrix Identity()
    {
        return new TransformMatrix(
        [
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1,
        ]);
    }

    /// <summary>
    /// Multiplies this * other, returning a new matrix.
    /// </summary>
    public TransformMatrix Multiply(TransformMatrix other)
    {
        var result = new float[16];
        for (int col = 0; col < 4; col++)
        {
            for (int row = 0; row < 4; row++)
            {
                float sum = 0;
                for (int k = 0; k < 4; k++)
                    sum += this[row, k] * other[k, col];
                result[col * 4 + row] = sum;
            }
        }
        return new TransformMatrix(result);
    }

    public static TransformMatrix CreateTranslation(float tx, float ty, float tz = 0)
    {
        var m = Identity();
        m[0, 3] = tx;
        m[1, 3] = ty;
        m[2, 3] = tz;
        return m;
    }

    public static TransformMatrix CreateScale(float sx, float sy, float sz = 1)
    {
        var m = Identity();
        m[0, 0] = sx;
        m[1, 1] = sy;
        m[2, 2] = sz;
        return m;
    }

    /// <summary>Rotation around Z axis by angle in radians.</summary>
    public static TransformMatrix CreateRotateZ(float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        var m = Identity();
        m[0, 0] = cos;
        m[0, 1] = -sin;
        m[1, 0] = sin;
        m[1, 1] = cos;
        return m;
    }

    public static TransformMatrix CreateRotateX(float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        var m = Identity();
        m[1, 1] = cos;
        m[1, 2] = -sin;
        m[2, 1] = sin;
        m[2, 2] = cos;
        return m;
    }

    public static TransformMatrix CreateRotateY(float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        var m = Identity();
        m[0, 0] = cos;
        m[0, 2] = sin;
        m[2, 0] = -sin;
        m[2, 2] = cos;
        return m;
    }

    /// <summary>Rotation around arbitrary axis (x,y,z) by angle in radians.</summary>
    public static TransformMatrix CreateRotate3D(float x, float y, float z, float radians)
    {
        float len = MathF.Sqrt(x * x + y * y + z * z);
        if (len < 1e-8f) return Identity();
        x /= len; y /= len; z /= len;

        float c = MathF.Cos(radians);
        float s = MathF.Sin(radians);
        float t = 1 - c;

        var m = Identity();
        m[0, 0] = t * x * x + c;
        m[0, 1] = t * x * y - s * z;
        m[0, 2] = t * x * z + s * y;
        m[1, 0] = t * x * y + s * z;
        m[1, 1] = t * y * y + c;
        m[1, 2] = t * y * z - s * x;
        m[2, 0] = t * x * z - s * y;
        m[2, 1] = t * y * z + s * x;
        m[2, 2] = t * z * z + c;
        return m;
    }

    public static TransformMatrix CreateSkewX(float radians)
    {
        var m = Identity();
        m[0, 1] = MathF.Tan(radians);
        return m;
    }

    public static TransformMatrix CreateSkewY(float radians)
    {
        var m = Identity();
        m[1, 0] = MathF.Tan(radians);
        return m;
    }

    public static TransformMatrix CreatePerspective(float d)
    {
        var m = Identity();
        if (d > 0)
            m[3, 2] = -1f / d;
        return m;
    }

    /// <summary>
    /// Creates a 2D matrix from 6 values: matrix(a, b, c, d, tx, ty).
    /// </summary>
    public static TransformMatrix FromMatrix2D(float a, float b, float c, float d, float tx, float ty)
    {
        var m = Identity();
        m[0, 0] = a;
        m[1, 0] = b;
        m[0, 1] = c;
        m[1, 1] = d;
        m[0, 3] = tx;
        m[1, 3] = ty;
        return m;
    }

    /// <summary>Creates a 4x4 matrix from 16 values in row-major order as per CSS matrix3d().</summary>
    public static TransformMatrix FromMatrix3D(float[] values)
    {
        if (values.Length != 16)
            throw new ArgumentException("matrix3d requires 16 values.", nameof(values));
        // CSS matrix3d is row-major: m11,m12,m13,m14, m21,m22,m23,m24, ...
        // Convert to our column-major storage
        var m = new TransformMatrix();
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4; col++)
                m[row, col] = values[row * 4 + col];
        return m;
    }

    /// <summary>Composes a list of transform matrices by multiplying left to right.</summary>
    public static TransformMatrix Compose(IEnumerable<TransformMatrix> matrices)
    {
        var result = Identity();
        foreach (var m in matrices)
            result = result.Multiply(m);
        return result;
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture,
            "matrix3d({0})", string.Join(", ", Elements.Select(e => e.ToString(CultureInfo.InvariantCulture))));
    }
}
