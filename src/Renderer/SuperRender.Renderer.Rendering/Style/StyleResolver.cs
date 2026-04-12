using SuperRender.Document.Style;
using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;

namespace SuperRender.Renderer.Rendering.Style;

public sealed class StyleResolver
{
    private readonly List<Stylesheet> _stylesheets;
    private readonly Stylesheet? _userAgentStylesheet;
    private float _viewportWidth = PropertyDefaults.DefaultViewportWidth;
    private float _viewportHeight = PropertyDefaults.DefaultViewportHeight;

    public StyleResolver(List<Stylesheet> stylesheets, Stylesheet? userAgentStylesheet = null)
    {
        _stylesheets = stylesheets;
        _userAgentStylesheet = userAgentStylesheet;
    }

    public record PseudoElementInfo(ComputedStyle Style, string Content);

    /// <summary>
    /// Per-element pseudo-element data: maps (Element, PseudoElementType) to style+content.
    /// </summary>
    public Dictionary<(Element, PseudoElementType), PseudoElementInfo>? PseudoElements { get; private set; }

    public Dictionary<Node, ComputedStyle> ResolveAll(DomDocument document, float viewportWidth = PropertyDefaults.DefaultViewportWidth, float viewportHeight = PropertyDefaults.DefaultViewportHeight)
    {
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        PseudoElements = new Dictionary<(Element, PseudoElementType), PseudoElementInfo>();
        var styles = new Dictionary<Node, ComputedStyle>();
        ResolveNode(document, null, styles);
        return styles;
    }

    private void ResolveNode(Node node, ComputedStyle? parentStyle, Dictionary<Node, ComputedStyle> styles)
    {
        ComputedStyle style;

        if (node is Element element)
        {
            style = Resolve(element, parentStyle);

            // Resolve pseudo-elements for this element
            if (PseudoElements != null)
            {
                var before = ResolvePseudoElement(element, PseudoElementType.Before, style);
                if (before.HasValue)
                    PseudoElements[(element, PseudoElementType.Before)] =
                        new PseudoElementInfo(before.Value.style, before.Value.content);

                var after = ResolvePseudoElement(element, PseudoElementType.After, style);
                if (after.HasValue)
                    PseudoElements[(element, PseudoElementType.After)] =
                        new PseudoElementInfo(after.Value.style, after.Value.content);
            }
        }
        else if (node is TextNode)
        {
            // Text nodes only inherit text-related properties, not box-model properties
            style = new ComputedStyle { Display = DisplayType.Inline };
            if (parentStyle != null)
                InheritFromParent(style, parentStyle);
        }
        else
        {
            style = parentStyle?.Clone() ?? new ComputedStyle();
        }

        styles[node] = style;

        foreach (var child in node.Children)
        {
            ResolveNode(child, style, styles);
        }
    }

    public ComputedStyle Resolve(Element element, ComputedStyle? parentStyle = null)
    {
        var style = new ComputedStyle();

        // Start with inherited properties from parent
        if (parentStyle != null)
        {
            InheritFromParent(style, parentStyle);
        }

        // Apply default display based on tag
        style.Display = GetDefaultDisplay(element.TagName);

        // Collect all matching rules
        var matchedDeclarations = CollectMatchingDeclarations(element);

        // Sort by: !important > specificity > source order
        matchedDeclarations.Sort((a, b) =>
        {
            // !important first
            var impCmp = a.Important.CompareTo(b.Important);
            if (impCmp != 0) return impCmp;

            // Then specificity
            var specCmp = a.Specificity.CompareTo(b.Specificity);
            if (specCmp != 0) return specCmp;

            // Then source order
            return a.SourceOrder.CompareTo(b.SourceOrder);
        });

        // Apply declarations in order (later overrides earlier)
        foreach (var matched in matchedDeclarations)
        {
            ApplyDeclaration(style, matched.Declaration, parentStyle);
        }

        // Apply inline style attribute (highest specificity for non-!important)
        var inlineStyle = element.GetAttribute(HtmlAttributeNames.Style);
        if (!string.IsNullOrWhiteSpace(inlineStyle))
        {
            ApplyInlineStyle(style, inlineStyle, parentStyle);
        }

        // Apply hidden attribute
        if (element.Attributes.ContainsKey(HtmlAttributeNames.Hidden))
        {
            style.Display = DisplayType.None;
        }

        return style;
    }

    private List<MatchedDeclaration> CollectMatchingDeclarations(Element element)
    {
        var result = new List<MatchedDeclaration>();
        int sourceOrder = 0;

        // User-agent stylesheet first (lowest cascade priority)
        if (_userAgentStylesheet != null)
        {
            CollectFromStylesheet(_userAgentStylesheet, element, result, ref sourceOrder);
        }

        foreach (var stylesheet in _stylesheets)
        {
            CollectFromStylesheet(stylesheet, element, result, ref sourceOrder);
        }

        return result;
    }

