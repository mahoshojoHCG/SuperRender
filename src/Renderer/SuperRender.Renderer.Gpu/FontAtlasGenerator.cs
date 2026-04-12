using FreeTypeSharp;
using Microsoft.Extensions.Logging;

namespace SuperRender.Renderer.Gpu;

public static class FontAtlasGenerator
{
    public const int AtlasWidth = 2048;
    public const int AtlasHeight = 4096;
    public const float BaseFontSize = 32f;

    private const int FirstChar = 32;
    private const int LastChar = 126;
    private const int GlyphCount = LastChar - FirstChar + 1; // 95
    private const int Padding = 2;

    /// <summary>
    /// Generates a font atlas using a <see cref="SystemFontLocator"/> to discover fonts.
    /// Falls back to a minimal bitmap font if no system fonts are found.
    /// </summary>
    public static byte[] GenerateAtlas(
        out Dictionary<char, GlyphInfo> glyphs,
        out Dictionary<char, GlyphInfo> boldGlyphs,
        out Dictionary<char, GlyphInfo> monoGlyphs,
        float contentScale = 1.0f,
        ILogger? logger = null)
    {
        using var locator = new SystemFontLocator();

        // Resolve sans-serif for regular and bold
        var sansEntry = locator.Resolve(["sans-serif"]);
        string? regularPath = sansEntry?.RegularPath;
        string? boldPath = sansEntry?.BoldPath ?? regularPath;

        // Resolve monospace
        var monoEntry = locator.Resolve(["monospace"]);
        string? monoPath = monoEntry?.RegularPath;

        if (regularPath == null)
        {
            logger?.LogWarning("No system font found. Using fallback bitmap font.");
            var fallback = GenerateFallbackAtlas(out glyphs);
            boldGlyphs = glyphs;
            monoGlyphs = glyphs;
            return fallback;
        }

        return GenerateAtlas(
            regularPath,
            boldPath ?? regularPath,
            monoPath ?? regularPath,
            out glyphs, out boldGlyphs, out monoGlyphs,
            contentScale,
            logger);
    }

    /// <summary>
    /// Generates a font atlas using explicit font file paths.
    /// </summary>
    public static byte[] GenerateAtlas(
        string regularFontPath,
        string boldFontPath,
        string monoFontPath,
        out Dictionary<char, GlyphInfo> glyphs,
        out Dictionary<char, GlyphInfo> boldGlyphs,
        out Dictionary<char, GlyphInfo> monoGlyphs,
        float contentScale = 1.0f,
        ILogger? logger = null)
    {
        glyphs = new Dictionary<char, GlyphInfo>();
        boldGlyphs = new Dictionary<char, GlyphInfo>();
        monoGlyphs = new Dictionary<char, GlyphInfo>();
        float renderSize = BaseFontSize * Math.Max(contentScale, 1.0f);
        var pixels = new byte[AtlasWidth * AtlasHeight];

        var lib = new FreeTypeLibrary();

        logger?.LogInformation("Using system font: {FontPath}", regularFontPath);

        // Generate regular glyphs in top portion of atlas
        int nextRowY = GenerateVariantGlyphs(lib, regularFontPath, renderSize, pixels, glyphs, 0);

        // Generate bold glyphs
        logger?.LogInformation("Using bold font: {FontPath}", boldFontPath);
        nextRowY = GenerateVariantGlyphs(lib, boldFontPath, renderSize, pixels, boldGlyphs, nextRowY + Padding);
        if (boldGlyphs.Count == 0) boldGlyphs = glyphs;

        // Generate monospace glyphs
        logger?.LogInformation("Using monospace font: {FontPath}", monoFontPath);
        GenerateVariantGlyphs(lib, monoFontPath, renderSize, pixels, monoGlyphs, nextRowY + Padding);
        if (monoGlyphs.Count == 0) monoGlyphs = glyphs;

        lib.Dispose();
        AtlasRenderSize = renderSize;
        logger?.LogInformation("Font atlas generated: {RegularCount}+{BoldCount}+{MonoCount} glyphs, renderSize={RenderSize:F0}",
            glyphs.Count, boldGlyphs.Count, monoGlyphs.Count, renderSize);
        return pixels;
    }

