using SuperRender.Core.Css;

namespace SuperRender.Core.Style;

/// <summary>
/// Provides a default user-agent stylesheet following the WHATWG rendering specification.
/// This stylesheet is applied at the lowest cascade priority, before any author stylesheets.
/// </summary>
public static class UserAgentStylesheet
{
    private static Stylesheet? _cached;

    /// <summary>
    /// Returns a cached user-agent stylesheet instance.
    /// </summary>
    public static Stylesheet Create()
    {
        return _cached ??= new CssParser(CssText).Parse();
    }

    private const string CssText = """
        /* === Display types === */
        html, body, div, article, section, nav, aside, header, footer, main,
        address, blockquote, figure, figcaption, details, dialog, dd, dl, dt,
        fieldset, form, hgroup { display: block; }

        h1, h2, h3, h4, h5, h6 { display: block; }
        p { display: block; }
        ul, ol { display: block; }
        li { display: block; }
        pre { display: block; }
        hr { display: block; }

        head, title, style, meta, link, script, noscript { display: none; }

        /* === Body margin === */
        body { margin: 8px; }

        /* === Headings === */
        h1 {
            font-size: 32px;
            font-weight: bold;
            margin-top: 21px;
            margin-bottom: 21px;
        }
        h2 {
            font-size: 24px;
            font-weight: bold;
            margin-top: 20px;
            margin-bottom: 20px;
        }
        h3 {
            font-size: 18.72px;
            font-weight: bold;
            margin-top: 19px;
            margin-bottom: 19px;
        }
        h4 {
            font-size: 16px;
            font-weight: bold;
            margin-top: 21px;
            margin-bottom: 21px;
        }
        h5 {
            font-size: 13.28px;
            font-weight: bold;
            margin-top: 22px;
            margin-bottom: 22px;
        }
        h6 {
            font-size: 10.72px;
            font-weight: bold;
            margin-top: 25px;
            margin-bottom: 25px;
        }

        /* === Paragraphs === */
        p {
            margin-top: 16px;
            margin-bottom: 16px;
        }

        /* === Lists === */
        ul, ol {
            margin-top: 16px;
            margin-bottom: 16px;
            padding-left: 40px;
        }
        li {
            margin-bottom: 0;
        }

        /* === Horizontal rule === */
        hr {
            margin-top: 8px;
            margin-bottom: 8px;
            border-width: 1px;
            border-style: inset;
            border-color: gray;
        }

        /* === Preformatted / Code === */
        pre {
            margin-top: 16px;
            margin-bottom: 16px;
            font-family: monospace;
            white-space: pre;
        }
        code {
            font-family: monospace;
        }

        /* === Blockquote === */
        blockquote {
            margin-top: 16px;
            margin-bottom: 16px;
            margin-left: 40px;
            margin-right: 40px;
        }

        /* === Links === */
        a {
            color: #0000EE;
        }

        /* === Mark === */
        mark {
            background-color: yellow;
            color: black;
        }

        /* === Small === */
        small {
            font-size: 13.28px;
        }

        /* === Bold text === */
        strong, b {
            font-weight: bold;
        }

        /* === Italic text === */
        em, i, cite, var, dfn, address {
            font-style: italic;
        }

        /* === Underline === */
        u, ins {
            text-decoration: underline;
        }

        /* === Strikethrough === */
        s, del, strike {
            text-decoration: line-through;
        }

        /* === Links === underline (color already set above) */
        a {
            text-decoration: underline;
        }

        /* === Monospace === */
        kbd, samp {
            font-family: monospace;
        }

        /* === Subscript / Superscript === */
        sub, sup {
            font-size: 13.28px;
        }
        """;
}
