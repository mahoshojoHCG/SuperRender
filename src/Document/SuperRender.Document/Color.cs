namespace SuperRender.Document;

public readonly struct Color : IEquatable<Color>
{
    public float R { get; init; }
    public float G { get; init; }
    public float B { get; init; }
    public float A { get; init; }

    public Color(float r, float g, float b, float a)
    {
        R = r; G = g; B = b; A = a;
    }

    public static Color FromRgb(byte r, byte g, byte b)
        => new(r / 255f, g / 255f, b / 255f, 1f);

    public static Color FromRgba(byte r, byte g, byte b, byte a)
        => new(r / 255f, g / 255f, b / 255f, a / 255f);

    public static Color FromHex(string hex)
    {
        var h = hex.AsSpan();
        if (h.Length > 0 && h[0] == '#') h = h[1..];

        return h.Length switch
        {
            3 => FromRgb(
                (byte)(ParseHexDigit(h[0]) * 17),
                (byte)(ParseHexDigit(h[1]) * 17),
                (byte)(ParseHexDigit(h[2]) * 17)),
            6 => FromRgb(
                (byte)((ParseHexDigit(h[0]) << 4) | ParseHexDigit(h[1])),
                (byte)((ParseHexDigit(h[2]) << 4) | ParseHexDigit(h[3])),
                (byte)((ParseHexDigit(h[4]) << 4) | ParseHexDigit(h[5]))),
            8 => FromRgba(
                (byte)((ParseHexDigit(h[0]) << 4) | ParseHexDigit(h[1])),
                (byte)((ParseHexDigit(h[2]) << 4) | ParseHexDigit(h[3])),
                (byte)((ParseHexDigit(h[4]) << 4) | ParseHexDigit(h[5])),
                (byte)((ParseHexDigit(h[6]) << 4) | ParseHexDigit(h[7]))),
            _ => Black
        };
    }

    private static int ParseHexDigit(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => 0
    };

    public static Color FromName(string name)
        => NamedColors.TryGetValue(name.ToLowerInvariant(), out var color) ? color : Black;

    public static bool TryFromName(string name, out Color color)
        => NamedColors.TryGetValue(name.ToLowerInvariant(), out color);

    public static Color FromHsl(double h, double s, double l)
        => FromHsla(h, s, l, 1.0);

    public static Color FromHsla(double h, double s, double l, double a)
    {
        // Normalize hue to 0-360
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);
        a = Math.Clamp(a, 0, 1);

        // HSL to RGB conversion (CSS Color Level 4 algorithm)
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = l - c / 2;

        double r1, g1, b1;
        if (h < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        return new((float)(r1 + m), (float)(g1 + m), (float)(b1 + m), (float)a);
    }

    // --- HWB (CSS Color Level 4) ---

    public static Color FromHwb(double h, double w, double b)
        => FromHwba(h, w, b, 1.0);

    public static Color FromHwba(double h, double w, double b, double a)
    {
        h = ((h % 360) + 360) % 360;
        w = Math.Clamp(w, 0, 1);
        b = Math.Clamp(b, 0, 1);
        a = Math.Clamp(a, 0, 1);

        if (w + b >= 1)
        {
            float gray = (float)(w / (w + b));
            return new(gray, gray, gray, (float)a);
        }

        var hsl = FromHsla(h, 1.0, 0.5, 1.0);
        float scale = (float)(1 - w - b);
        float wf = (float)w;
        return new(
            Math.Clamp(hsl.R * scale + wf, 0, 1),
            Math.Clamp(hsl.G * scale + wf, 0, 1),
            Math.Clamp(hsl.B * scale + wf, 0, 1),
            (float)a);
    }

    // --- Lab / LCH (CSS Color Level 4, CIE Lab D50) ---

    public static Color FromLab(double l, double a, double b)
        => FromLaba(l, a, b, 1.0);

    public static Color FromLaba(double l, double a, double b, double alpha)
    {
        alpha = Math.Clamp(alpha, 0, 1);
        // Lab → XYZ (D50)
        LabToXyzD50(l, a, b, out double x, out double y, out double z);
        // D50 → D65 via Bradford
        XyzD50ToD65(x, y, z, out double xd65, out double yd65, out double zd65);
        // XYZ (D65) → linear sRGB → sRGB
        XyzD65ToLinearSrgb(xd65, yd65, zd65, out double lr, out double lg, out double lb);
        return new(
            (float)Math.Clamp(LinearToSrgb(lr), 0, 1),
            (float)Math.Clamp(LinearToSrgb(lg), 0, 1),
            (float)Math.Clamp(LinearToSrgb(lb), 0, 1),
            (float)alpha);
    }

    public static Color FromLch(double l, double c, double h)
        => FromLcha(l, c, h, 1.0);

    public static Color FromLcha(double l, double c, double h, double alpha)
    {
        double hRad = h * Math.PI / 180.0;
        double a = c * Math.Cos(hRad);
        double b = c * Math.Sin(hRad);
        return FromLaba(l, a, b, alpha);
    }

    // --- OKLab / OKLCH (CSS Color Level 4) ---

    public static Color FromOklab(double l, double a, double b)
        => FromOklaba(l, a, b, 1.0);

    public static Color FromOklaba(double l, double a, double b, double alpha)
    {
        alpha = Math.Clamp(alpha, 0, 1);
        // OKLab → LMS (cube roots)
        double lp = l + 0.3963377774 * a + 0.2158037573 * b;
        double mp = l - 0.1055613458 * a - 0.0638541728 * b;
        double sp = l - 0.0894841775 * a - 1.2914855480 * b;
        double ll = lp * lp * lp;
        double mm = mp * mp * mp;
        double ss = sp * sp * sp;
        // LMS → linear sRGB
        double lr = +4.0767416621 * ll - 3.3077115913 * mm + 0.2309699292 * ss;
        double lg = -1.2684380046 * ll + 2.6097574011 * mm - 0.3413193965 * ss;
        double lb = -0.0041960863 * ll - 0.7034186147 * mm + 1.7076147010 * ss;
        return new(
            (float)Math.Clamp(LinearToSrgb(lr), 0, 1),
            (float)Math.Clamp(LinearToSrgb(lg), 0, 1),
            (float)Math.Clamp(LinearToSrgb(lb), 0, 1),
            (float)alpha);
    }

    public static Color FromOklch(double l, double c, double h)
        => FromOklcha(l, c, h, 1.0);

    public static Color FromOklcha(double l, double c, double h, double alpha)
    {
        double hRad = h * Math.PI / 180.0;
        double a = c * Math.Cos(hRad);
        double b = c * Math.Sin(hRad);
        return FromOklaba(l, a, b, alpha);
    }

    // --- color-mix() (CSS Color Level 5) ---

    /// <summary>
    /// Mixes two colors in sRGB space. Percentages are 0-1.
    /// If both percentages are NaN, defaults to 50%/50%.
    /// </summary>
    public static Color ColorMix(Color c1, Color c2, double p1 = 0.5, double p2 = double.NaN)
    {
        if (double.IsNaN(p1) && double.IsNaN(p2)) { p1 = 0.5; p2 = 0.5; }
        else if (double.IsNaN(p2)) p2 = 1.0 - p1;
        else if (double.IsNaN(p1)) p1 = 1.0 - p2;

        double sum = p1 + p2;
        if (sum <= 0) return Transparent;
        p1 /= sum;
        p2 /= sum;

        return new(
            (float)Math.Clamp(c1.R * p1 + c2.R * p2, 0, 1),
            (float)Math.Clamp(c1.G * p1 + c2.G * p2, 0, 1),
            (float)Math.Clamp(c1.B * p1 + c2.B * p2, 0, 1),
            (float)Math.Clamp(c1.A * p1 + c2.A * p2, 0, 1));
    }

    // --- light-dark() (CSS Color Level 5) ---

    /// <summary>
    /// Returns lightColor in light mode, darkColor in dark mode.
    /// Currently always returns lightColor (dark mode not yet supported).
    /// </summary>
    public static Color LightDark(Color lightColor, Color darkColor, bool isDarkMode = false)
        => isDarkMode ? darkColor : lightColor;

    // --- System Colors (CSS Color Level 4) ---

    public static bool TryFromSystemColor(string name, out Color color)
        => SystemColors.TryGetValue(name.ToLowerInvariant(), out color);

    // --- Color space conversion helpers ---

    private static void LabToXyzD50(double l, double a, double b, out double x, out double y, out double z)
    {
        const double epsilon = 216.0 / 24389.0;
        const double kappa = 24389.0 / 27.0;
        const double xn = 0.96422;
        const double yn = 1.0;
        const double zn = 0.82521;

        double fy = (l + 16.0) / 116.0;
        double fx = a / 500.0 + fy;
        double fz = fy - b / 200.0;

        double xr = fx * fx * fx > epsilon ? fx * fx * fx : (116.0 * fx - 16.0) / kappa;
        double yr = l > kappa * epsilon ? Math.Pow((l + 16.0) / 116.0, 3) : l / kappa;
        double zr = fz * fz * fz > epsilon ? fz * fz * fz : (116.0 * fz - 16.0) / kappa;

        x = xr * xn;
        y = yr * yn;
        z = zr * zn;
    }

    private static void XyzD50ToD65(double x50, double y50, double z50, out double x65, out double y65, out double z65)
    {
        // Bradford chromatic adaptation D50 → D65
        x65 = 0.9555766 * x50 + -0.0230393 * y50 + 0.0631636 * z50;
        y65 = -0.0282895 * x50 + 1.0099416 * y50 + 0.0210077 * z50;
        z65 = 0.0122982 * x50 + -0.0204830 * y50 + 1.3299098 * z50;
    }

    private static void XyzD65ToLinearSrgb(double x, double y, double z, out double r, out double g, out double b)
    {
        r = 3.2404542 * x + -1.5371385 * y + -0.4985314 * z;
        g = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z;
        b = 0.0556434 * x + -0.2040259 * y + 1.0572252 * z;
    }

    private static double LinearToSrgb(double c)
    {
        if (c <= 0.0031308)
            return 12.92 * c;
        return 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
    }

    public static readonly Color Black = new(0, 0, 0, 1);
    public static readonly Color White = new(1, 1, 1, 1);
    public static readonly Color Transparent = new(0, 0, 0, 0);

    private static readonly Dictionary<string, Color> SystemColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["canvas"] = White,
        ["canvastext"] = Black,
        ["linktext"] = FromRgb(0, 0, 238),
        ["visitedtext"] = FromRgb(85, 26, 139),
        ["activetext"] = FromRgb(255, 0, 0),
        ["buttonface"] = FromRgb(240, 240, 240),
        ["buttontext"] = Black,
        ["buttonborder"] = FromRgb(118, 118, 118),
        ["field"] = White,
        ["fieldtext"] = Black,
        ["highlight"] = FromRgb(0, 120, 215),
        ["highlighttext"] = White,
        ["selecteditem"] = FromRgb(0, 120, 215),
        ["selecteditemtext"] = White,
        ["mark"] = FromRgb(255, 255, 0),
        ["marktext"] = Black,
        ["graytext"] = FromRgb(128, 128, 128),
        ["accentcolor"] = FromRgb(0, 120, 215),
        ["accentcolortext"] = White,
    };

    // Full CSS Color Level 4 named colors (148 colors + transparent)
    private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = FromRgb(240, 248, 255),
        ["antiquewhite"] = FromRgb(250, 235, 215),
        ["aqua"] = FromRgb(0, 255, 255),
        ["aquamarine"] = FromRgb(127, 255, 212),
        ["azure"] = FromRgb(240, 255, 255),
        ["beige"] = FromRgb(245, 245, 220),
        ["bisque"] = FromRgb(255, 228, 196),
        ["black"] = new(0, 0, 0, 1),
        ["blanchedalmond"] = FromRgb(255, 235, 205),
        ["blue"] = FromRgb(0, 0, 255),
        ["blueviolet"] = FromRgb(138, 43, 226),
        ["brown"] = FromRgb(165, 42, 42),
        ["burlywood"] = FromRgb(222, 184, 135),
        ["cadetblue"] = FromRgb(95, 158, 160),
        ["chartreuse"] = FromRgb(127, 255, 0),
        ["chocolate"] = FromRgb(210, 105, 30),
        ["coral"] = FromRgb(255, 127, 80),
        ["cornflowerblue"] = FromRgb(100, 149, 237),
        ["cornsilk"] = FromRgb(255, 248, 220),
        ["crimson"] = FromRgb(220, 20, 60),
        ["cyan"] = FromRgb(0, 255, 255),
        ["darkblue"] = FromRgb(0, 0, 139),
        ["darkcyan"] = FromRgb(0, 139, 139),
        ["darkgoldenrod"] = FromRgb(184, 134, 11),
        ["darkgray"] = FromRgb(169, 169, 169),
        ["darkgreen"] = FromRgb(0, 100, 0),
        ["darkgrey"] = FromRgb(169, 169, 169),
        ["darkkhaki"] = FromRgb(189, 183, 107),
        ["darkmagenta"] = FromRgb(139, 0, 139),
        ["darkolivegreen"] = FromRgb(85, 107, 47),
        ["darkorange"] = FromRgb(255, 140, 0),
        ["darkorchid"] = FromRgb(153, 50, 204),
        ["darkred"] = FromRgb(139, 0, 0),
        ["darksalmon"] = FromRgb(233, 150, 122),
        ["darkseagreen"] = FromRgb(143, 188, 143),
        ["darkslateblue"] = FromRgb(72, 61, 139),
        ["darkslategray"] = FromRgb(47, 79, 79),
        ["darkslategrey"] = FromRgb(47, 79, 79),
        ["darkturquoise"] = FromRgb(0, 206, 209),
        ["darkviolet"] = FromRgb(148, 0, 211),
        ["deeppink"] = FromRgb(255, 20, 147),
        ["deepskyblue"] = FromRgb(0, 191, 255),
        ["dimgray"] = FromRgb(105, 105, 105),
        ["dimgrey"] = FromRgb(105, 105, 105),
        ["dodgerblue"] = FromRgb(30, 144, 255),
        ["firebrick"] = FromRgb(178, 34, 34),
        ["floralwhite"] = FromRgb(255, 250, 240),
        ["forestgreen"] = FromRgb(34, 139, 34),
        ["fuchsia"] = FromRgb(255, 0, 255),
        ["gainsboro"] = FromRgb(220, 220, 220),
        ["ghostwhite"] = FromRgb(248, 248, 255),
        ["gold"] = FromRgb(255, 215, 0),
        ["goldenrod"] = FromRgb(218, 165, 32),
        ["gray"] = FromRgb(128, 128, 128),
        ["green"] = FromRgb(0, 128, 0),
        ["greenyellow"] = FromRgb(173, 255, 47),
        ["grey"] = FromRgb(128, 128, 128),
        ["honeydew"] = FromRgb(240, 255, 240),
        ["hotpink"] = FromRgb(255, 105, 180),
        ["indianred"] = FromRgb(205, 92, 92),
        ["indigo"] = FromRgb(75, 0, 130),
        ["ivory"] = FromRgb(255, 255, 240),
        ["khaki"] = FromRgb(240, 230, 140),
        ["lavender"] = FromRgb(230, 230, 250),
        ["lavenderblush"] = FromRgb(255, 240, 245),
        ["lawngreen"] = FromRgb(124, 252, 0),
        ["lemonchiffon"] = FromRgb(255, 250, 205),
        ["lightblue"] = FromRgb(173, 216, 230),
        ["lightcoral"] = FromRgb(240, 128, 128),
        ["lightcyan"] = FromRgb(224, 255, 255),
        ["lightgoldenrodyellow"] = FromRgb(250, 250, 210),
        ["lightgray"] = FromRgb(211, 211, 211),
        ["lightgreen"] = FromRgb(144, 238, 144),
        ["lightgrey"] = FromRgb(211, 211, 211),
        ["lightpink"] = FromRgb(255, 182, 193),
        ["lightsalmon"] = FromRgb(255, 160, 122),
        ["lightseagreen"] = FromRgb(32, 178, 170),
        ["lightskyblue"] = FromRgb(135, 206, 250),
        ["lightslategray"] = FromRgb(119, 136, 153),
        ["lightslategrey"] = FromRgb(119, 136, 153),
        ["lightsteelblue"] = FromRgb(176, 196, 222),
        ["lightyellow"] = FromRgb(255, 255, 224),
        ["lime"] = FromRgb(0, 255, 0),
        ["limegreen"] = FromRgb(50, 205, 50),
        ["linen"] = FromRgb(250, 240, 230),
        ["magenta"] = FromRgb(255, 0, 255),
        ["maroon"] = FromRgb(128, 0, 0),
        ["mediumaquamarine"] = FromRgb(102, 205, 170),
        ["mediumblue"] = FromRgb(0, 0, 205),
        ["mediumorchid"] = FromRgb(186, 85, 211),
        ["mediumpurple"] = FromRgb(147, 112, 219),
        ["mediumseagreen"] = FromRgb(60, 179, 113),
        ["mediumslateblue"] = FromRgb(123, 104, 238),
        ["mediumspringgreen"] = FromRgb(0, 250, 154),
        ["mediumturquoise"] = FromRgb(72, 209, 204),
        ["mediumvioletred"] = FromRgb(199, 21, 133),
        ["midnightblue"] = FromRgb(25, 25, 112),
        ["mintcream"] = FromRgb(245, 255, 250),
        ["mistyrose"] = FromRgb(255, 228, 225),
        ["moccasin"] = FromRgb(255, 228, 181),
        ["navajowhite"] = FromRgb(255, 222, 173),
        ["navy"] = FromRgb(0, 0, 128),
        ["oldlace"] = FromRgb(253, 245, 230),
        ["olive"] = FromRgb(128, 128, 0),
        ["olivedrab"] = FromRgb(107, 142, 35),
        ["orange"] = FromRgb(255, 165, 0),
        ["orangered"] = FromRgb(255, 69, 0),
        ["orchid"] = FromRgb(218, 112, 214),
        ["palegoldenrod"] = FromRgb(238, 232, 170),
        ["palegreen"] = FromRgb(152, 251, 152),
        ["paleturquoise"] = FromRgb(175, 238, 238),
        ["palevioletred"] = FromRgb(219, 112, 147),
        ["papayawhip"] = FromRgb(255, 239, 213),
        ["peachpuff"] = FromRgb(255, 218, 185),
        ["peru"] = FromRgb(205, 133, 63),
        ["pink"] = FromRgb(255, 192, 203),
        ["plum"] = FromRgb(221, 160, 221),
        ["powderblue"] = FromRgb(176, 224, 230),
        ["purple"] = FromRgb(128, 0, 128),
        ["rebeccapurple"] = FromRgb(102, 51, 153),
        ["red"] = FromRgb(255, 0, 0),
        ["rosybrown"] = FromRgb(188, 143, 143),
        ["royalblue"] = FromRgb(65, 105, 225),
        ["saddlebrown"] = FromRgb(139, 69, 19),
        ["salmon"] = FromRgb(250, 128, 114),
        ["sandybrown"] = FromRgb(244, 164, 96),
        ["seagreen"] = FromRgb(46, 139, 87),
        ["seashell"] = FromRgb(255, 245, 238),
        ["sienna"] = FromRgb(160, 82, 45),
        ["silver"] = FromRgb(192, 192, 192),
        ["skyblue"] = FromRgb(135, 206, 235),
        ["slateblue"] = FromRgb(106, 90, 205),
        ["slategray"] = FromRgb(112, 128, 144),
        ["slategrey"] = FromRgb(112, 128, 144),
        ["snow"] = FromRgb(255, 250, 250),
        ["springgreen"] = FromRgb(0, 255, 127),
        ["steelblue"] = FromRgb(70, 130, 180),
        ["tan"] = FromRgb(210, 180, 140),
        ["teal"] = FromRgb(0, 128, 128),
        ["thistle"] = FromRgb(216, 191, 216),
        ["tomato"] = FromRgb(255, 99, 71),
        ["transparent"] = Transparent,
        ["turquoise"] = FromRgb(64, 224, 208),
        ["violet"] = FromRgb(238, 130, 238),
        ["wheat"] = FromRgb(245, 222, 179),
        ["white"] = new(1, 1, 1, 1),
        ["whitesmoke"] = FromRgb(245, 245, 245),
        ["yellow"] = FromRgb(255, 255, 0),
        ["yellowgreen"] = FromRgb(154, 205, 50),
    };

    public bool Equals(Color other)
        => R == other.R && G == other.G && B == other.B && A == other.A;

    public override bool Equals(object? obj) => obj is Color c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(Color left, Color right) => left.Equals(right);
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    public override string ToString() => $"Color({R:F2}, {G:F2}, {B:F2}, {A:F2})";
}
