using SuperRender.Document.Dom;
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
        ["letterSpacing"] = "letter-spacing",
        ["wordSpacing"] = "word-spacing",
        ["textTransform"] = "text-transform",
        ["textDecoration"] = "text-decoration",
        ["textOverflow"] = "text-overflow",
        ["whiteSpace"] = "white-space",
        ["visibility"] = "visibility",
        ["opacity"] = "opacity",
        ["cursor"] = "cursor",
        ["overflow"] = "overflow",
        ["boxSizing"] = "box-sizing",
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
        ["minWidth"] = "min-width",
        ["maxWidth"] = "max-width",
        ["minHeight"] = "min-height",
        ["maxHeight"] = "max-height",
        ["display"] = "display",
        ["position"] = "position",
        ["top"] = "top",
        ["left"] = "left",
        ["right"] = "right",
        ["bottom"] = "bottom",
        ["zIndex"] = "z-index",
        ["border"] = "border",
        ["borderWidth"] = "border-width",
        ["borderColor"] = "border-color",
        ["borderStyle"] = "border-style",
        ["borderTop"] = "border-top",
        ["borderRight"] = "border-right",
        ["borderBottom"] = "border-bottom",
        ["borderLeft"] = "border-left",
        ["borderRadius"] = "border-radius",
        ["borderTopLeftRadius"] = "border-top-left-radius",
        ["borderTopRightRadius"] = "border-top-right-radius",
        ["borderBottomRightRadius"] = "border-bottom-right-radius",
        ["borderBottomLeftRadius"] = "border-bottom-left-radius",
        ["flexDirection"] = "flex-direction",
        ["flexWrap"] = "flex-wrap",
        ["flexGrow"] = "flex-grow",
        ["flexShrink"] = "flex-shrink",
        ["flexBasis"] = "flex-basis",
        ["flex"] = "flex",
        ["justifyContent"] = "justify-content",
        ["alignItems"] = "align-items",
        ["alignSelf"] = "align-self",
        ["gap"] = "gap",
        ["listStyleType"] = "list-style-type",
    };

    public JsCssStyleDeclaration(Element element)
    {
        DefineOwnProperty("cssText", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get cssText", (_, _) =>
                new JsString(element.GetAttribute(HtmlAttributeNames.Style) ?? ""), 0),
            JsFunction.CreateNative("set cssText", (_, args) =>
            {
                if (args.Length > 0)
                    element.SetAttribute(HtmlAttributeNames.Style, args[0].ToJsString());
                return Undefined;
            }, 1),
            enumerable: true, configurable: true));

        foreach (var (camelName, kebabName) in CamelToKebab)
        {
            var kebab = kebabName; // capture for closure
            DefineOwnProperty(camelName, PropertyDescriptor.Accessor(
                JsFunction.CreateNative($"get {camelName}", (_, _) =>
                {
                    var styleText = element.GetAttribute(HtmlAttributeNames.Style) ?? "";
                    return new JsString(GetPropertyValue(styleText, kebab));
                }, 0),
                JsFunction.CreateNative($"set {camelName}", (_, args) =>
                {
                    var value = args.Length > 0 ? args[0].ToJsString() : "";
                    var styleText = element.GetAttribute(HtmlAttributeNames.Style) ?? "";
                    var newStyle = SetPropertyValue(styleText, kebab, value);
                    element.SetAttribute(HtmlAttributeNames.Style, newStyle);
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
