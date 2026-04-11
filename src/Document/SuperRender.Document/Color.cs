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

    public static readonly Color Black = new(0, 0, 0, 1);
    public static readonly Color White = new(1, 1, 1, 1);
    public static readonly Color Transparent = new(0, 0, 0, 0);

    private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = new(0, 0, 0, 1),
        ["white"] = new(1, 1, 1, 1),
        ["red"] = FromRgb(255, 0, 0),
        ["green"] = FromRgb(0, 128, 0),
        ["blue"] = FromRgb(0, 0, 255),
        ["yellow"] = FromRgb(255, 255, 0),
        ["cyan"] = FromRgb(0, 255, 255),
        ["magenta"] = FromRgb(255, 0, 255),
        ["orange"] = FromRgb(255, 165, 0),
        ["purple"] = FromRgb(128, 0, 128),
        ["gray"] = FromRgb(128, 128, 128),
        ["grey"] = FromRgb(128, 128, 128),
        ["silver"] = FromRgb(192, 192, 192),
        ["maroon"] = FromRgb(128, 0, 0),
        ["olive"] = FromRgb(128, 128, 0),
        ["lime"] = FromRgb(0, 255, 0),
        ["aqua"] = FromRgb(0, 255, 255),
        ["teal"] = FromRgb(0, 128, 128),
        ["navy"] = FromRgb(0, 0, 128),
        ["fuchsia"] = FromRgb(255, 0, 255),
        ["transparent"] = Transparent,
        // Extended colors commonly used
        ["coral"] = FromRgb(255, 127, 80),
        ["crimson"] = FromRgb(220, 20, 60),
        ["darkblue"] = FromRgb(0, 0, 139),
        ["darkgray"] = FromRgb(169, 169, 169),
        ["darkgreen"] = FromRgb(0, 100, 0),
        ["darkred"] = FromRgb(139, 0, 0),
        ["gold"] = FromRgb(255, 215, 0),
        ["indigo"] = FromRgb(75, 0, 130),
        ["ivory"] = FromRgb(255, 255, 240),
        ["khaki"] = FromRgb(240, 230, 140),
        ["lavender"] = FromRgb(230, 230, 250),
        ["lightblue"] = FromRgb(173, 216, 230),
        ["lightgray"] = FromRgb(211, 211, 211),
        ["lightgreen"] = FromRgb(144, 238, 144),
        ["pink"] = FromRgb(255, 192, 203),
        ["salmon"] = FromRgb(250, 128, 114),
        ["skyblue"] = FromRgb(135, 206, 235),
        ["tomato"] = FromRgb(255, 99, 71),
        ["violet"] = FromRgb(238, 130, 238),
        ["wheat"] = FromRgb(245, 222, 179),
    };

    public bool Equals(Color other)
        => R == other.R && G == other.G && B == other.B && A == other.A;

    public override bool Equals(object? obj) => obj is Color c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(Color left, Color right) => left.Equals(right);
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    public override string ToString() => $"Color({R:F2}, {G:F2}, {B:F2}, {A:F2})";
}
