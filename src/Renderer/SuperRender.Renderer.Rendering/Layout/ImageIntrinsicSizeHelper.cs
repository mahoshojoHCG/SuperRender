using SuperRender.Document.Dom;

namespace SuperRender.Renderer.Rendering.Layout;

/// <summary>
/// Shared helpers for reading intrinsic image dimensions from HTML attributes
/// and decoded image metadata (data-natural-width/height).
/// Used by both BlockLayout and FlexLayout.
/// </summary>
internal static class ImageIntrinsicSizeHelper
{
    /// <summary>
    /// For &lt;img&gt; elements, tries to get intrinsic width from HTML attributes or decoded image metadata.
    /// Checks: HTML width attribute, data-natural-width attribute.
    /// </summary>
    public static bool TryGetWidth(LayoutBox box, out float width)
    {
        width = 0;
        if (box.DomNode is not Element el || el.TagName != HtmlTagNames.Img) return false;

        var widthAttr = el.GetAttribute(HtmlAttributeNames.Width);
        if (widthAttr != null && float.TryParse(widthAttr, System.Globalization.CultureInfo.InvariantCulture, out width))
            return true;

        var naturalW = el.GetAttribute(HtmlAttributeNames.DataNaturalWidth);
        if (naturalW != null && float.TryParse(naturalW, System.Globalization.CultureInfo.InvariantCulture, out width))
            return true;

        return false;
    }

    /// <summary>
    /// For &lt;img&gt; elements, tries to get intrinsic height, preserving aspect ratio if only width is known.
    /// </summary>
    public static bool TryGetHeight(LayoutBox box, float currentWidth, out float height)
    {
        height = 0;
        if (box.DomNode is not Element el || el.TagName != HtmlTagNames.Img) return false;

        var heightAttr = el.GetAttribute(HtmlAttributeNames.Height);
        if (heightAttr != null && float.TryParse(heightAttr, System.Globalization.CultureInfo.InvariantCulture, out height))
            return true;

        var naturalH = el.GetAttribute(HtmlAttributeNames.DataNaturalHeight);
        if (naturalH != null && float.TryParse(naturalH, System.Globalization.CultureInfo.InvariantCulture, out height))
        {
            var naturalW = el.GetAttribute(HtmlAttributeNames.DataNaturalWidth);
            if (naturalW != null && float.TryParse(naturalW, System.Globalization.CultureInfo.InvariantCulture, out float nw) && nw > 0)
            {
                height = currentWidth * height / nw;
            }
            return true;
        }

        return false;
    }
}
