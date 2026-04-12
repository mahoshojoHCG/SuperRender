using System.Numerics;
using SuperRender.Document;
using SuperRender.Renderer.Rendering.Painting;

namespace SuperRender.Renderer.Gpu;

/// <summary>
/// Builds GPU vertex data for gradient and box-shadow paint commands.
/// Gradients are decomposed into a mesh of quads where each vertex carries
/// the interpolated color at its gradient position, letting the GPU hardware
/// interpolation produce the smooth gradient.
/// </summary>
public static class GradientRenderer
{
    /// <summary>
    /// Emits quads for a linear gradient. The gradient line is divided into
    /// segments between color stops. Each segment is a quad with the start
    /// and end colors set on its vertices.
    /// </summary>
    public static void EmitLinearGradient(
        List<QuadVertex> verts, List<uint> idx,
        DrawLinearGradientCommand cmd)
    {
        var rect = cmd.Rect;
        var stops = cmd.ColorStops;
        if (stops.Count < 2) return;

        // Gradient angle: 0 = to top, 90 = to right, 180 = to bottom
        float angleDeg = cmd.AngleDeg;
        float angleRad = angleDeg * MathF.PI / 180f;

        // Gradient direction vector (in CSS, 0deg = to top = -Y)
        float dx = MathF.Sin(angleRad);
        float dy = -MathF.Cos(angleRad);

        // For axis-aligned gradients, emit simple quads
        bool isVertical = MathF.Abs(dx) < 0.001f;
        bool isHorizontal = MathF.Abs(dy) < 0.001f;

        if (isVertical)
        {
            // Top-to-bottom or bottom-to-top
            bool downward = dy > 0;
            for (int i = 0; i < stops.Count - 1; i++)
            {
                float t0 = stops[i].Position;
                float t1 = stops[i + 1].Position;
                var c0 = stops[i].Color;
                var c1 = stops[i + 1].Color;

                float y0, y1;
                if (downward)
                {
                    y0 = rect.Y + t0 * rect.Height;
                    y1 = rect.Y + t1 * rect.Height;
                }
                else
                {
                    y0 = rect.Y + (1 - t0) * rect.Height;
                    y1 = rect.Y + (1 - t1) * rect.Height;
                }

                AddGradientQuadVertical(verts, idx, rect.X, y0, rect.Width, y1 - y0,
                    c0, c1, downward, cmd);
            }
        }
        else if (isHorizontal)
        {
            // Left-to-right or right-to-left
            bool rightward = dx > 0;
            for (int i = 0; i < stops.Count - 1; i++)
            {
                float t0 = stops[i].Position;
                float t1 = stops[i + 1].Position;
                var c0 = stops[i].Color;
                var c1 = stops[i + 1].Color;

                float x0, x1;
                if (rightward)
                {
                    x0 = rect.X + t0 * rect.Width;
                    x1 = rect.X + t1 * rect.Width;
                }
                else
                {
                    x0 = rect.X + (1 - t0) * rect.Width;
                    x1 = rect.X + (1 - t1) * rect.Width;
                }

                AddGradientQuadHorizontal(verts, idx, x0, rect.Y, x1 - x0, rect.Height,
                    c0, c1, rightward, cmd);
            }
        }
        else
        {
            // Diagonal gradient: approximate with the full rect using start/end colors
            // This is a simplification; a full implementation would compute per-vertex
            // positions along the gradient line.
            var c0 = stops[0].Color;
            var c1 = stops[^1].Color;
            EmitDiagonalGradientQuad(verts, idx, rect, c0, c1, dx, dy, cmd);
        }
    }

    /// <summary>
    /// Emits quads for a radial gradient by rendering the full rect with
    /// the outermost color and overlaying concentric filled rects.
    /// This is a CPU-side approximation; a proper implementation would use
    /// a dedicated radial gradient shader.
    /// </summary>
    public static void EmitRadialGradient(
        List<QuadVertex> verts, List<uint> idx,
        DrawRadialGradientCommand cmd)
    {
        var rect = cmd.Rect;
        var stops = cmd.ColorStops;
        if (stops.Count < 2) return;

        // Render as concentric bands from outside in
        // First, fill with the outermost color
        var outerColor = stops[^1].Color;
        QuadRenderer.AddFilledRect(verts, idx, rect.X, rect.Y, rect.Width, rect.Height, outerColor);

        // Then overlay inner rings (from second-to-last to first)
        float cxPx = rect.X + cmd.CenterX * rect.Width;
        float cyPx = rect.Y + cmd.CenterY * rect.Height;
        float maxRadius = MathF.Max(rect.Width, rect.Height) * 0.5f;

        for (int i = stops.Count - 2; i >= 0; i--)
        {
            float t = stops[i].Position;
            float radius = maxRadius * t;
            if (radius <= 0) continue;

            float rx = rect.X + (cxPx - rect.X) - radius;
            float ry = rect.Y + (cyPx - rect.Y) - radius;
            float rw = radius * 2;
            float rh = radius * 2;

            QuadRenderer.AddFilledRect(verts, idx, rx, ry, rw, rh, stops[i].Color);
        }
    }

