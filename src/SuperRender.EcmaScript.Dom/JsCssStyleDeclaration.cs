using SuperRender.Core.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for element.style - a CSSStyleDeclaration-like object.
/// Reads/writes the inline style attribute on the backing Element.
/// </summary>
internal sealed class JsCssStyleDeclaration : JsObject
{
    private static readonly Dictionary<string, string> CamelToKebab = new(StringComparer.Ordinal)
    {
        ["color"] = "color",
        ["backgroundColor"] = "background-color",
        ["fontSize"] = "font-size",
        ["fontFamily"] = "font-family",
        ["fontWeight"] = "font-weight",
        ["fontStyle"] = "font-style",
        ["textAlign"] = "text-align",
        ["lineHeight"] = "line-height",
        ["margin"] = "margin",
        ["marginTop"] = "margin-top",
        ["marginRight"] = "margin-right",
        ["marginBottom"] = "margin-bottom",
        ["marginLeft"] = "margin-left",
        ["padding"] = "padding",
        ["paddingTop"] = "padding-top",
        ["paddingRight"] = "padding-right",
        ["paddingBottom"] = "padding-bottom",
        ["paddingLeft"] = "padding-left",
        ["width"] = "width",
        ["height"] = "height",
        ["display"] = "display",
        ["position"] = "position",
        ["top"] = "top",
        ["left"] = "left",
        ["right"] = "right",
        ["bottom"] = "bottom",
        ["borderWidth"] = "border-width",
        ["borderColor"] = "border-color",
        ["borderStyle"] = "border-style",
        ["textDecoration"] = "text-decoration",
    };

    public JsCssStyleDeclaration(Element element)
    {
        DefineOwnProperty("cssText", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get cssText", (_, _) =>
                new JsString(element.GetAttribute("style") ?? ""), 0),
            JsFunction.CreateNative("set cssText", (_, args) =>
            {
                if (args.Length > 0)
                    element.SetAttribute("style", args[0].ToJsString());
                return Undefined;
            }, 1),
            enumerable: true, configurable: true));

        foreach (var (camelName, kebabName) in CamelToKebab)
        {
            var kebab = kebabName; // capture for closure
            DefineOwnProperty(camelName, PropertyDescriptor.Accessor(
                JsFunction.CreateNative($"get {camelName}", (_, _) =>
                {
                    var styleText = element.GetAttribute("style") ?? "";
                    return new JsString(GetPropertyValue(styleText, kebab));
                }, 0),
                JsFunction.CreateNative($"set {camelName}", (_, args) =>
                {
                    var value = args.Length > 0 ? args[0].ToJsString() : "";
                    var styleText = element.GetAttribute("style") ?? "";
                    var newStyle = SetPropertyValue(styleText, kebab, value);
                    element.SetAttribute("style", newStyle);
                    return Undefined;
                }, 1),
                enumerable: true, configurable: true));
        }
    }

    private static string GetPropertyValue(string cssText, string property)
    {
        var parts = cssText.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var colonIdx = part.IndexOf(':');
            if (colonIdx < 0) continue;
            var prop = part[..colonIdx].Trim();
            if (prop.Equals(property, StringComparison.OrdinalIgnoreCase))
                return part[(colonIdx + 1)..].Trim();
        }
        return "";
    }

    private static string SetPropertyValue(string cssText, string property, string value)
    {
        var parts = cssText.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var found = false;
        var result = new List<string>();

        foreach (var part in parts)
        {
            var colonIdx = part.IndexOf(':');
            if (colonIdx < 0) { result.Add(part.Trim()); continue; }
            var prop = part[..colonIdx].Trim();
            if (prop.Equals(property, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                if (!string.IsNullOrEmpty(value))
                    result.Add($"{property}: {value}");
                // If value is empty, we remove the property
            }
            else
            {
                result.Add(part.Trim());
            }
        }

        if (!found && !string.IsNullOrEmpty(value))
            result.Add($"{property}: {value}");

        return string.Join("; ", result);
    }
}
