using System.Numerics;
using System.Runtime.InteropServices;
using SuperRender.Core.Painting;

namespace SuperRender.Gpu;

[StructLayout(LayoutKind.Sequential)]
public struct QuadVertex
{
    public Vector2 Position;  // 8 bytes
    public Vector4 Color;     // 16 bytes  (total stride = 24)
}

public sealed class QuadRenderer
{
    /// <summary>
    /// Scans the <paramref name="paintList"/> for <see cref="FillRectCommand"/> and
    /// <see cref="StrokeRectCommand"/> entries and returns interleaved vertex + index
    /// arrays ready for GPU upload.
    /// </summary>
    public static (QuadVertex[] vertices, uint[] indices) BuildQuadBatch(PaintList paintList)
    {
        var vertices = new List<QuadVertex>();
        var indices = new List<uint>();

        foreach (var cmd in paintList.Commands)
        {
            switch (cmd)
            {
                case FillRectCommand fill:
                    AddFilledRect(vertices, indices, fill.Rect.X, fill.Rect.Y,
                        fill.Rect.Width, fill.Rect.Height, fill.Color);
                    break;

                case StrokeRectCommand stroke:
                    AddStrokeRect(vertices, indices, stroke.Rect.X, stroke.Rect.Y,
                        stroke.Rect.Width, stroke.Rect.Height, stroke.LineWidth, stroke.Color);
                    break;
            }
        }

        return (vertices.ToArray(), indices.ToArray());
    }

    private static void AddFilledRect(
        List<QuadVertex> verts, List<uint> idx,
        float x, float y, float w, float h,
        SuperRender.Core.Color color)
    {
        uint baseIndex = (uint)verts.Count;
        var col = new Vector4(color.R, color.G, color.B, color.A);

        verts.Add(new QuadVertex { Position = new Vector2(x, y), Color = col });
        verts.Add(new QuadVertex { Position = new Vector2(x + w, y), Color = col });
        verts.Add(new QuadVertex { Position = new Vector2(x + w, y + h), Color = col });
        verts.Add(new QuadVertex { Position = new Vector2(x, y + h), Color = col });

        // Two triangles: 0-1-2, 0-2-3
        idx.Add(baseIndex);
        idx.Add(baseIndex + 1);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex + 3);
    }

    private static void AddStrokeRect(
        List<QuadVertex> verts, List<uint> idx,
        float x, float y, float w, float h,
        float lineWidth, SuperRender.Core.Color color)
    {
        float lw = lineWidth;

        // Top edge
        AddFilledRect(verts, idx, x, y, w, lw, color);
        // Bottom edge
        AddFilledRect(verts, idx, x, y + h - lw, w, lw, color);
        // Left edge (between top and bottom)
        AddFilledRect(verts, idx, x, y + lw, lw, h - 2 * lw, color);
        // Right edge (between top and bottom)
        AddFilledRect(verts, idx, x + w - lw, y + lw, lw, h - 2 * lw, color);
    }
}