    private static void CollectFromStylesheet(
        Stylesheet stylesheet, Element element,
        List<MatchedDeclaration> result, ref int sourceOrder)
    {
        foreach (var rule in stylesheet.Rules)
        {
            foreach (var selector in rule.Selectors)
            {
                // Skip pseudo-element selectors — they're handled separately
                if (selector.PseudoElement != null)
                    continue;

                if (SelectorMatcher.Matches(selector, element))
                {
                    var specificity = selector.GetSpecificity();
                    foreach (var declaration in rule.Declarations)
                    {
                        result.Add(new MatchedDeclaration
                        {
                            Specificity = specificity,
                            SourceOrder = sourceOrder++,
                            Declaration = declaration,
                            Important = declaration.Important,
                        });
                    }
                    break; // Only match once per rule
                }
            }
        }
    }

    /// <summary>
    /// Resolves the style for a ::before or ::after pseudo-element on the given element.
    /// Returns null if no matching pseudo-element rule with content is found.
    /// </summary>
    public (ComputedStyle style, string content)? ResolvePseudoElement(
        Element element, PseudoElementType pseudoType, ComputedStyle parentStyle)
    {
        var declarations = new List<MatchedDeclaration>();
        int sourceOrder = 0;

        if (_userAgentStylesheet != null)
            CollectPseudoFromStylesheet(_userAgentStylesheet, element, pseudoType, declarations, ref sourceOrder);
        foreach (var stylesheet in _stylesheets)
            CollectPseudoFromStylesheet(stylesheet, element, pseudoType, declarations, ref sourceOrder);

        if (declarations.Count == 0) return null;

        // Build a style by applying matched declarations
        var style = new ComputedStyle();
        InheritFromParent(style, parentStyle);

        declarations.Sort((a, b) =>
        {
            var impCmp = a.Important.CompareTo(b.Important);
            if (impCmp != 0) return impCmp;
            var specCmp = a.Specificity.CompareTo(b.Specificity);
            if (specCmp != 0) return specCmp;
            return a.SourceOrder.CompareTo(b.SourceOrder);
        });

        string? content = null;
        foreach (var matched in declarations)
        {
            if (matched.Declaration.Property == CssPropertyNames.Content)
            {
                content = ParseContentValue(matched.Declaration.Value.Raw);
            }
            else
            {
                ApplyDeclaration(style, matched.Declaration, parentStyle);
            }
        }

        if (content == null) return null;

        // Pseudo-elements are inline by default
        if (style.Display == DisplayType.Block)
            style.Display = DisplayType.Inline;

        return (style, content);
    }

    private static void CollectPseudoFromStylesheet(
        Stylesheet stylesheet, Element element, PseudoElementType pseudoType,
        List<MatchedDeclaration> result, ref int sourceOrder)
    {
        foreach (var rule in stylesheet.Rules)
        {
            foreach (var selector in rule.Selectors)
            {
                if (selector.PseudoElement != pseudoType)
                    continue;

                if (SelectorMatcher.Matches(selector, element))
                {
                    var specificity = selector.GetSpecificity();
                    foreach (var declaration in rule.Declarations)
                    {
                        result.Add(new MatchedDeclaration
                        {
                            Specificity = specificity,
                            SourceOrder = sourceOrder++,
                            Declaration = declaration,
                            Important = declaration.Important,
                        });
                    }
                    break;
                }
            }
        }
    }

    private static string? ParseContentValue(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("normal", StringComparison.OrdinalIgnoreCase))
            return null;

