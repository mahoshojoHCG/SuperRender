namespace SuperRender.EcmaScript.Runtime;

using System.Text.RegularExpressions;

public sealed class JsRegExp : JsDynamicObject
{
    private readonly Regex _regex;

    public string Pattern { get; }
    public string Flags { get; }
    public int LastIndex { get; set; }

    public bool Global => Flags.Contains('g', StringComparison.Ordinal);
    public bool IgnoreCase => Flags.Contains('i', StringComparison.Ordinal);
    public bool Multiline => Flags.Contains('m', StringComparison.Ordinal);
    public bool DotAll => Flags.Contains('s', StringComparison.Ordinal);
    public bool Unicode => Flags.Contains('u', StringComparison.Ordinal);
    public bool Sticky => Flags.Contains('y', StringComparison.Ordinal);
    public bool HasIndices => Flags.Contains('d', StringComparison.Ordinal);

    public JsRegExp(string pattern, string flags = "")
    {
        Pattern = pattern;
        Flags = flags;

        var options = TranslateFlags(flags);
        _regex = new Regex(pattern, options);
    }

    public JsValue Exec(string input)
    {
        Match match;
        if (Global || Sticky)
        {
            if (LastIndex > input.Length)
            {
                LastIndex = 0;
                return Null;
            }

            match = _regex.Match(input, LastIndex);
            if (!match.Success)
            {
                LastIndex = 0;
                return Null;
            }

            if (Sticky && match.Index != LastIndex)
            {
                LastIndex = 0;
                return Null;
            }

            LastIndex = match.Index + match.Length;
        }
        else
        {
            match = _regex.Match(input);
            if (!match.Success)
            {
                return Null;
            }
        }

        return BuildMatchArray(match, input);
    }

    public JsBoolean Test(string input)
    {
        var result = Exec(input);
        return result is JsNull ? JsBoolean.False : JsBoolean.True;
    }

    public override JsValue Get(string name)
    {
        return name switch
        {
            "source" => new JsString(Pattern),
            "flags" => new JsString(Flags),
            "global" => Global ? True : False,
            "ignoreCase" => IgnoreCase ? True : False,
            "multiline" => Multiline ? True : False,
            "dotAll" => DotAll ? True : False,
            "unicode" => Unicode ? True : False,
            "sticky" => Sticky ? True : False,
            "hasIndices" => HasIndices ? True : False,
            "lastIndex" => JsNumber.Create(LastIndex),
            _ => base.Get(name)
        };
    }

    public override void Set(string name, JsValue value)
    {
        if (name == "lastIndex")
        {
            LastIndex = (int)value.ToNumber();
            return;
        }

        base.Set(name, value);
    }

    private JsArray BuildMatchArray(Match match, string input)
    {
        var array = new JsArray();
        array.Push(new JsString(match.Value));

        for (int i = 1; i < match.Groups.Count; i++)
        {
            var group = match.Groups[i];
            array.Push(group.Success ? new JsString(group.Value) : Undefined);
        }

        array.DefineOwnProperty("index", PropertyDescriptor.Data(JsNumber.Create(match.Index), writable: false, enumerable: true, configurable: false));
        array.DefineOwnProperty("input", PropertyDescriptor.Data(new JsString(input), writable: false, enumerable: true, configurable: false));

        // Named capture groups: populate result.groups
        var groupsObj = BuildNamedGroups(match);
        array.DefineOwnProperty("groups", PropertyDescriptor.Data(
            groupsObj ?? (JsValue)Undefined,
            writable: false, enumerable: true, configurable: false));

        // /d flag (hasIndices): populate result.indices
        if (HasIndices)
        {
            var indices = BuildIndices(match);
            array.DefineOwnProperty("indices", PropertyDescriptor.Data(
                indices, writable: false, enumerable: true, configurable: false));
        }

        return array;
    }

    private static JsDynamicObject? BuildNamedGroups(Match match)
    {
        JsDynamicObject? groupsObj = null;

        foreach (Group group in match.Groups)
        {
            // Skip the overall match (index 0) and numeric-only group names
            if (int.TryParse(group.Name, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                continue;
            }

            groupsObj ??= new JsDynamicObject { Prototype = null };
            groupsObj.DefineOwnProperty(group.Name, PropertyDescriptor.Data(
                group.Success ? new JsString(group.Value) : Undefined,
                writable: true, enumerable: true, configurable: true));
        }

        return groupsObj;
    }

    private static JsArray BuildIndices(Match match)
    {
        var indices = new JsArray();
        JsDynamicObject? namedIndices = null;

        for (int i = 0; i < match.Groups.Count; i++)
        {
            var group = match.Groups[i];
            if (group.Success)
            {
                var pair = new JsArray();
                pair.Push(JsNumber.Create(group.Index));
                pair.Push(JsNumber.Create(group.Index + group.Length));
                indices.Push(pair);
            }
            else
            {
                indices.Push(Undefined);
            }

            // Also collect named group indices
            var name = group.Name;
            if (!int.TryParse(name, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                namedIndices ??= new JsDynamicObject { Prototype = null };
                if (group.Success)
                {
                    var pair = new JsArray();
                    pair.Push(JsNumber.Create(group.Index));
                    pair.Push(JsNumber.Create(group.Index + group.Length));
                    namedIndices.DefineOwnProperty(name, PropertyDescriptor.Data(
                        pair, writable: true, enumerable: true, configurable: true));
                }
                else
                {
                    namedIndices.DefineOwnProperty(name, PropertyDescriptor.Data(
                        Undefined, writable: true, enumerable: true, configurable: true));
                }
            }
        }

        indices.DefineOwnProperty("groups", PropertyDescriptor.Data(
            namedIndices ?? (JsValue)Undefined,
            writable: false, enumerable: true, configurable: false));

        return indices;
    }

    private static RegexOptions TranslateFlags(string flags)
    {
        var options = RegexOptions.None;

        foreach (var ch in flags)
        {
            switch (ch)
            {
                case 'i':
                    options |= RegexOptions.IgnoreCase;
                    break;
                case 'm':
                    options |= RegexOptions.Multiline;
                    break;
                case 's':
                    options |= RegexOptions.Singleline;
                    break;
            }
        }

        return options;
    }
}
