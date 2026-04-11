using System.Numerics;
using System.Runtime.InteropServices;
using SuperRender.Renderer.Rendering.Painting;

namespace SuperRender.Renderer.Gpu;

[StructLayout(LayoutKind.Sequential)]
public struct TextVertex
{
    public Vector2 Position;  // 8 bytes
    public Vector2 TexCoord;  // 8 bytes
    public Vector4 Color;     // 16 bytes  (total stride = 32)
}

public sealed class TextRenderer
{
    private readonly FontAtlas _fontAtlas;

    public TextRenderer(FontAtlas fontAtlas)
    {
        _fontAtlas = fontAtlas;
    }

    /// <summary>
    /// Scans the <paramref name="paintList"/> for <see cref="DrawTextCommand"/> entries
    /// and returns interleaved vertex + index arrays for textured quads.
    /// </summary>
    public (TextVertex[] vertices, uint[] indices) BuildTextBatch(PaintList paintList)
    {
        var vertices = new List<TextVertex>();
        var indices = new List<uint>();

        foreach (var cmd in paintList.Commands)
        {
            if (cmd is DrawTextCommand text)
                EmitTextQuads(vertices, indices, text);
        }

        return (vertices.ToArray(), indices.ToArray());
    }

    public void EmitTextQuads(
        List<TextVertex> verts, List<uint> idx, DrawTextCommand cmd)
    {
        float scale = cmd.FontSize / FontAtlasGenerator.AtlasRenderSize;
        var color = new Vector4(cmd.Color.R, cmd.Color.G, cmd.Color.B, cmd.Color.A);

        // Select glyph set based on font properties
        var (glyphs, variant) = cmd.FontFamilies.Count > 0
            ? SelectGlyphsWithVariant(cmd.FontFamilies, cmd.FontWeight)
            : SelectGlyphsWithVariant(cmd.FontFamily, cmd.FontWeight);

        // Ensure all characters in the text are in the atlas
        _fontAtlas.EnsureGlyphs(cmd.Text, variant);

        float cursorX = cmd.X;
        float baselineY = cmd.Y;

        foreach (char c in cmd.Text)
        {
            if (!glyphs.TryGetValue(c, out var glyph))
            {
                // Try fallback '?', or skip entirely
                if (!glyphs.TryGetValue('?', out glyph))
                {
                    cursorX += cmd.FontSize * 0.6f; // rough advance for unknown
                    continue;
                }
            }

            float w = glyph.Width * scale;
            float h = glyph.Height * scale;
            float x = cursorX + glyph.OffsetX * scale;
            float y = baselineY + glyph.OffsetY * scale;

            uint baseIndex = (uint)verts.Count;

            verts.Add(new TextVertex
            {
                Position = new Vector2(x, y),
                TexCoord = new Vector2(glyph.U0, glyph.V0),
                Color = color
            });
            verts.Add(new TextVertex
            {
                Position = new Vector2(x + w, y),
                TexCoord = new Vector2(glyph.U1, glyph.V0),
                Color = color
            });
            verts.Add(new TextVertex
            {
                Position = new Vector2(x + w, y + h),
                TexCoord = new Vector2(glyph.U1, glyph.V1),
                Color = color
            });
            verts.Add(new TextVertex
            {
                Position = new Vector2(x, y + h),
                TexCoord = new Vector2(glyph.U0, glyph.V1),
                Color = color
            });

            // Two triangles: 0-1-2, 0-2-3
            idx.Add(baseIndex);
            idx.Add(baseIndex + 1);
            idx.Add(baseIndex + 2);
            idx.Add(baseIndex);
            idx.Add(baseIndex + 2);
            idx.Add(baseIndex + 3);

            cursorX += glyph.AdvanceX * scale;
        }
    }

    private (Dictionary<char, GlyphInfo> glyphs, GlyphVariant variant)
        SelectGlyphsWithVariant(IReadOnlyList<string> fontFamilies, int fontWeight)
    {
        foreach (var family in fontFamilies)
        {
            if (IsMonospaceFamily(family))
                return (_fontAtlas.MonospaceGlyphs, GlyphVariant.Monospace);
        }

        if (fontWeight >= 700)
            return (_fontAtlas.BoldGlyphs, GlyphVariant.Bold);
        return (_fontAtlas.Glyphs, GlyphVariant.Regular);
    }

    private (Dictionary<char, GlyphInfo> glyphs, GlyphVariant variant)
        SelectGlyphsWithVariant(string fontFamily, int fontWeight)
    {
        if (!string.IsNullOrEmpty(fontFamily) && IsMonospaceFamily(fontFamily))
            return (_fontAtlas.MonospaceGlyphs, GlyphVariant.Monospace);

        if (fontWeight >= 700)
            return (_fontAtlas.BoldGlyphs, GlyphVariant.Bold);
        return (_fontAtlas.Glyphs, GlyphVariant.Regular);
    }

    private static bool IsMonospaceFamily(string family)
    {
        return family.Equals("monospace", StringComparison.OrdinalIgnoreCase)
            || family.Equals("ui-monospace", StringComparison.OrdinalIgnoreCase)
            || family.Contains("Mono", StringComparison.OrdinalIgnoreCase)
            || family.Contains("Courier", StringComparison.OrdinalIgnoreCase)
            || family.Contains("Consolas", StringComparison.OrdinalIgnoreCase)
            || family.Contains("Menlo", StringComparison.OrdinalIgnoreCase);
    }
}
