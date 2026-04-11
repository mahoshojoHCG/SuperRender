using FreeTypeSharp;

namespace SuperRender.Gpu;

/// <summary>
/// Manages a font atlas texture with on-demand glyph rendering.
/// Pre-renders ASCII glyphs for regular/bold/monospace variants at startup,
/// and dynamically renders additional glyphs (CJK, symbols, etc.) as needed.
/// </summary>
public sealed class FontAtlas : IDisposable
{
    public int AtlasWidth { get; }
    public int AtlasHeight { get; }
    public byte[] PixelData { get; }
    public Dictionary<char, GlyphInfo> Glyphs { get; }
    public Dictionary<char, GlyphInfo> BoldGlyphs { get; }
    public Dictionary<char, GlyphInfo> MonospaceGlyphs { get; }

    /// <summary>
    /// True when new glyphs have been rendered since the last GPU upload.
    /// </summary>
    public bool IsDirty { get; private set; }

    public void ClearDirty() => IsDirty = false;

    // FreeType state for on-demand rendering
    private FreeTypeLibrary? _lib;
    private readonly string? _regularFontPath;
    private readonly string? _boldFontPath;
    private readonly string? _monoFontPath;
    private readonly string? _cjkFontPath;
    private readonly float _renderSize;
    private float _maxAscent;

    // Unified packing cursor (continues after initial ASCII rendering)
    private int _cursorX;
    private int _cursorY;
    private int _rowHeight;

    private const int Padding = 2;

    public FontAtlas(float contentScale = 1.0f)
    {
        AtlasWidth = FontAtlasGenerator.AtlasWidth;
        AtlasHeight = FontAtlasGenerator.AtlasHeight;
        PixelData = FontAtlasGenerator.GenerateAtlas(
            out var glyphs, out var boldGlyphs, out var monoGlyphs, contentScale);
        Glyphs = glyphs;
        BoldGlyphs = boldGlyphs;
        MonospaceGlyphs = monoGlyphs;
    }

    public FontAtlas(SystemFontLocator locator, float contentScale = 1.0f)
    {
        AtlasWidth = FontAtlasGenerator.AtlasWidth;
        AtlasHeight = FontAtlasGenerator.AtlasHeight;
        _renderSize = FontAtlasGenerator.BaseFontSize * Math.Max(contentScale, 1.0f);

        Glyphs = new Dictionary<char, GlyphInfo>();
        BoldGlyphs = new Dictionary<char, GlyphInfo>();
        MonospaceGlyphs = new Dictionary<char, GlyphInfo>();

        // Resolve font paths
        var sansEntry = locator.Resolve(["sans-serif"]);
        _regularFontPath = sansEntry?.RegularPath;
        _boldFontPath = sansEntry?.BoldPath ?? _regularFontPath;

        var monoEntry = locator.Resolve(["monospace"]);
        _monoFontPath = monoEntry?.RegularPath ?? _regularFontPath;

        // Resolve CJK fallback font
        foreach (var cjkName in GenericFontFamilies.GetCjkFallbacks())
        {
            var cjkEntry = locator.FindFamily(cjkName);
            if (cjkEntry?.RegularPath != null)
            {
                _cjkFontPath = cjkEntry.RegularPath;
                Console.WriteLine($"Using CJK font: {cjkName} ({_cjkFontPath})");
                break;
            }
        }

        if (_regularFontPath == null)
        {
            Console.WriteLine("Warning: No system font found. Using fallback bitmap font.");
            PixelData = FontAtlasGenerator.GenerateAtlas(
                out var glyphs, out var boldGlyphs, out var monoGlyphs, contentScale);
            Glyphs = glyphs;
            BoldGlyphs = boldGlyphs;
            MonospaceGlyphs = monoGlyphs;
            return;
        }

        Console.WriteLine($"Using system font: {_regularFontPath}");
        Console.WriteLine($"Using bold font: {_boldFontPath}");
        Console.WriteLine($"Using monospace font: {_monoFontPath}");

        PixelData = new byte[AtlasWidth * AtlasHeight];
        _lib = new FreeTypeLibrary();

        // Pre-render ASCII for all 3 variants
        PreRenderAscii();

        FontAtlasGenerator.AtlasRenderSize = _renderSize;
        Console.WriteLine($"Font atlas generated: {Glyphs.Count}+{BoldGlyphs.Count}+{MonospaceGlyphs.Count} glyphs, renderSize={_renderSize:F0}");
    }

