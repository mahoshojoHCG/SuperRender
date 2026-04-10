using FreeTypeSharp;

namespace SuperRender.Demo;

public static class FontAtlasGenerator
{
    public const int AtlasWidth = 1024;
    public const int AtlasHeight = 1024;
    public const float BaseFontSize = 32f;

    private const int FirstChar = 32;
    private const int LastChar = 126;
    private const int GlyphCount = LastChar - FirstChar + 1; // 95
    private const int Padding = 2;

    // Per-platform fallback font paths, tried in order.
    private static readonly string[][] MacFontPaths =
    [
        ["/System/Library/Fonts/Helvetica.ttc"],
        ["/System/Library/Fonts/SFNSText.ttf", "/System/Library/Fonts/SFNS.ttf"],
        ["/Library/Fonts/Arial.ttf", "/Library/Fonts/Arial Unicode.ttf"],
        ["/System/Library/Fonts/HelveticaNeue.ttc"],
        ["/System/Library/Fonts/Geneva.ttf"],
        ["/System/Library/Fonts/LucidaGrande.ttc"],
        ["/System/Library/Fonts/Supplemental/Arial.ttf"],
        ["/System/Library/Fonts/Menlo.ttc"],
        ["/System/Library/Fonts/Monaco.ttf"],
    ];

    private static readonly string[][] WindowsFontPaths =
    [
        [@"C:\Windows\Fonts\segoeui.ttf"],
        [@"C:\Windows\Fonts\arial.ttf"],
        [@"C:\Windows\Fonts\verdana.ttf"],
        [@"C:\Windows\Fonts\tahoma.ttf"],
        [@"C:\Windows\Fonts\calibri.ttf"],
        [@"C:\Windows\Fonts\consola.ttf"],
    ];

    private static readonly string[][] LinuxFontPaths =
    [
        ["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"],
        ["/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"],
        ["/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf"],
        ["/usr/share/fonts/TTF/DejaVuSans.ttf"],
        ["/usr/share/fonts/noto/NotoSans-Regular.ttf"],
        ["/usr/share/fonts/liberation-sans/LiberationSans-Regular.ttf"],
        ["/usr/share/fonts/ubuntu/Ubuntu-R.ttf"],
        ["/usr/share/fonts/truetype/freefont/FreeSans.ttf"],
    ];