        // Strip quotes: "text" or 'text'
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') ||
             (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private void ApplyDeclaration(ComputedStyle style, Declaration decl, ComputedStyle? parentStyle)
    {
        var prop = decl.Property.ToLowerInvariant();
        var value = decl.Value;

        // Handle CSS global keywords: initial, inherit, unset, revert
        if (value.Type == CssValueType.Keyword)
        {
            var kw = value.Raw.ToLowerInvariant();
            switch (kw)
            {
                case "initial":
                    PropertyDefaults.ApplyInitialValue(style, prop);
                    return;
                case "inherit":
                    if (parentStyle != null)
                        PropertyDefaults.InheritProperty(style, prop, parentStyle);
                    return;
                case "unset":
                    if (PropertyDefaults.IsInherited(prop) && parentStyle != null)
                        PropertyDefaults.InheritProperty(style, prop, parentStyle);
                    else
                        PropertyDefaults.ApplyInitialValue(style, prop);
                    return;
                case "revert":
                    // Revert to UA default: skip this declaration (UA styles already applied)
                    return;
            }
        }

        if (ApplyBoxModelProperty(style, prop, value, parentStyle))
            return;
        if (ApplyTypographyProperty(style, prop, value, parentStyle))
            return;
        if (ApplyColorProperty(style, prop, value, parentStyle))
            return;
        if (ApplyPositionProperty(style, prop, value, parentStyle))
            return;
        ApplyFlexProperty(style, prop, value, parentStyle);
    }

    private bool ApplyBoxModelProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.Width:
                if (value.Type == CssValueType.Calc && value.CalcExpr != null)
                {
                    style.WidthCalc = value.CalcExpr;
                    style.Width = float.NaN;
                }
                else if (value.Type == CssValueType.Percentage)
                {
                    style.WidthCalc = new CalcValueNode(value);
                    style.Width = float.NaN;
                }
                else
                {
                    style.Width = ResolveLength(value, parentStyle);
                    style.WidthCalc = null;
                }
                break;
            case CssPropertyNames.Height:
                if (value.Type == CssValueType.Calc && value.CalcExpr != null)
                {
                    style.HeightCalc = value.CalcExpr;
                    style.Height = float.NaN;
                }
                else if (value.Type == CssValueType.Percentage)
                {
                    style.HeightCalc = new CalcValueNode(value);
                    style.Height = float.NaN;
                }
                else
                {
                    style.Height = ResolveLength(value, parentStyle);
                    style.HeightCalc = null;
                }
                break;

            case CssPropertyNames.BoxSizing:
                style.BoxSizing = value.Raw.ToLowerInvariant() switch
                {
                    "content-box" => BoxSizingType.ContentBox,
                    "border-box" => BoxSizingType.BorderBox,
                    _ => style.BoxSizing
                };
                break;

            case CssPropertyNames.MinWidth:
                var minW = ResolveLength(value, parentStyle);
                style.MinWidth = float.IsNaN(minW) ? 0 : minW;
                break;
            case CssPropertyNames.MaxWidth:
                if (value.Raw.Equals("none", StringComparison.OrdinalIgnoreCase))
                    style.MaxWidth = float.PositiveInfinity;
                else
                {
                    var maxW = ResolveLength(value, parentStyle);
                    style.MaxWidth = float.IsNaN(maxW) ? float.PositiveInfinity : maxW;
                }
                break;
            case CssPropertyNames.MinHeight:
                var minH = ResolveLength(value, parentStyle);
                style.MinHeight = float.IsNaN(minH) ? 0 : minH;
                break;
            case CssPropertyNames.MaxHeight:
                if (value.Raw.Equals("none", StringComparison.OrdinalIgnoreCase))
                    style.MaxHeight = float.PositiveInfinity;
                else
                {
                    var maxH = ResolveLength(value, parentStyle);
                    style.MaxHeight = float.IsNaN(maxH) ? float.PositiveInfinity : maxH;
                }
                break;

            case CssPropertyNames.MarginTop:
                style.Margin = style.Margin with { Top = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.MarginRight:
                style.Margin = style.Margin with { Right = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.MarginBottom:
                style.Margin = style.Margin with { Bottom = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.MarginLeft:
                style.Margin = style.Margin with { Left = ResolveLength(value, parentStyle) };
                break;

            case CssPropertyNames.PaddingTop:
                style.Padding = style.Padding with { Top = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.PaddingRight:
                style.Padding = style.Padding with { Right = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.PaddingBottom:
                style.Padding = style.Padding with { Bottom = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.PaddingLeft:
                style.Padding = style.Padding with { Left = ResolveLength(value, parentStyle) };
                break;

            case CssPropertyNames.BorderWidth:
                var bw = ResolveLength(value, parentStyle);
                style.BorderWidth = new EdgeSizes(bw);
                break;
            case CssPropertyNames.BorderTopWidth:
                style.BorderWidth = style.BorderWidth with { Top = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.BorderRightWidth:
                style.BorderWidth = style.BorderWidth with { Right = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.BorderBottomWidth:
                style.BorderWidth = style.BorderWidth with { Bottom = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.BorderLeftWidth:
                style.BorderWidth = style.BorderWidth with { Left = ResolveLength(value, parentStyle) };
                break;

            case CssPropertyNames.BorderStyle:
                var bs = value.Raw.ToLowerInvariant();
                style.BorderTopStyle = bs;
                style.BorderRightStyle = bs;
                style.BorderBottomStyle = bs;
                style.BorderLeftStyle = bs;
                break;
            case CssPropertyNames.BorderTopStyle:
                style.BorderTopStyle = value.Raw.ToLowerInvariant();
                break;
            case CssPropertyNames.BorderRightStyle:
                style.BorderRightStyle = value.Raw.ToLowerInvariant();
                break;
            case CssPropertyNames.BorderBottomStyle:
                style.BorderBottomStyle = value.Raw.ToLowerInvariant();
                break;
            case CssPropertyNames.BorderLeftStyle:
                style.BorderLeftStyle = value.Raw.ToLowerInvariant();
                break;

            case CssPropertyNames.BorderTopLeftRadius:
                style.BorderTopLeftRadius = ResolveBorderRadius(value, parentStyle);
                break;
            case CssPropertyNames.BorderTopRightRadius:
                style.BorderTopRightRadius = ResolveBorderRadius(value, parentStyle);
                break;
            case CssPropertyNames.BorderBottomRightRadius:
                style.BorderBottomRightRadius = ResolveBorderRadius(value, parentStyle);
                break;
            case CssPropertyNames.BorderBottomLeftRadius:
                style.BorderBottomLeftRadius = ResolveBorderRadius(value, parentStyle);
                break;

            default:
                return false;
        }
        return true;
    }

    private bool ApplyTypographyProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.FontSize:
                style.FontSize = ResolveFontSize(value, parentStyle);
                break;
            case CssPropertyNames.FontFamily:
                style.FontFamilies = FontFamilyParser.Parse(value.Raw);
                break;

            case CssPropertyNames.TextAlign:
                style.TextAlign = value.Raw.ToLowerInvariant() switch
                {
                    "left" => TextAlign.Left,
                    "right" => TextAlign.Right,
                    "center" => TextAlign.Center,
                    "justify" => TextAlign.Justify,
                    _ => style.TextAlign
                };
                break;

            case CssPropertyNames.LineHeight:
                if (value.Type == CssValueType.Number)
                    style.LineHeight = (float)value.NumericValue;
                else if (value.Type == CssValueType.Length)
                    style.LineHeight = (float)value.NumericValue / style.FontSize;
                else if (value.Type == CssValueType.Percentage)
                    style.LineHeight = (float)value.NumericValue / 100f;
                break;

            case CssPropertyNames.FontWeight:
                style.FontWeight = ResolveFontWeight(value, parentStyle);
                break;

            case CssPropertyNames.FontStyle:
                style.FontStyle = value.Raw.ToLowerInvariant() switch
                {
                    "normal" => FontStyleType.Normal,
                    "italic" => FontStyleType.Italic,
                    "oblique" => FontStyleType.Oblique,
                    _ => style.FontStyle
                };
                break;

            case CssPropertyNames.TextDecoration or CssPropertyNames.TextDecorationLine:
                style.TextDecorationLine = ResolveTextDecorationLine(value);
                break;

            case CssPropertyNames.WhiteSpace:
                style.WhiteSpace = value.Raw.ToLowerInvariant() switch
                {
                    "normal" => WhiteSpaceType.Normal,
                    "pre" => WhiteSpaceType.Pre,
                    "nowrap" => WhiteSpaceType.Nowrap,
                    "pre-wrap" => WhiteSpaceType.PreWrap,
                    "pre-line" => WhiteSpaceType.PreLine,
                    _ => style.WhiteSpace
                };
                break;

            case CssPropertyNames.TextTransform:
                style.TextTransform = value.Raw.ToLowerInvariant() switch
                {
                    "none" => TextTransformType.None,
                    "uppercase" => TextTransformType.Uppercase,
                    "lowercase" => TextTransformType.Lowercase,
                    "capitalize" => TextTransformType.Capitalize,
                    _ => style.TextTransform
                };
                break;

            case CssPropertyNames.LetterSpacing:
                if (value.Raw.Equals("normal", StringComparison.OrdinalIgnoreCase))
                    style.LetterSpacing = 0;
                else
                    style.LetterSpacing = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.WordSpacing:
                if (value.Raw.Equals("normal", StringComparison.OrdinalIgnoreCase))
                    style.WordSpacing = 0;
                else
                    style.WordSpacing = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.WordBreak:
                style.WordBreak = value.Raw.ToLowerInvariant() switch
                {
                    "normal" => WordBreakType.Normal,
                    "break-all" => WordBreakType.BreakAll,
                    "keep-all" => WordBreakType.KeepAll,
                    _ => style.WordBreak
                };
                break;

            case CssPropertyNames.OverflowWrap or CssPropertyNames.WordWrap:
                style.OverflowWrap = value.Raw.ToLowerInvariant() switch
                {
                    "normal" => OverflowWrapType.Normal,
                    "break-word" => OverflowWrapType.BreakWord,
                    "anywhere" => OverflowWrapType.Anywhere,
                    _ => style.OverflowWrap
                };
                break;

            case CssPropertyNames.ListStyleType:
                style.ListStyleType = value.Raw.ToLowerInvariant();
                break;

            case CssPropertyNames.ListStyle:
                // Simplified: treat entire value as list-style-type
                style.ListStyleType = value.Raw.ToLowerInvariant();
                break;

            default:
                return false;
        }
        return true;
    }

    private static bool ApplyColorProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.Color:
                style.Color = ResolveColor(value, parentStyle);
                break;
            case CssPropertyNames.BackgroundColor:
                style.BackgroundColor = ResolveColor(value, style);
                break;

            case CssPropertyNames.BorderColor:
                var bc = ResolveColor(value, style);
                style.BorderTopColor = bc;
                style.BorderRightColor = bc;
                style.BorderBottomColor = bc;
                style.BorderLeftColor = bc;
                break;
            case CssPropertyNames.BorderTopColor:
                style.BorderTopColor = ResolveColor(value, style);
                break;
            case CssPropertyNames.BorderRightColor:
                style.BorderRightColor = ResolveColor(value, style);
                break;
            case CssPropertyNames.BorderBottomColor:
                style.BorderBottomColor = ResolveColor(value, style);
                break;
            case CssPropertyNames.BorderLeftColor:
                style.BorderLeftColor = ResolveColor(value, style);
                break;

            case CssPropertyNames.TextDecorationColor:
                style.TextDecorationColor = ResolveColor(value, style);
                break;

            // P1: Opacity
            case CssPropertyNames.Opacity:
                if (value.Type is CssValueType.Number or CssValueType.Percentage)
                    style.Opacity = (float)Math.Clamp(value.NumericValue, 0, 1);
                break;

            default:
                return false;
        }
        return true;
    }

    private bool ApplyPositionProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.Display:
                style.Display = value.Raw.ToLowerInvariant() switch
                {
                    "block" => DisplayType.Block,
                    "inline" => DisplayType.Inline,
                    "inline-block" => DisplayType.InlineBlock,
                    "flow-root" => DisplayType.FlowRoot,
                    "none" => DisplayType.None,
                    "flex" => DisplayType.Flex,
                    _ => style.Display
                };
                break;

            case CssPropertyNames.Position:
                style.Position = value.Raw.ToLowerInvariant() switch
                {
                    "static" => PositionType.Static,
                    "relative" => PositionType.Relative,
                    "absolute" => PositionType.Absolute,
                    _ => style.Position
                };
                break;

            case CssPropertyNames.Top:
                style.Top = ResolveLength(value, parentStyle);
                break;
            case CssPropertyNames.Left:
                style.Left = ResolveLength(value, parentStyle);
                break;
            case CssPropertyNames.Right:
                style.Right = ResolveLength(value, parentStyle);
                break;
            case CssPropertyNames.Bottom:
                style.Bottom = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.ZIndex:
                if (value.Raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    style.ZIndex = 0;
                    style.ZIndexIsAuto = true;
                }
                else if (value.Type == CssValueType.Number)
                {
                    style.ZIndex = (int)value.NumericValue;
                    style.ZIndexIsAuto = false;
                }
                break;

            case CssPropertyNames.Overflow or CssPropertyNames.OverflowX or CssPropertyNames.OverflowY:
                style.Overflow = value.Raw.ToLowerInvariant() switch
                {
                    "visible" => OverflowType.Visible,
                    "hidden" => OverflowType.Hidden,
                    "scroll" => OverflowType.Scroll,
                    "auto" => OverflowType.Auto,
                    _ => style.Overflow
                };
                break;

            // P1: Inherited text properties
            case CssPropertyNames.Visibility:
                style.Visibility = value.Raw.ToLowerInvariant() switch
                {
                    "visible" => VisibilityType.Visible,
                    "hidden" => VisibilityType.Hidden,
                    "collapse" => VisibilityType.Collapse,
                    _ => style.Visibility
                };
                break;

            case CssPropertyNames.TextOverflow:
                style.TextOverflow = value.Raw.ToLowerInvariant() switch
                {
                    "clip" => TextOverflowType.Clip,
                    "ellipsis" => TextOverflowType.Ellipsis,
                    _ => style.TextOverflow
                };
                break;

            case CssPropertyNames.Cursor:
                style.Cursor = value.Raw.ToLowerInvariant() switch
                {
                    "auto" => CursorType.Auto,
                    "default" => CursorType.Default,
                    "pointer" => CursorType.Pointer,
                    "text" => CursorType.Text,
                    "crosshair" => CursorType.Crosshair,
                    "move" => CursorType.Move,
                    "not-allowed" => CursorType.NotAllowed,
                    "wait" => CursorType.Wait,
                    "help" => CursorType.Help,
                    _ => style.Cursor
                };
                break;

            default:
                return false;
        }
        return true;
    }

    private bool ApplyFlexProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.FlexDirection:
                style.FlexDirection = value.Raw.ToLowerInvariant() switch
                {
                    "row" => FlexDirectionType.Row,
                    "row-reverse" => FlexDirectionType.RowReverse,
                    "column" => FlexDirectionType.Column,
                    "column-reverse" => FlexDirectionType.ColumnReverse,
                    _ => style.FlexDirection
                };
                break;

            case CssPropertyNames.FlexWrap:
                style.FlexWrap = value.Raw.ToLowerInvariant() switch
                {
                    "nowrap" => FlexWrapType.Nowrap,
                    "wrap" => FlexWrapType.Wrap,
                    "wrap-reverse" => FlexWrapType.WrapReverse,
                    _ => style.FlexWrap
                };
                break;

            case CssPropertyNames.FlexFlow:
                ApplyFlexFlowShorthand(style, value.Raw);
                break;

            case CssPropertyNames.JustifyContent:
                style.JustifyContent = value.Raw.ToLowerInvariant() switch
                {
                    "flex-start" => JustifyContentType.FlexStart,
                    "flex-end" => JustifyContentType.FlexEnd,
                    "center" => JustifyContentType.Center,
                    "space-between" => JustifyContentType.SpaceBetween,
                    "space-around" => JustifyContentType.SpaceAround,
                    "space-evenly" => JustifyContentType.SpaceEvenly,
                    _ => style.JustifyContent
                };
                break;

            case CssPropertyNames.AlignItems:
                style.AlignItems = value.Raw.ToLowerInvariant() switch
                {
                    "stretch" => AlignItemsType.Stretch,
                    "flex-start" => AlignItemsType.FlexStart,
                    "flex-end" => AlignItemsType.FlexEnd,
                    "center" => AlignItemsType.Center,
                    "baseline" => AlignItemsType.Baseline,
                    _ => style.AlignItems
                };
                break;

            case CssPropertyNames.AlignSelf:
                style.AlignSelf = value.Raw.ToLowerInvariant() switch
                {
                    "auto" => AlignSelfType.Auto,
                    "stretch" => AlignSelfType.Stretch,
                    "flex-start" => AlignSelfType.FlexStart,
                    "flex-end" => AlignSelfType.FlexEnd,
                    "center" => AlignSelfType.Center,
                    "baseline" => AlignSelfType.Baseline,
                    _ => style.AlignSelf
                };
                break;

            case CssPropertyNames.FlexGrow:
                if (value.Type is CssValueType.Number)
                    style.FlexGrow = (float)value.NumericValue;
                break;

            case CssPropertyNames.FlexShrink:
                if (value.Type is CssValueType.Number)
                    style.FlexShrink = (float)value.NumericValue;
                break;

            case CssPropertyNames.FlexBasis:
                style.FlexBasis = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.Flex:
                ApplyFlexShorthand(style, value);
                break;

            case CssPropertyNames.Gap:
                {
                    var gapVal = ResolveLength(value, parentStyle);
                    if (!float.IsNaN(gapVal))
                    {
                        style.Gap = gapVal;
                        style.RowGap = float.NaN;
                        style.ColumnGap = float.NaN;
                    }
                }
                break;

            case CssPropertyNames.RowGap:
                {
                    var rgVal = ResolveLength(value, parentStyle);
                    if (!float.IsNaN(rgVal))
                        style.RowGap = rgVal;
                }
                break;

            case CssPropertyNames.ColumnGap:
                {
                    var cgVal = ResolveLength(value, parentStyle);
                    if (!float.IsNaN(cgVal))
                        style.ColumnGap = cgVal;
                }
                break;

            default:
                return false;
        }
        return true;
    }

