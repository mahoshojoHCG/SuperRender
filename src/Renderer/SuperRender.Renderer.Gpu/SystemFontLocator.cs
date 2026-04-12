using FreeTypeSharp;
using SuperRender.Document.Style;

namespace SuperRender.Renderer.Gpu;

/// <summary>
/// Scans system font directories and builds a lookup table of font family name to file paths.
/// Uses FreeType to read the actual family name from font files.
/// </summary>
public sealed class SystemFontLocator : IDisposable
{
    public record FontFamilyEntry(
        string FamilyName,
        string? RegularPath,
        string? BoldPath,
        string? ItalicPath,
        string? BoldItalicPath,
        long RegularFaceIndex = 0,
        long BoldFaceIndex = 0,
        long ItalicFaceIndex = 0,
        long BoldItalicFaceIndex = 0);

    private readonly Dictionary<string, MutableEntry> _families = new(StringComparer.OrdinalIgnoreCase);
    private FreeTypeLibrary? _lib;

    public SystemFontLocator()
    {
        _lib = new FreeTypeLibrary();
        ScanSystemFonts();
        _lib.Dispose();
        _lib = null;
    }

    /// <summary>
    /// Returns true if the given font family name is available on the system.
    /// </summary>
    public bool HasFamily(string familyName)
        => _families.ContainsKey(familyName);

    /// <summary>
    /// Finds a font family entry by name (case-insensitive).
    /// </summary>
    public FontFamilyEntry? FindFamily(string familyName)
    {
        if (_families.TryGetValue(familyName, out var entry))
            return entry.ToRecord();
        return null;
    }

    /// <summary>
    /// All available font family names discovered on the system.
    /// </summary>
    public IReadOnlyCollection<string> AvailableFamilies
        => _families.Keys;

    /// <summary>
    /// Resolves a CSS font-family list to a font family entry.
    /// Walks the list in order, resolving generic families, and returns the first match.
    /// </summary>
    public FontFamilyEntry? Resolve(IReadOnlyList<string> fontFamilies)
    {
        foreach (var family in fontFamilies)
        {
            if (FontFamilyParser.IsGenericFamily(family))
            {
                var defaultName = GenericFontFamilies.GetDefault(family);
                if (defaultName != null && _families.TryGetValue(defaultName, out var genericEntry))
                    return genericEntry.ToRecord();
            }
            else if (_families.TryGetValue(family, out var entry))
            {
                return entry.ToRecord();
            }
        }

        return null;
    }

    public void Dispose()
    {
        _lib?.Dispose();
    }

    private void ScanSystemFonts()
    {
        var dirs = GetSystemFontDirectories();
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext is ".ttf" or ".otf" or ".ttc")
                        TryRegisterFont(file);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't read
            }
            catch (DirectoryNotFoundException)
            {
                // Directory disappeared between check and enumeration
            }
        }
    }

    private unsafe void TryRegisterFont(string filePath)
    {
        if (_lib == null) return;

        try
        {
            FT_FaceRec_* facePtr;
            fixed (byte* pPath = System.Text.Encoding.UTF8.GetBytes(filePath + "\0"))
            {
                var error = FT.FT_New_Face(_lib.Native, pPath, 0, &facePtr);
                if (error != FT_Error.FT_Err_Ok)
                    return;
            }

            long numFaces = facePtr->num_faces;
            RegisterFaceInfo(facePtr, filePath, 0);
            FT.FT_Done_Face(facePtr);

            // For TTC collections, scan additional faces
            for (nint i = 1; i < numFaces; i++)
            {
                fixed (byte* pPath = System.Text.Encoding.UTF8.GetBytes(filePath + "\0"))
                {
                    var error = FT.FT_New_Face(_lib.Native, pPath, i, &facePtr);
                    if (error != FT_Error.FT_Err_Ok)
                        continue;
                }

                RegisterFaceInfo(facePtr, filePath, i);
                FT.FT_Done_Face(facePtr);
            }
        }
        catch
        {
            // Skip fonts that fail to load
        }
    }

    private unsafe void RegisterFaceInfo(FT_FaceRec_* facePtr, string filePath, long faceIndex)
    {
        var familyName = facePtr->family_name == null
            ? null
            : System.Runtime.InteropServices.Marshal.PtrToStringAnsi((nint)facePtr->family_name);

        if (string.IsNullOrWhiteSpace(familyName))
            return;

        var styleName = facePtr->style_name == null
            ? ""
            : System.Runtime.InteropServices.Marshal.PtrToStringAnsi((nint)facePtr->style_name) ?? "";

        var styleUpper = styleName.ToUpperInvariant();
        bool isBold = styleUpper.Contains("BOLD");
        bool isItalic = styleUpper.Contains("ITALIC") || styleUpper.Contains("OBLIQUE");

        if (!_families.TryGetValue(familyName, out var entry))
        {
            entry = new MutableEntry(familyName);
            _families[familyName] = entry;
        }

        if (isBold && isItalic)
        {
            entry.BoldItalicPath ??= filePath;
            if (entry.BoldItalicPath == filePath) entry.BoldItalicFaceIndex = faceIndex;
        }
        else if (isBold)
        {
            entry.BoldPath ??= filePath;
            if (entry.BoldPath == filePath) entry.BoldFaceIndex = faceIndex;
        }
        else if (isItalic)
        {
            entry.ItalicPath ??= filePath;
            if (entry.ItalicPath == filePath) entry.ItalicFaceIndex = faceIndex;
        }
        else
        {
            entry.RegularPath ??= filePath;
            if (entry.RegularPath == filePath) entry.RegularFaceIndex = faceIndex;
        }
    }

    private static string[] GetSystemFontDirectories()
    {
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return
            [
                "/System/Library/Fonts",
                "/Library/Fonts",
                Path.Combine(home, "Library/Fonts"),
            ];
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return
            [
                @"C:\Windows\Fonts",
                Path.Combine(localAppData, @"Microsoft\Windows\Fonts"),
            ];
        }

        // Linux
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return
        [
            "/usr/share/fonts",
            "/usr/local/share/fonts",
            Path.Combine(homeDir, ".local/share/fonts"),
        ];
    }

    private sealed class MutableEntry(string familyName)
    {
        public string FamilyName { get; } = familyName;
        public string? RegularPath { get; set; }
        public string? BoldPath { get; set; }
        public string? ItalicPath { get; set; }
        public string? BoldItalicPath { get; set; }
        public long RegularFaceIndex { get; set; }
        public long BoldFaceIndex { get; set; }
        public long ItalicFaceIndex { get; set; }
        public long BoldItalicFaceIndex { get; set; }

        public FontFamilyEntry ToRecord() => new(
            FamilyName,
            RegularPath,
            BoldPath,
            ItalicPath,
            BoldItalicPath,
            RegularFaceIndex,
            BoldFaceIndex,
            ItalicFaceIndex,
            BoldItalicFaceIndex);
    }
}
