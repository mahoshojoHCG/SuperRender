namespace SuperRender.Renderer.Gpu;

/// <summary>
/// Maps CSS generic font family keywords to platform-specific real font family names.
/// </summary>
internal static class GenericFontFamilies
{
    /// <summary>
    /// Returns the platform-specific default font family name for a CSS generic family keyword,
    /// or null if the keyword is not recognized.
    /// </summary>
    public static string? GetDefault(string genericFamily)
    {
        var key = genericFamily.ToLowerInvariant();

        if (OperatingSystem.IsMacOS())
        {
            return key switch
            {
                "serif" => "Times",
                "sans-serif" => "Helvetica",
                "monospace" => "Menlo",
                "cursive" => "Apple Chancery",
                "fantasy" => "Papyrus",
                "system-ui" => "Helvetica",
                "ui-serif" => "Times",
                "ui-sans-serif" => "Helvetica",
                "ui-monospace" => "Menlo",
                "ui-rounded" => "Helvetica",
                _ => null,
            };
        }

        if (OperatingSystem.IsWindows())
        {
            return key switch
            {
                "serif" => "Times New Roman",
                "sans-serif" => "Segoe UI",
                "monospace" => "Consolas",
                "cursive" => "Comic Sans MS",
                "fantasy" => "Impact",
                "system-ui" => "Segoe UI",
                "ui-serif" => "Times New Roman",
                "ui-sans-serif" => "Segoe UI",
                "ui-monospace" => "Consolas",
                "ui-rounded" => "Segoe UI",
                _ => null,
            };
        }

        // Linux / other
        return key switch
        {
            "serif" => "DejaVu Serif",
            "sans-serif" => "DejaVu Sans",
            "monospace" => "DejaVu Sans Mono",
            "cursive" => "DejaVu Sans",
            "fantasy" => "DejaVu Sans",
            "system-ui" => "DejaVu Sans",
            "ui-serif" => "DejaVu Serif",
            "ui-sans-serif" => "DejaVu Sans",
            "ui-monospace" => "DejaVu Sans Mono",
            "ui-rounded" => "DejaVu Sans",
            _ => null,
        };
    }

    /// <summary>
    /// Returns a list of CJK font family names to try as fallback, in priority order.
    /// </summary>
    public static string[] GetCjkFallbacks()
    {
        if (OperatingSystem.IsMacOS())
        {
            return
            [
                "PingFang SC",
                "Hiragino Sans GB",
                "STHeiti",
                "Apple SD Gothic Neo",
                "Heiti SC",
                "Heiti TC",
            ];
        }

        if (OperatingSystem.IsWindows())
        {
            return
            [
                "Microsoft YaHei",
                "SimSun",
                "NSimSun",
                "Microsoft JhengHei",
                "Yu Gothic",
                "Malgun Gothic",
            ];
        }

        // Linux
        return
        [
            "Noto Sans CJK SC",
            "Noto Sans CJK",
            "WenQuanYi Micro Hei",
            "WenQuanYi Zen Hei",
            "Droid Sans Fallback",
            "Source Han Sans SC",
        ];
    }
}
