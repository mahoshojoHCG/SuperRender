namespace SuperRender.Core.Style;

public static class PropertyDefaults
{
    public static readonly HashSet<string> InheritedProperties =
    [
        "color",
        "font-size",
        "font-family",
        "font-weight",
        "font-style",
        "text-align",
        "line-height",
    ];

    public static bool IsInherited(string property) => InheritedProperties.Contains(property);
}