    /// <summary>
    /// Generates a font atlas using a <see cref="SystemFontLocator"/> that has already
    /// been created and populated. Avoids redundant font scanning when the locator
    /// is shared.
    /// </summary>
    public static byte[] GenerateAtlas(
        SystemFontLocator locator,
        out Dictionary<char, GlyphInfo> glyphs,
        out Dictionary<char, GlyphInfo> boldGlyphs,
        out Dictionary<char, GlyphInfo> monoGlyphs,
        float contentScale = 1.0f,
        ILogger? logger = null)
    {
        // Resolve sans-serif for regular and bold
        var sansEntry = locator.Resolve(["sans-serif"]);
        string? regularPath = sansEntry?.RegularPath;
        string? boldPath = sansEntry?.BoldPath ?? regularPath;

        // Resolve monospace
        var monoEntry = locator.Resolve(["monospace"]);
        string? monoPath = monoEntry?.RegularPath;

        if (regularPath == null)
        {
            logger?.LogWarning("No system font found. Using fallback bitmap font.");
            var fallback = GenerateFallbackAtlas(out glyphs);
            boldGlyphs = glyphs;
            monoGlyphs = glyphs;
            return fallback;
        }

        return GenerateAtlas(
            regularPath,
            boldPath ?? regularPath,
            monoPath ?? regularPath,
            out glyphs, out boldGlyphs, out monoGlyphs,
            contentScale,
            logger);
    }

    private static unsafe int GenerateVariantGlyphs(
        FreeTypeLibrary lib, string fontPath, float renderSize,
        byte[] pixels, Dictionary<char, GlyphInfo> glyphs, int startY)
    {
        FT_FaceRec_* facePtr;
        fixed (byte* pPath = System.Text.Encoding.UTF8.GetBytes(fontPath + "\0"))
        {
            var error = FT.FT_New_Face(lib.Native, pPath, 0, &facePtr);
            if (error != FT_Error.FT_Err_Ok)
                return startY;
        }

        FT.FT_Set_Pixel_Sizes(facePtr, 0, (uint)renderSize);

        // Pass 1: measure max ascent
        float maxAscent = 0;
        for (int i = 0; i < GlyphCount; i++)
        {
            char c = (char)(FirstChar + i);
            var glyphIndex = FT.FT_Get_Char_Index(facePtr, c);
            if (FT.FT_Load_Glyph(facePtr, glyphIndex, FT_LOAD.FT_LOAD_DEFAULT) != FT_Error.FT_Err_Ok)
                continue;
            float bearingY = facePtr->glyph->metrics.horiBearingY / 64f;
            if (bearingY > maxAscent) maxAscent = bearingY;
        }

        if (maxAscent > Ascent) Ascent = maxAscent;

        // Pass 2: render and pack
        int cursorX = Padding;
        int cursorY = startY + Padding;
        int rowHeight = 0;

        for (int i = 0; i < GlyphCount; i++)
        {
            char c = (char)(FirstChar + i);
            var glyphIndex = FT.FT_Get_Char_Index(facePtr, c);
            if (FT.FT_Load_Glyph(facePtr, glyphIndex, FT_LOAD.FT_LOAD_DEFAULT) != FT_Error.FT_Err_Ok)
                continue;
            if (FT.FT_Render_Glyph(facePtr->glyph, FT_Render_Mode_.FT_RENDER_MODE_NORMAL) != FT_Error.FT_Err_Ok)
                continue;

            var glyph = facePtr->glyph;
            var bitmap = glyph->bitmap;
            int bmpW = (int)bitmap.width;
            int bmpH = (int)bitmap.rows;
            int bearingX = glyph->bitmap_left;
            int bearingY = glyph->bitmap_top;
            float advance = glyph->advance.x / 64f;

            int cellW = bmpW + Padding * 2;
            int cellH = bmpH + Padding * 2;

            if (cursorX + cellW > AtlasWidth)
            {
                cursorX = Padding;
                cursorY += rowHeight + Padding;
                rowHeight = 0;
            }

            if (cursorY + cellH > AtlasHeight) break;
            rowHeight = Math.Max(rowHeight, cellH);

            if (bmpW > 0 && bmpH > 0 && bitmap.buffer != null)
            {
                for (int row = 0; row < bmpH; row++)
                {
                    int dstX = cursorX + Padding;
                    int dstY = cursorY + Padding + row;
                    if (dstY >= AtlasHeight) break;
                    int copyWidth = Math.Min(bmpW, AtlasWidth - dstX);
                    if (copyWidth <= 0) continue;
                    var src = new ReadOnlySpan<byte>(bitmap.buffer + row * bitmap.pitch, copyWidth);
                    var dst = pixels.AsSpan(dstY * AtlasWidth + dstX, copyWidth);
                    src.CopyTo(dst);
                }
            }

            float u0 = (cursorX + Padding) / (float)AtlasWidth;
            float v0 = (cursorY + Padding) / (float)AtlasHeight;
            float u1 = (cursorX + Padding + bmpW) / (float)AtlasWidth;
            float v1 = (cursorY + Padding + bmpH) / (float)AtlasHeight;

            glyphs[c] = new GlyphInfo
            {
                U0 = u0, V0 = v0, U1 = u1, V1 = v1,
                Width = bmpW, Height = bmpH,
                AdvanceX = advance > 0 ? advance : BaseFontSize * 0.3f,
                OffsetX = bearingX,
                OffsetY = maxAscent - bearingY,
            };

            cursorX += cellW;
        }

        FT.FT_Done_Face(facePtr);
        return cursorY + rowHeight;
    }