    /// <summary>
    /// Emits a quad for a box shadow. The shadow quad is larger than the
    /// element rect by (blur + spread + offset). The shadow shader uses
    /// SDF to compute the falloff.
    /// </summary>
    public static void EmitBoxShadow(
        List<QuadVertex> verts, List<uint> idx,
        DrawBoxShadowCommand cmd)
    {
        float expand = cmd.BlurRadius * 2f + MathF.Abs(cmd.SpreadRadius);
        float x = cmd.Rect.X + cmd.OffsetX - expand;
        float y = cmd.Rect.Y + cmd.OffsetY - expand;
        float w = cmd.Rect.Width + expand * 2;
        float h = cmd.Rect.Height + expand * 2;

        // The element center and half-size (with spread) for the SDF in the shader
        float elemCx = cmd.Rect.X + cmd.OffsetX + cmd.Rect.Width * 0.5f;
        float elemCy = cmd.Rect.Y + cmd.OffsetY + cmd.Rect.Height * 0.5f;
        float elemHw = (cmd.Rect.Width + cmd.SpreadRadius * 2) * 0.5f;
        float elemHh = (cmd.Rect.Height + cmd.SpreadRadius * 2) * 0.5f;

        uint baseIndex = (uint)verts.Count;
        var col = new Vector4(cmd.Color.R, cmd.Color.G, cmd.Color.B, cmd.Color.A);
        var center = new Vector2(elemCx, elemCy);
        var halfSize = new Vector2(elemHw, elemHh);
        var radius = new Vector4(cmd.RadiusTL, cmd.RadiusTR, cmd.RadiusBR, cmd.RadiusBL);

        verts.Add(new QuadVertex { Position = new Vector2(x, y), Color = col, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });
        verts.Add(new QuadVertex { Position = new Vector2(x + w, y), Color = col, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });
        verts.Add(new QuadVertex { Position = new Vector2(x + w, y + h), Color = col, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });
        verts.Add(new QuadVertex { Position = new Vector2(x, y + h), Color = col, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });

        idx.Add(baseIndex);
        idx.Add(baseIndex + 1);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex + 3);
    }

    /// <summary>
    /// Emits an outline as four stroke rects around the element.
    /// </summary>
    public static void EmitOutline(
        List<QuadVertex> verts, List<uint> idx,
        DrawOutlineCommand cmd)
    {
        float offset = cmd.Offset;
        float w = cmd.Width;
        var r = cmd.Rect;

        float ox = r.X - w - offset;
        float oy = r.Y - w - offset;
        float ow = r.Width + 2 * (w + offset);
        float oh = r.Height + 2 * (w + offset);

        // Top
        QuadRenderer.AddFilledRect(verts, idx, ox, oy, ow, w, cmd.Color);
        // Bottom
        QuadRenderer.AddFilledRect(verts, idx, ox, oy + oh - w, ow, w, cmd.Color);
        // Left
        QuadRenderer.AddFilledRect(verts, idx, ox, oy + w, w, oh - 2 * w, cmd.Color);
        // Right
        QuadRenderer.AddFilledRect(verts, idx, ox + ow - w, oy + w, w, oh - 2 * w, cmd.Color);
    }

    private static void AddGradientQuadVertical(
        List<QuadVertex> verts, List<uint> idx,
        float x, float y, float w, float h,
        Color topColor, Color bottomColor, bool downward,
        DrawLinearGradientCommand cmd)
    {
        uint baseIndex = (uint)verts.Count;
        var center = new Vector2(cmd.Rect.X + cmd.Rect.Width * 0.5f, cmd.Rect.Y + cmd.Rect.Height * 0.5f);
        var halfSize = new Vector2(cmd.Rect.Width * 0.5f, cmd.Rect.Height * 0.5f);
        var radius = new Vector4(cmd.RadiusTL, cmd.RadiusTR, cmd.RadiusBR, cmd.RadiusBL);

        var c0 = downward ? topColor : bottomColor;
        var c1 = downward ? bottomColor : topColor;
        var col0 = new Vector4(c0.R, c0.G, c0.B, c0.A);
        var col1 = new Vector4(c1.R, c1.G, c1.B, c1.A);

        // Top-left, Top-right (color at top), Bottom-right, Bottom-left (color at bottom)
        verts.Add(new QuadVertex { Position = new Vector2(x, y), Color = col0, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });
        verts.Add(new QuadVertex { Position = new Vector2(x + w, y), Color = col0, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });
        verts.Add(new QuadVertex { Position = new Vector2(x + w, y + h), Color = col1, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });
        verts.Add(new QuadVertex { Position = new Vector2(x, y + h), Color = col1, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });

        idx.Add(baseIndex);
        idx.Add(baseIndex + 1);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex + 3);
    }

    private static void AddGradientQuadHorizontal(
        List<QuadVertex> verts, List<uint> idx,
        float x, float y, float w, float h,
        Color leftColor, Color rightColor, bool rightward,
        DrawLinearGradientCommand cmd)
    {
        uint baseIndex = (uint)verts.Count;
        var center = new Vector2(cmd.Rect.X + cmd.Rect.Width * 0.5f, cmd.Rect.Y + cmd.Rect.Height * 0.5f);
        var halfSize = new Vector2(cmd.Rect.Width * 0.5f, cmd.Rect.Height * 0.5f);
        var radius = new Vector4(cmd.RadiusTL, cmd.RadiusTR, cmd.RadiusBR, cmd.RadiusBL);

        var cL = rightward ? leftColor : rightColor;
        var cR = rightward ? rightColor : leftColor;
        var colL = new Vector4(cL.R, cL.G, cL.B, cL.A);
        var colR = new Vector4(cR.R, cR.G, cR.B, cR.A);

        verts.Add(new QuadVertex { Position = new Vector2(x, y), Color = colL, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });
        verts.Add(new QuadVertex { Position = new Vector2(x + w, y), Color = colR, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });
        verts.Add(new QuadVertex { Position = new Vector2(x + w, y + h), Color = colR, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });
        verts.Add(new QuadVertex { Position = new Vector2(x, y + h), Color = colL, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });

        idx.Add(baseIndex);
        idx.Add(baseIndex + 1);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex + 3);
    }

    private static void EmitDiagonalGradientQuad(
        List<QuadVertex> verts, List<uint> idx,
        Rendering.Layout.RectF rect,
        Color startColor, Color endColor,
        float dx, float dy,
        DrawLinearGradientCommand cmd)
    {
        uint baseIndex = (uint)verts.Count;
        var center = new Vector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);
        var halfSize = new Vector2(rect.Width * 0.5f, rect.Height * 0.5f);
        var radius = new Vector4(cmd.RadiusTL, cmd.RadiusTR, cmd.RadiusBR, cmd.RadiusBL);

        // Compute per-vertex gradient position by projecting onto the gradient line
        var corners = new Vector2[]
        {
            new(rect.X, rect.Y),
            new(rect.X + rect.Width, rect.Y),
            new(rect.X + rect.Width, rect.Y + rect.Height),
            new(rect.X, rect.Y + rect.Height),
        };

        var dir = new Vector2(dx, dy);
        float minProj = float.MaxValue;
        float maxProj = float.MinValue;
        foreach (var c in corners)
        {
            float proj = Vector2.Dot(c - center, dir);
            minProj = MathF.Min(minProj, proj);
            maxProj = MathF.Max(maxProj, proj);
        }
        float range = maxProj - minProj;
        if (range < 0.001f) range = 1f;

        var cs = new Vector4(startColor.R, startColor.G, startColor.B, startColor.A);
        var ce = new Vector4(endColor.R, endColor.G, endColor.B, endColor.A);

        for (int i = 0; i < 4; i++)
        {
            float t = (Vector2.Dot(corners[i] - center, dir) - minProj) / range;
            t = Math.Clamp(t, 0f, 1f);
            var col = Vector4.Lerp(cs, ce, t);
            verts.Add(new QuadVertex { Position = corners[i], Color = col, RectCenter = center, RectHalfSize = halfSize, BorderRadius = radius });
        }

        idx.Add(baseIndex);
        idx.Add(baseIndex + 1);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex + 3);
    }
}