    private static void ApplyFlexShorthand(ComputedStyle style, CssValue value)
    {
        var raw = value.Raw.Trim().ToLowerInvariant();

        switch (raw)
        {
            case "initial":
                style.FlexGrow = 0;
                style.FlexShrink = 1;
                style.FlexBasis = float.NaN; // auto
                return;
            case "auto":
                style.FlexGrow = 1;
                style.FlexShrink = 1;
                style.FlexBasis = float.NaN; // auto
                return;
            case "none":
                style.FlexGrow = 0;
                style.FlexShrink = 0;
                style.FlexBasis = float.NaN; // auto
                return;
        }

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 1 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var grow))
        {
            style.FlexGrow = grow;
            style.FlexShrink = 1;
            style.FlexBasis = 0; // single number: basis = 0

            if (parts.Length >= 2 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var shrink))
            {
                style.FlexShrink = shrink;

                if (parts.Length >= 3)
                {
                    if (parts[2] == "auto")
                        style.FlexBasis = float.NaN;
                    else
                        style.FlexBasis = ParseLengthValue(parts[2]);
                }
                else
                {
                    style.FlexBasis = 0;
                }
            }
        }
    }

    private static float ParseLengthValue(string raw)
    {
        if (raw == "auto") return float.NaN;
        if (raw == "0") return 0;

        // Try parsing number with unit
        int numEnd = 0;
        while (numEnd < raw.Length && (char.IsDigit(raw[numEnd]) || raw[numEnd] == '.' || raw[numEnd] == '-'))
            numEnd++;

        if (numEnd > 0 && float.TryParse(raw[..numEnd], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            var unit = raw[numEnd..].Trim().ToLowerInvariant();
            return unit switch
            {
                "px" or "" => num,
                _ => num
            };
        }

        return float.NaN;
    }

    private static void ApplyFlexFlowShorthand(ComputedStyle style, string raw)
    {
        var parts = raw.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            switch (part)
            {
                case "row":
                    style.FlexDirection = FlexDirectionType.Row;
                    break;
                case "row-reverse":
                    style.FlexDirection = FlexDirectionType.RowReverse;
                    break;
                case "column":
                    style.FlexDirection = FlexDirectionType.Column;
                    break;
                case "column-reverse":
                    style.FlexDirection = FlexDirectionType.ColumnReverse;
                    break;
                case "nowrap":
                    style.FlexWrap = FlexWrapType.Nowrap;
                    break;
                case "wrap":
                    style.FlexWrap = FlexWrapType.Wrap;
                    break;
                case "wrap-reverse":
                    style.FlexWrap = FlexWrapType.WrapReverse;
                    break;
            }
        }
    }

    private float ResolveLength(CssValue value, ComputedStyle? parentStyle)
    {
        if (value.Raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return float.NaN;

        if (value.Raw == "0")
            return 0;

        if (value.Type == CssValueType.Calc && value.CalcExpr != null)
        {
            var context = new CalcContext
            {
                FontSize = parentStyle?.FontSize ?? 16,
                ContainingBlockSize = 0,
                ViewportWidth = _viewportWidth,
                ViewportHeight = _viewportHeight
            };
            return (float)value.CalcExpr.Evaluate(context);
        }

        return value.Type switch
        {
            CssValueType.Length => value.Unit?.ToLowerInvariant() switch
            {
                "px" => (float)value.NumericValue,
                "em" => (float)value.NumericValue * (parentStyle?.FontSize ?? PropertyDefaults.DefaultFontSize),
                "rem" => (float)value.NumericValue * PropertyDefaults.DefaultFontSize,
                "vw" => (float)(value.NumericValue * _viewportWidth / 100),
                "vh" => (float)(value.NumericValue * _viewportHeight / 100),
                "vmin" => (float)(value.NumericValue * Math.Min(_viewportWidth, _viewportHeight) / 100),
                "vmax" => (float)(value.NumericValue * Math.Max(_viewportWidth, _viewportHeight) / 100),
                _ => (float)value.NumericValue
            },
            CssValueType.Number => (float)value.NumericValue,
            CssValueType.Percentage => (float)value.NumericValue,
            _ => float.NaN
        };
    }

    private static float ResolveFontSize(CssValue value, ComputedStyle? parentStyle)
    {
        var parentFontSize = parentStyle?.FontSize ?? PropertyDefaults.DefaultFontSize;

        return value.Type switch
        {
            CssValueType.Length => value.Unit?.ToLowerInvariant() switch
            {
                "px" => (float)value.NumericValue,
                "em" => (float)value.NumericValue * parentFontSize,
                "rem" => (float)value.NumericValue * PropertyDefaults.DefaultFontSize,
                "pt" => (float)value.NumericValue * 1.333f,
                _ => (float)value.NumericValue
            },
            CssValueType.Percentage => parentFontSize * (float)value.NumericValue / 100f,
            CssValueType.Number => (float)value.NumericValue,
            CssValueType.Keyword => value.Raw.ToLowerInvariant() switch
            {
                "small" => 13f,
                "medium" => PropertyDefaults.DefaultFontSize,
                "large" => 18f,
                "x-large" => 24f,
                "xx-large" => 32f,
                "smaller" => parentFontSize * 0.833f,
                "larger" => parentFontSize * 1.2f,
                _ => parentFontSize
            },
            _ => parentFontSize
        };
    }

    /// <summary>
    /// Resolves a border-radius value. Percentage values are stored as negative
    /// values (e.g., 50% → -50) to signal percentage-based resolution during painting.
    /// </summary>
    private float ResolveBorderRadius(CssValue value, ComputedStyle? parentStyle)
    {
        if (value.Type == CssValueType.Percentage)
            return -(float)value.NumericValue; // negative = percentage marker
        return ResolveLength(value, parentStyle);
    }

    private static Color ResolveColor(CssValue value, ComputedStyle? contextStyle = null)
    {
        if (value.ColorValue.HasValue)
            return value.ColorValue.Value;

        if (value.Raw.Equals("currentcolor", StringComparison.OrdinalIgnoreCase) ||
            value.Raw.Equals("currentColor", StringComparison.Ordinal))
        {
            return contextStyle?.Color ?? Color.Black;
        }

        if (value.Type == CssValueType.Color)
            return Color.FromHex(value.Raw);

        if (Color.TryFromName(value.Raw, out var named))
            return named;

        if (value.Raw.StartsWith('#'))
            return Color.FromHex(value.Raw);

        return Color.Black;
    }

    private static int ResolveFontWeight(CssValue value, ComputedStyle? parentStyle)
    {
        var raw = value.Raw.ToLowerInvariant();
        return raw switch
        {
            "normal" => 400,
            "bold" => 700,
            "bolder" => Math.Min((parentStyle?.FontWeight ?? 400) + 300, 900),
            "lighter" => Math.Max((parentStyle?.FontWeight ?? 400) - 100, 100),
            _ => value.Type == CssValueType.Number
                ? Math.Clamp((int)value.NumericValue, 100, 900)
                : parentStyle?.FontWeight ?? 400
        };
    }

    private static TextDecorationLine ResolveTextDecorationLine(CssValue value)
    {
        var raw = value.Raw.ToLowerInvariant().Trim();
        if (raw == "none")
            return TextDecorationLine.None;

        var result = TextDecorationLine.None;
        foreach (var part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            result |= part switch
            {
                "underline" => TextDecorationLine.Underline,
                "overline" => TextDecorationLine.Overline,
                "line-through" => TextDecorationLine.LineThrough,
                _ => TextDecorationLine.None
            };
        }
        return result;
    }

    private static void InheritFromParent(ComputedStyle style, ComputedStyle parentStyle)
    {
        style.Color = parentStyle.Color;
        style.FontSize = parentStyle.FontSize;
        style.FontFamilies = parentStyle.FontFamilies;
        style.FontWeight = parentStyle.FontWeight;
        style.FontStyle = parentStyle.FontStyle;
        style.TextAlign = parentStyle.TextAlign;
        style.LineHeight = parentStyle.LineHeight;
        style.WhiteSpace = parentStyle.WhiteSpace;
        style.Visibility = parentStyle.Visibility;
        style.TextTransform = parentStyle.TextTransform;
        style.LetterSpacing = parentStyle.LetterSpacing;
        style.WordSpacing = parentStyle.WordSpacing;
        style.Cursor = parentStyle.Cursor;
        style.WordBreak = parentStyle.WordBreak;
        style.OverflowWrap = parentStyle.OverflowWrap;
        style.ListStyleType = parentStyle.ListStyleType;
    }

    private static DisplayType GetDefaultDisplay(string tagName) => tagName switch
    {
        "div" or "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
            or "ul" or "ol" or "li" or "header" or "footer" or "main"
            or "section" or "article" or "nav" or "aside" or "hr"
            or "blockquote" or "pre" or "address" or "figure" or "figcaption"
            or "details" or "summary" or "dialog" or "dd" or "dl" or "dt"
            or "fieldset" or "form" or "hgroup" => DisplayType.Block,
        "span" or "a" or "strong" or "em" or "b" or "i" or "u"
            or "code" or "br" or "s" or "del" or "ins" or "strike"
            or "small" or "mark" or "cite" or "var" or "dfn" or "kbd" or "samp"
            or "abbr" or "sub" or "sup" or "q" or "time" or "data" => DisplayType.Inline,
        "img" => DisplayType.InlineBlock, // replaced element: needs width/height
        "html" or "body" => DisplayType.Block,
        "head" or "title" or "style" or "meta" or "link" or "script" => DisplayType.None,
        _ => DisplayType.Block,
    };

    private void ApplyInlineStyle(ComputedStyle style, string cssText, ComputedStyle? parentStyle)
    {
        // Use the full CSS parser to handle shorthand expansion (border, margin, padding, etc.)
        var declarations = CssParser.ParseInlineStyleDeclarations(cssText);
        foreach (var decl in declarations)
        {
            ApplyDeclaration(style, decl, parentStyle);
        }
    }

    private static CssValue ParseInlineValue(string rawValue)
    {
        // Delegate to the full CSS tokenizer for function values (hsl, rgb, calc, etc.)
        // and shorthand values that the simple parser can't handle
        if (rawValue.Contains('('))
        {
            return CssParser.ParseValueText(rawValue);
        }

        // Try to parse as a simple value
        if (rawValue.StartsWith('#'))
        {
            return new CssValue
            {
                Type = CssValueType.Color,
                Raw = rawValue,
                ColorValue = Color.FromHex(rawValue)
            };
        }

        if (Color.TryFromName(rawValue, out var namedColor))
        {
            return new CssValue
            {
                Type = CssValueType.Color,
                Raw = rawValue,
                ColorValue = namedColor
            };
        }

        // Try number + unit
        var numEnd = 0;
        while (numEnd < rawValue.Length && (char.IsDigit(rawValue[numEnd]) || rawValue[numEnd] == '.' || rawValue[numEnd] == '-'))
            numEnd++;

        if (numEnd > 0 && double.TryParse(rawValue[..numEnd], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            var unit = rawValue[numEnd..].Trim().ToLowerInvariant();
            if (unit == "%")
            {
                return new CssValue { Type = CssValueType.Percentage, Raw = rawValue, NumericValue = num };
            }
            if (unit.Length > 0)
            {
                return new CssValue { Type = CssValueType.Length, Raw = rawValue, NumericValue = num, Unit = unit };
            }
            return new CssValue { Type = CssValueType.Number, Raw = rawValue, NumericValue = num };
        }

        return new CssValue { Type = CssValueType.Keyword, Raw = rawValue };
    }

    private struct MatchedDeclaration
    {
        public Specificity Specificity;
        public int SourceOrder;
        public Declaration Declaration;
        public bool Important;
    }
}
