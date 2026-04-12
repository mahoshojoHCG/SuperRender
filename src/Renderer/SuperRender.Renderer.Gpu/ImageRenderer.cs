using System.Numerics;

namespace SuperRender.Renderer.Gpu;

public static class ImageRenderer
{
    public static void EmitImageQuad(
        List<TextVertex> verts, List<uint> idx,
        float x, float y, float w, float h, float opacity)
    {
        uint baseIndex = (uint)verts.Count;
        var color = new Vector4(1f, 1f, 1f, opacity);

        verts.Add(new TextVertex
        {
            Position = new Vector2(x, y),
            TexCoord = new Vector2(0f, 0f),
            Color = color,
        });
        verts.Add(new TextVertex
        {
            Position = new Vector2(x + w, y),
            TexCoord = new Vector2(1f, 0f),
            Color = color,
        });
        verts.Add(new TextVertex
        {
            Position = new Vector2(x + w, y + h),
            TexCoord = new Vector2(1f, 1f),
            Color = color,
        });
        verts.Add(new TextVertex
        {
            Position = new Vector2(x, y + h),
            TexCoord = new Vector2(0f, 1f),
            Color = color,
        });

        idx.Add(baseIndex);
        idx.Add(baseIndex + 1);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex + 3);
    }
}