    /// <summary>
    /// Ensures a glyph for the given character exists in the specified variant's dictionary.
    /// Returns true if the glyph is available (either already cached or just rendered).
    /// </summary>
    public bool EnsureGlyph(char c, GlyphVariant variant)
    {
        var dict = GetDictionary(variant);
        if (dict.ContainsKey(c))
            return true;

        if (_lib == null || _regularFontPath == null)
            return false;

        var fontPath = variant switch
        {
            GlyphVariant.Bold => _boldFontPath,
            GlyphVariant.Monospace => _monoFontPath,
            _ => _regularFontPath,
        };

        if (fontPath != null && TryRenderGlyph(c, fontPath, dict))
            return true;

        // Try CJK fallback
        if (_cjkFontPath != null && TryRenderGlyph(c, _cjkFontPath, dict))
            return true;

        return false;
    }

    /// <summary>
    /// Ensures all characters in the text exist in the atlas for the given variant.
    /// </summary>
    public void EnsureGlyphs(string text, GlyphVariant variant)
    {
        var dict = GetDictionary(variant);
        foreach (char c in text)
        {
            if (!dict.ContainsKey(c))
                EnsureGlyph(c, variant);
        }
    }

    private Dictionary<char, GlyphInfo> GetDictionary(GlyphVariant variant) => variant switch
    {
        GlyphVariant.Bold => BoldGlyphs,
        GlyphVariant.Monospace => MonospaceGlyphs,
        _ => Glyphs,
    };

    private void PreRenderAscii()
    {
        if (_lib == null) return;

        // Measure ascent from regular font first
        MeasureAscent(_regularFontPath!);

        // Pre-render regular ASCII
        for (int c = 32; c <= 126; c++)
            TryRenderGlyph((char)c, _regularFontPath!, Glyphs);

        // Pre-render bold ASCII
        if (_boldFontPath != null)
        {
            for (int c = 32; c <= 126; c++)
                TryRenderGlyph((char)c, _boldFontPath, BoldGlyphs);
        }
        if (BoldGlyphs.Count == 0)
            foreach (var kv in Glyphs) BoldGlyphs[kv.Key] = kv.Value;

        // Pre-render monospace ASCII
        if (_monoFontPath != null)
        {
            for (int c = 32; c <= 126; c++)
                TryRenderGlyph((char)c, _monoFontPath, MonospaceGlyphs);
        }
        if (MonospaceGlyphs.Count == 0)
            foreach (var kv in Glyphs) MonospaceGlyphs[kv.Key] = kv.Value;

        // Pre-render common symbols (bullet, em-dash, etc.)
        char[] extraChars = ['\u2022', '\u2013', '\u2014', '\u2018', '\u2019', '\u201C', '\u201D', '\u2026'];
        foreach (char c in extraChars)
        {
            TryRenderGlyph(c, _regularFontPath!, Glyphs);
            if (_boldFontPath != null) TryRenderGlyph(c, _boldFontPath, BoldGlyphs);
        }

        // Don't mark initial render as dirty — the GPU texture will be created from PixelData
        IsDirty = false;
    }

    private unsafe void MeasureAscent(string fontPath)
    {
        if (_lib == null) return;

        FT_FaceRec_* facePtr;
        fixed (byte* pPath = System.Text.Encoding.UTF8.GetBytes(fontPath + "\0"))
        {
            if (FT.FT_New_Face(_lib.Native, pPath, 0, &facePtr) != FT_Error.FT_Err_Ok)
                return;
        }

        FT.FT_Set_Pixel_Sizes(facePtr, 0, (uint)_renderSize);

        float maxAscent = 0;
        for (int c = 32; c <= 126; c++)
        {
            var idx = FT.FT_Get_Char_Index(facePtr, (uint)c);
            if (FT.FT_Load_Glyph(facePtr, idx, FT_LOAD.FT_LOAD_DEFAULT) != FT_Error.FT_Err_Ok)
                continue;
            float bearingY = facePtr->glyph->metrics.horiBearingY / 64f;
            if (bearingY > maxAscent) maxAscent = bearingY;
        }

        _maxAscent = maxAscent;
        if (maxAscent > FontAtlasGenerator.Ascent)
            FontAtlasGenerator.Ascent = maxAscent;

        FT.FT_Done_Face(facePtr);
    }

