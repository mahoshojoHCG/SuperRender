using SuperRender.Document;

namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// Provides interpolation functions for different CSS property types
/// used in transitions and animations.
/// </summary>
public static class PropertyInterpolation
{
    /// <summary>Linear interpolation between two float values.</summary>
    public static float LerpFloat(float from, float to, float t)
    {
        return from + (to - from) * t;
    }

    /// <summary>Linear interpolation between two colors (component-wise in RGBA).</summary>
    public static Color LerpColor(Color from, Color to, float t)
    {
        return new Color(
            LerpFloat(from.R, to.R, t),
            LerpFloat(from.G, to.G, t),
            LerpFloat(from.B, to.B, t),
            LerpFloat(from.A, to.A, t)
        );
    }

    /// <summary>Interpolates between two transform matrices element-by-element.</summary>
    public static TransformMatrix LerpMatrix(TransformMatrix from, TransformMatrix to, float t)
    {
        var result = new float[16];
        for (int i = 0; i < 16; i++)
            result[i] = LerpFloat(from.Elements[i], to.Elements[i], t);
        return new TransformMatrix(result);
    }

    /// <summary>Interpolates between two lists of transform functions.</summary>
    public static TransformMatrix? InterpolateTransforms(
        List<TransformFunction>? from, List<TransformFunction>? to, float t)
    {
        var fromMatrix = from != null
            ? TransformMatrix.Compose(from.Select(f => f.ToMatrix()))
            : TransformMatrix.Identity();

        var toMatrix = to != null
            ? TransformMatrix.Compose(to.Select(f => f.ToMatrix()))
            : TransformMatrix.Identity();

        return LerpMatrix(fromMatrix, toMatrix, t);
    }
}