    /// <summary>
    /// The actual pixel size used to render glyphs into the atlas.
    /// On HiDPI this is <c>BaseFontSize * contentScale</c>.
    /// TextRenderer and BitmapFontTextMeasurer scale against this value.
    /// </summary>
    public static float AtlasRenderSize { get; internal set; } = BaseFontSize;

    /// <summary>
    /// Font ascent in pixels at <see cref="AtlasRenderSize"/>. Set after GenerateAtlas is called.
    /// </summary>
    public static float Ascent { get; internal set; } = BaseFontSize * 0.8f;

    /// <summary>
    /// Minimal 5x7 bitmap fallback if no system fonts are available.
    /// </summary>
    private static byte[] GenerateFallbackAtlas(out Dictionary<char, GlyphInfo> glyphs)
    {
        glyphs = new Dictionary<char, GlyphInfo>();
        var pixels = new byte[AtlasWidth * AtlasHeight];

        int cellW = 16, cellH = 20;
        int cols = AtlasWidth / cellW;

        for (int i = 0; i < GlyphCount; i++)
        {
            char c = (char)(FirstChar + i);
            int col = i % cols;
            int row = i / cols;
            int cx = col * cellW;
            int cy = row * cellH;

            // Simple filled rectangle for each glyph (very basic)
            if (c != ' ')
            {
                for (int y = 2; y < cellH - 2; y++)
                    for (int x = 2; x < cellW - 2; x++)
                        pixels[(cy + y) * AtlasWidth + (cx + x)] = 180;
            }

            glyphs[c] = new GlyphInfo
            {
                U0 = (cx + 2f) / AtlasWidth,
                V0 = (cy + 2f) / AtlasHeight,
                U1 = (cx + cellW - 2f) / AtlasWidth,
                V1 = (cy + cellH - 2f) / AtlasHeight,
                Width = cellW - 4,
                Height = cellH - 4,
                AdvanceX = cellW - 2,
                OffsetX = 0,
                OffsetY = 0,
            };
        }

        return pixels;
    }
}