    public static byte[] GenerateAtlas(out Dictionary<char, GlyphInfo> glyphs)
    {
        glyphs = new Dictionary<char, GlyphInfo>();
        var pixels = new byte[AtlasWidth * AtlasHeight];

        var lib = new FreeTypeLibrary();
        var fontPath = FindSystemFont();

        if (fontPath == null)
        {
            Console.WriteLine("Warning: No system font found. Using fallback bitmap font.");
            return GenerateFallbackAtlas(out glyphs);
        }

        Console.WriteLine($"Using system font: {fontPath}");

        // Track the font ascent (max bearingY across all glyphs)
        float maxAscent = 0;

        unsafe
        {
            FT_FaceRec_* facePtr;
            fixed (byte* pPath = System.Text.Encoding.UTF8.GetBytes(fontPath + "\0"))
            {
                var error = FT.FT_New_Face(lib.Native, pPath, 0, &facePtr);
                if (error != FT_Error.FT_Err_Ok)
                {
                    Console.WriteLine($"FreeType error loading font: {error}");
                    return GenerateFallbackAtlas(out glyphs);
                }
            }

            // Set pixel size
            FT.FT_Set_Pixel_Sizes(facePtr, 0, (uint)BaseFontSize);

            // --- Pass 1: measure all glyphs to find max ascent ---
            for (int i = 0; i < GlyphCount; i++)
            {
                char c = (char)(FirstChar + i);
                var glyphIndex = FT.FT_Get_Char_Index(facePtr, c);
                if (FT.FT_Load_Glyph(facePtr, glyphIndex, FT_LOAD.FT_LOAD_DEFAULT) != FT_Error.FT_Err_Ok)
                    continue;
                float bearingY = facePtr->glyph->metrics.horiBearingY / 64f;
                if (bearingY > maxAscent) maxAscent = bearingY;
            }

            // --- Pass 2: render glyphs and build atlas ---
            int cursorX = Padding;
            int cursorY = Padding;
            int rowHeight = 0;

            for (int i = 0; i < GlyphCount; i++)
            {
                char c = (char)(FirstChar + i);

                var glyphIndex = FT.FT_Get_Char_Index(facePtr, c);
                var err = FT.FT_Load_Glyph(facePtr, glyphIndex, FT_LOAD.FT_LOAD_DEFAULT);
                if (err != FT_Error.FT_Err_Ok) continue;

                err = FT.FT_Render_Glyph(facePtr->glyph, FT_Render_Mode_.FT_RENDER_MODE_NORMAL);
                if (err != FT_Error.FT_Err_Ok) continue;

                var glyph = facePtr->glyph;
                var bitmap = glyph->bitmap;
                int bmpW = (int)bitmap.width;
                int bmpH = (int)bitmap.rows;
                int bearingX = glyph->bitmap_left;
                int bearingY = glyph->bitmap_top;
                float advance = glyph->advance.x / 64f;

                // Cell size includes padding
                int cellW = bmpW + Padding * 2;
                int cellH = bmpH + Padding * 2;

                // Wrap to next row if needed
                if (cursorX + cellW > AtlasWidth)
                {
                    cursorX = Padding;
                    cursorY += rowHeight + Padding;
                    rowHeight = 0;
                }

                if (cursorY + cellH > AtlasHeight)
                    break; // Atlas full

                rowHeight = Math.Max(rowHeight, cellH);

                // Copy glyph bitmap into atlas
                if (bmpW > 0 && bmpH > 0 && bitmap.buffer != null)
                {
                    for (int row = 0; row < bmpH; row++)
                    {
                        for (int col = 0; col < bmpW; col++)
                        {
                            int srcIdx = row * bitmap.pitch + col;
                            int dstX = cursorX + Padding + col;
                            int dstY = cursorY + Padding + row;
                            if (dstX < AtlasWidth && dstY < AtlasHeight)
                                pixels[dstY * AtlasWidth + dstX] = bitmap.buffer[srcIdx];
                        }
                    }
                }

                // Store glyph info
                float u0 = (cursorX + Padding) / (float)AtlasWidth;
                float v0 = (cursorY + Padding) / (float)AtlasHeight;
                float u1 = (cursorX + Padding + bmpW) / (float)AtlasWidth;
                float v1 = (cursorY + Padding + bmpH) / (float)AtlasHeight;

                // OffsetY: position relative to line top, not baseline.
                // maxAscent is the distance from baseline to top of tallest glyph.
                // bearingY is this glyph's distance from baseline to its top.
                // So (maxAscent - bearingY) = how far below the line top this glyph starts.
                glyphs[c] = new GlyphInfo
                {
                    U0 = u0,
                    V0 = v0,
                    U1 = u1,
                    V1 = v1,
                    Width = bmpW,
                    Height = bmpH,
                    AdvanceX = advance > 0 ? advance : BaseFontSize * 0.3f,
                    OffsetX = bearingX,
                    OffsetY = maxAscent - bearingY,
                };

                cursorX += cellW;
            }

            FT.FT_Done_Face(facePtr);
        }

        lib.Dispose();
        Ascent = maxAscent;
        Console.WriteLine($"Font atlas generated: {glyphs.Count} glyphs, ascent={maxAscent:F1}");
        return pixels;
    }

    /// <summary>
    /// Font ascent in pixels at BaseFontSize. Set after GenerateAtlas is called.
    /// </summary>
    public static float Ascent { get; private set; } = BaseFontSize * 0.8f;

    private static string? FindSystemFont()
    {
        var candidates = OperatingSystem.IsMacOS() ? MacFontPaths
            : OperatingSystem.IsWindows() ? WindowsFontPaths
            : LinuxFontPaths;

        foreach (var group in candidates)
        {
            foreach (var path in group)
            {
                if (File.Exists(path))
                    return path;
            }
        }

        // Brute-force search common directories
        var searchDirs = OperatingSystem.IsMacOS()
            ? new[] { "/System/Library/Fonts", "/Library/Fonts" }
            : OperatingSystem.IsWindows()
                ? new[] { @"C:\Windows\Fonts" }
                : new[] { "/usr/share/fonts" };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.ttf", SearchOption.AllDirectories))
                return file;
            foreach (var file in Directory.EnumerateFiles(dir, "*.ttc", SearchOption.AllDirectories))
                return file;
        }

        return null;
    }

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
