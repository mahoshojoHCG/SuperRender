using FreeTypeSharp;

namespace SuperRender.Gpu;

public static class FontAtlasGenerator
{
    public const int AtlasWidth = 1024;
    public const int AtlasHeight = 2048; // Increased to fit regular + bold + monospace
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

    public static byte[] GenerateAtlas(
        out Dictionary<char, GlyphInfo> glyphs,
        out Dictionary<char, GlyphInfo> boldGlyphs,
        out Dictionary<char, GlyphInfo> monoGlyphs,
        float contentScale = 1.0f)
    {
        glyphs = new Dictionary<char, GlyphInfo>();
        boldGlyphs = new Dictionary<char, GlyphInfo>();
        monoGlyphs = new Dictionary<char, GlyphInfo>();
        float renderSize = BaseFontSize * Math.Max(contentScale, 1.0f);
        var pixels = new byte[AtlasWidth * AtlasHeight];

        var lib = new FreeTypeLibrary();
        var fontPath = FindSystemFont();

        if (fontPath == null)
        {
            Console.WriteLine("Warning: No system font found. Using fallback bitmap font.");
            var fallback = GenerateFallbackAtlas(out glyphs);
            boldGlyphs = glyphs;
            monoGlyphs = glyphs;
            return fallback;
        }

        Console.WriteLine($"Using system font: {fontPath}");

        // Generate regular glyphs in top portion of atlas
        int nextRowY = GenerateVariantGlyphs(lib, fontPath, renderSize, pixels, glyphs, 0);

        // Generate bold glyphs
        var boldPath = FindBoldFont() ?? fontPath;
        Console.WriteLine($"Using bold font: {boldPath}");
        nextRowY = GenerateVariantGlyphs(lib, boldPath, renderSize, pixels, boldGlyphs, nextRowY + Padding);
        if (boldGlyphs.Count == 0) boldGlyphs = glyphs;

        // Generate monospace glyphs
        var monoPath = FindMonospaceFont() ?? fontPath;
        Console.WriteLine($"Using monospace font: {monoPath}");
        GenerateVariantGlyphs(lib, monoPath, renderSize, pixels, monoGlyphs, nextRowY + Padding);
        if (monoGlyphs.Count == 0) monoGlyphs = glyphs;

        lib.Dispose();
        AtlasRenderSize = renderSize;
        Console.WriteLine($"Font atlas generated: {glyphs.Count}+{boldGlyphs.Count}+{monoGlyphs.Count} glyphs, renderSize={renderSize:F0}");
        return pixels;
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
    public static float AtlasRenderSize { get; private set; } = BaseFontSize;

    /// <summary>
    /// Font ascent in pixels at <see cref="AtlasRenderSize"/>. Set after GenerateAtlas is called.
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

    private static string? FindBoldFont()
    {
        if (OperatingSystem.IsMacOS())
        {
            // Try common bold variants
            foreach (var path in new[]
            {
                "/System/Library/Fonts/Helvetica.ttc", // face index 1 is often bold; for now use same file
                "/Library/Fonts/Arial Bold.ttf",
                "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
            })
            {
                if (File.Exists(path)) return path;
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            foreach (var path in new[]
            {
                @"C:\Windows\Fonts\segoeuib.ttf",
                @"C:\Windows\Fonts\arialbd.ttf",
                @"C:\Windows\Fonts\verdanab.ttf",
            })
            {
                if (File.Exists(path)) return path;
            }
        }
        else // Linux
        {
            foreach (var path in new[]
            {
                "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
                "/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf",
            })
            {
                if (File.Exists(path)) return path;
            }
        }
        return null;
    }

    private static string? FindMonospaceFont()
    {
        if (OperatingSystem.IsMacOS())
        {
            foreach (var path in new[]
            {
                "/System/Library/Fonts/Menlo.ttc",
                "/System/Library/Fonts/Monaco.ttf",
                "/System/Library/Fonts/SFMono-Regular.otf",
                "/System/Library/Fonts/Courier.ttc",
                "/Library/Fonts/Courier New.ttf",
            })
            {
                if (File.Exists(path)) return path;
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            foreach (var path in new[]
            {
                @"C:\Windows\Fonts\consola.ttf",
                @"C:\Windows\Fonts\cour.ttf",
                @"C:\Windows\Fonts\lucon.ttf",
            })
            {
                if (File.Exists(path)) return path;
            }
        }
        else // Linux
        {
            foreach (var path in new[]
            {
                "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
                "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
                "/usr/share/fonts/truetype/noto/NotoSansMono-Regular.ttf",
            })
            {
                if (File.Exists(path)) return path;
            }
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