    // Cache of opened FreeType faces to avoid reopening for each glyph
    private readonly Dictionary<string, nint> _faceCache = new();

    private unsafe nint GetOrOpenFace(string fontPath)
    {
        if (_faceCache.TryGetValue(fontPath, out var cached))
            return cached;

        if (_lib == null) return 0;

        FT_FaceRec_* facePtr;
        fixed (byte* pPath = System.Text.Encoding.UTF8.GetBytes(fontPath + "\0"))
        {
            if (FT.FT_New_Face(_lib.Native, pPath, 0, &facePtr) != FT_Error.FT_Err_Ok)
                return 0;
        }

        FT.FT_Set_Pixel_Sizes(facePtr, 0, (uint)_renderSize);

        var ptr = (nint)facePtr;
        _faceCache[fontPath] = ptr;
        return ptr;
    }

    private unsafe bool TryRenderGlyph(char c, string fontPath, Dictionary<char, GlyphInfo> dict)
    {
        if (dict.ContainsKey(c)) return true;

        var faceNative = GetOrOpenFace(fontPath);
        if (faceNative == 0) return false;

        var facePtr = (FT_FaceRec_*)faceNative;
        var glyphIndex = FT.FT_Get_Char_Index(facePtr, c);
        if (glyphIndex == 0) return false; // glyph not in this font

        if (FT.FT_Load_Glyph(facePtr, glyphIndex, FT_LOAD.FT_LOAD_DEFAULT) != FT_Error.FT_Err_Ok)
            return false;
        if (FT.FT_Render_Glyph(facePtr->glyph, FT_Render_Mode_.FT_RENDER_MODE_NORMAL) != FT_Error.FT_Err_Ok)
            return false;

        var glyph = facePtr->glyph;
        var bitmap = glyph->bitmap;
        int bmpW = (int)bitmap.width;
        int bmpH = (int)bitmap.rows;
        int bearingX = glyph->bitmap_left;
        int bearingY = glyph->bitmap_top;
        float advance = glyph->advance.x / 64f;

        int cellW = bmpW + Padding * 2;
        int cellH = bmpH + Padding * 2;

        // Pack into atlas
        if (_cursorX + cellW > AtlasWidth)
        {
            _cursorX = Padding;
            _cursorY += _rowHeight + Padding;
            _rowHeight = 0;
        }

        if (_cursorY + cellH > AtlasHeight)
            return false; // atlas full

        _rowHeight = Math.Max(_rowHeight, cellH);

        if (bmpW > 0 && bmpH > 0 && bitmap.buffer != null)
        {
            for (int row = 0; row < bmpH; row++)
            {
                for (int col = 0; col < bmpW; col++)
                {
                    int srcIdx = row * bitmap.pitch + col;
                    int dstX = _cursorX + Padding + col;
                    int dstY = _cursorY + Padding + row;
                    if (dstX < AtlasWidth && dstY < AtlasHeight)
                        PixelData[dstY * AtlasWidth + dstX] = bitmap.buffer[srcIdx];
                }
            }
        }

        float u0 = (_cursorX + Padding) / (float)AtlasWidth;
        float v0 = (_cursorY + Padding) / (float)AtlasHeight;
        float u1 = (_cursorX + Padding + bmpW) / (float)AtlasWidth;
        float v1 = (_cursorY + Padding + bmpH) / (float)AtlasHeight;

        float ascent = _maxAscent > 0 ? _maxAscent : _renderSize * 0.8f;

        dict[c] = new GlyphInfo
        {
            U0 = u0, V0 = v0, U1 = u1, V1 = v1,
            Width = bmpW, Height = bmpH,
            AdvanceX = advance > 0 ? advance : _renderSize * 0.3f,
            OffsetX = bearingX,
            OffsetY = ascent - bearingY,
        };

        _cursorX += cellW;
        IsDirty = true;
        return true;
    }

    public void Dispose()
    {
        if (_lib != null)
        {
            foreach (var face in _faceCache.Values)
            {
                unsafe
                {
                    FT.FT_Done_Face((FT_FaceRec_*)face);
                }
            }
            _faceCache.Clear();
            _lib.Dispose();
            _lib = null;
        }
    }
}

public enum GlyphVariant
{
    Regular,
    Bold,
    Monospace,
}
