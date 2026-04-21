using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SuperRender.Analyzer;

/// <summary>
/// Discovers user interfaces inheriting <c>SuperRender.EcmaScript.Runtime.Interop.IJsType</c> and
/// emits a sealed proxy class per interface that forwards member access to the backing
/// <c>JsObject</c>. The proxy is self-registered with <c>JsTypeInterfaceProxyRegistry</c> via a
/// <c>[ModuleInitializer]</c> so that <c>value.AsInterface&lt;T&gt;()</c> uses the generated
/// proxy instead of falling back to <c>System.Reflection.DispatchProxy</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class JsTypeInterfaceProxyGenerator : IIncrementalGenerator
{
    private const string InteropNs = "SuperRender.EcmaScript.Runtime.Interop";
    private const string RuntimeNs = "SuperRender.EcmaScript.Runtime";
    private const string IJsTypeName = "IJsType";
    private const string JsNameAttr = "JsNameAttribute";

    private static readonly DiagnosticDescriptor UnsupportedInterfaceMember = new(
        "JSGEN003",
        "Unsupported interface member for IJsType",
        "Interface member '{0}.{1}' is not supported: {2}. Use properties or methods whose parameter and return types are JsValue-derived, a C# primitive (string/bool/double/float/decimal/int/uint/long/ulong/short/ushort/byte/sbyte), another IJsType interface, or JsValue[] (final rest parameter only). Events, indexers, generic methods, and ref/out parameters are not supported.",
        "SuperRender.Analyzer",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaces = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is InterfaceDeclarationSyntax,
                static (ctx, _) => Collect(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        context.RegisterSourceOutput(interfaces, static (ctx, model) => Emit(ctx, model));
    }

    private static InterfaceModel? Collect(GeneratorSyntaxContext ctx)
    {
        var decl = (InterfaceDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(decl) is not INamedTypeSymbol sym)
        {
            return null;
        }

        if (!InheritsIJsType(sym))
        {
            return null;
        }

        // Skip IJsType itself.
        if (sym.Name == IJsTypeName && sym.ContainingNamespace?.ToDisplayString() == InteropNs)
        {
            return null;
        }

        var members = ImmutableArray.CreateBuilder<MemberModel>();
        var diagnostics = new List<Diagnostic>();
        var allInterfaces = new List<INamedTypeSymbol> { sym };
        allInterfaces.AddRange(sym.AllInterfaces);

        var seen = new HashSet<string>();

        foreach (var iface in allInterfaces)
        {
            if (iface.Name == IJsTypeName && iface.ContainingNamespace?.ToDisplayString() == InteropNs)
            {
                continue;
            }

            foreach (var member in iface.GetMembers())
            {
                if (member.IsStatic)
                {
                    continue;
                }

                var key = iface.ToDisplayString() + "::" + member.Name;
                if (!seen.Add(key))
                {
                    continue;
                }

                var m = BuildMember(iface, member, diagnostics);
                if (m is not null)
                {
                    members.Add(m);
                }
            }
        }

        return new InterfaceModel(
            sym.ContainingNamespace is null || sym.ContainingNamespace.IsGlobalNamespace ? null : sym.ContainingNamespace.ToDisplayString(),
            sym.Name,
            sym.ToDisplayString(),
            members.ToImmutable(),
            diagnostics.ToImmutableArray());
    }

    private static MemberModel? BuildMember(INamedTypeSymbol declaringInterface, ISymbol member, List<Diagnostic> diagnostics)
    {
        var declaringFq = declaringInterface.ToDisplayString();

        switch (member)
        {
            case IEventSymbol:
                diagnostics.Add(Diagnostic.Create(
                    UnsupportedInterfaceMember,
                    member.Locations.FirstOrDefault(),
                    declaringFq, member.Name, "events are not supported"));
                return null;

            case IPropertySymbol prop when prop.IsIndexer:
                diagnostics.Add(Diagnostic.Create(
                    UnsupportedInterfaceMember,
                    member.Locations.FirstOrDefault(),
                    declaringFq, member.Name, "indexers are not supported"));
                return null;

            case IPropertySymbol prop:
                {
                    var kind = ClassifyType(prop.Type, isParam: true, isLast: true);
                    if (kind.Kind == TypeKind.Unsupported)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            UnsupportedInterfaceMember,
                            member.Locations.FirstOrDefault(),
                            declaringFq, member.Name, $"type '{prop.Type.ToDisplayString()}' is not convertible"));
                        return null;
                    }

                    var jsName = ResolveJsName(prop);
                    return new MemberModel(
                        MemberKind.Property,
                        declaringFq,
                        member.Name,
                        jsName,
                        Parameters: ImmutableArray<ParamModel>.Empty,
                        Return: kind,
                        HasGetter: prop.GetMethod is not null,
                        HasSetter: prop.SetMethod is not null,
                        TypeDisplay: prop.Type.ToDisplayString());
                }

            case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary:
                {
                    if (method.IsGenericMethod)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            UnsupportedInterfaceMember,
                            member.Locations.FirstOrDefault(),
                            declaringFq, member.Name, "generic methods are not supported"));
                        return null;
                    }

                    var parameters = ImmutableArray.CreateBuilder<ParamModel>();
                    for (var i = 0; i < method.Parameters.Length; i++)
                    {
                        var p = method.Parameters[i];
                        if (p.RefKind != RefKind.None)
                        {
                            diagnostics.Add(Diagnostic.Create(
                                UnsupportedInterfaceMember,
                                member.Locations.FirstOrDefault(),
                                declaringFq, member.Name, $"ref/out parameter '{p.Name}' is not supported"));
                            return null;
                        }

                        var isLast = i == method.Parameters.Length - 1;
                        var pkind = ClassifyType(p.Type, isParam: true, isLast: isLast);
                        if (pkind.Kind == TypeKind.Unsupported)
                        {
                            diagnostics.Add(Diagnostic.Create(
                                UnsupportedInterfaceMember,
                                member.Locations.FirstOrDefault(),
                                declaringFq, member.Name, $"parameter '{p.Name}' of type '{p.Type.ToDisplayString()}' is not convertible"));
                            return null;
                        }

                        parameters.Add(new ParamModel(p.Name, p.Type.ToDisplayString(), pkind));
                    }

                    var retKind = method.ReturnsVoid
                        ? new TypeInfo(TypeKind.Void, null)
                        : ClassifyType(method.ReturnType, isParam: false, isLast: false);
                    if (retKind.Kind == TypeKind.Unsupported)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            UnsupportedInterfaceMember,
                            member.Locations.FirstOrDefault(),
                            declaringFq, member.Name, $"return type '{method.ReturnType.ToDisplayString()}' is not convertible"));
                        return null;
                    }

                    var jsName = ResolveJsName(method);
                    return new MemberModel(
                        MemberKind.Method,
                        declaringFq,
                        member.Name,
                        jsName,
                        Parameters: parameters.ToImmutable(),
                        Return: retKind,
                        HasGetter: false,
                        HasSetter: false,
                        TypeDisplay: method.ReturnType.ToDisplayString());
                }

            default:
                return null;
        }
    }

    private static string ResolveJsName(ISymbol member)
    {
        foreach (var attr in member.GetAttributes())
        {
            if (attr.AttributeClass?.Name == JsNameAttr &&
                attr.AttributeClass.ContainingNamespace?.ToDisplayString() == InteropNs &&
                attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is string s)
            {
                return s;
            }
        }

        return CamelCase(member.Name);
    }

    /// <summary>Strips a leading uppercase <c>I</c> from an interface name when the next char is also uppercase,
    /// so generated proxy class names don't start with <c>I</c> (e.g., <c>IFoo</c> → <c>Foo</c>).</summary>
    private static string StripLeadingI(string name)
    {
        if (name.Length >= 2 && name[0] == 'I' && char.IsUpper(name[1]))
        {
            return name.Substring(1);
        }

        return name;
    }

    internal static string CamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
        {
            return name;
        }

        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (i > 0 && i < chars.Length - 1 && !char.IsUpper(chars[i + 1]))
            {
                break;
            }

            if (!char.IsUpper(chars[i]))
            {
                break;
            }

            chars[i] = char.ToLowerInvariant(chars[i]);
        }

        return new string(chars);
    }

    private static TypeInfo ClassifyType(ITypeSymbol t, bool isParam, bool isLast)
    {
        if (IsJsValueOrDerived(t))
        {
            return new TypeInfo(TypeKind.JsValue, t.ToDisplayString());
        }

        if (isParam && isLast && t is IArrayTypeSymbol arr && IsJsValue(arr.ElementType))
        {
            return new TypeInfo(TypeKind.JsValueArrayRest, t.ToDisplayString());
        }

        if (t.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface && InheritsIJsType(t))
        {
            return new TypeInfo(TypeKind.IJsTypeInterface, t.ToDisplayString());
        }

        return t.SpecialType switch
        {
            SpecialType.System_String => new TypeInfo(TypeKind.PString, null),
            SpecialType.System_Boolean => new TypeInfo(TypeKind.PBool, null),
            SpecialType.System_Double => new TypeInfo(TypeKind.PDouble, null),
            SpecialType.System_Single => new TypeInfo(TypeKind.PFloat, null),
            SpecialType.System_Decimal => new TypeInfo(TypeKind.PDecimal, null),
            SpecialType.System_Int32 => new TypeInfo(TypeKind.PInt32, null),
            SpecialType.System_Int64 => new TypeInfo(TypeKind.PInt64, null),
            SpecialType.System_Int16 => new TypeInfo(TypeKind.PInt16, null),
            SpecialType.System_Byte => new TypeInfo(TypeKind.PByte, null),
            SpecialType.System_UInt32 => new TypeInfo(TypeKind.PUInt32, null),
            SpecialType.System_UInt64 => new TypeInfo(TypeKind.PUInt64, null),
            SpecialType.System_SByte => new TypeInfo(TypeKind.PSByte, null),
            SpecialType.System_UInt16 => new TypeInfo(TypeKind.PUInt16, null),
            _ => new TypeInfo(TypeKind.Unsupported, null),
        };
    }

    private static bool InheritsIJsType(ITypeSymbol t)
    {
        if (t is INamedTypeSymbol nts)
        {
            foreach (var iface in nts.AllInterfaces)
            {
                if (iface.Name == IJsTypeName && iface.ContainingNamespace?.ToDisplayString() == InteropNs)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsJsValue(ITypeSymbol t) =>
        t.Name == "JsValue" && t.ContainingNamespace?.ToDisplayString() == RuntimeNs;

    private static bool IsJsValueOrDerived(ITypeSymbol t)
    {
        for (var cur = t; cur is not null; cur = cur.BaseType)
        {
            if (IsJsValue(cur))
            {
                return true;
            }
        }

        return false;
    }

    private static void Emit(SourceProductionContext ctx, InterfaceModel model)
    {
        foreach (var d in model.Diagnostics)
        {
            ctx.ReportDiagnostic(d);
        }

        if (model.Members.Length == 0 && model.Diagnostics.Length > 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8019 // unnecessary using");
        sb.AppendLine("using global::SuperRender.EcmaScript.Runtime;");
        sb.AppendLine("using global::SuperRender.EcmaScript.Runtime.Interop;");
        sb.AppendLine();

        if (model.Namespace is not null)
        {
            sb.Append("namespace ").Append(model.Namespace).AppendLine(";");
            sb.AppendLine();
        }

        var baseName = StripLeadingI(model.InterfaceName);
        var proxyName = "__" + baseName + "Proxy";
        var registrarName = "__" + baseName + "ProxyRegistrar";

        sb.Append("internal sealed class ").Append(proxyName)
          .Append(" : global::").Append(model.InterfaceFullyQualified)
          .AppendLine(", global::SuperRender.EcmaScript.Runtime.Interop.IJsTypeProxy");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly global::SuperRender.EcmaScript.Runtime.JsObject _target;");
        sb.Append("    public ").Append(proxyName).AppendLine("(global::SuperRender.EcmaScript.Runtime.JsObject target) { _target = target; }");
        sb.AppendLine("    global::SuperRender.EcmaScript.Runtime.JsObject global::SuperRender.EcmaScript.Runtime.Interop.IJsTypeProxy.Target => _target;");
        sb.AppendLine();

        foreach (var member in model.Members)
        {
            EmitMember(sb, member);
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append("file static class ").AppendLine(registrarName);
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.Append("    internal static void Register() => global::SuperRender.EcmaScript.Runtime.Interop.JsTypeInterfaceProxyRegistry.Register(typeof(global::")
          .Append(model.InterfaceFullyQualified)
          .Append("), o => new ").Append(proxyName).AppendLine("(o));");
        sb.AppendLine("}");

        var hintFileName = SanitizeFileName(model.InterfaceFullyQualified) + ".IJsTypeProxy.g.cs";
        ctx.AddSource(hintFileName, sb.ToString());
    }

    private static void EmitMember(StringBuilder sb, MemberModel m)
    {
        var ifaceFq = "global::" + m.DeclaringInterfaceFullyQualified;

        if (m.Kind == MemberKind.Property)
        {
            sb.Append("    ").Append(m.TypeDisplay).Append(' ').Append(ifaceFq).Append('.').Append(m.CsharpName).AppendLine();
            sb.AppendLine("    {");
            if (m.HasGetter)
            {
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.Append("            var __raw = _target.Get(\"").Append(Escape(m.JsName)).AppendLine("\");");
                sb.Append("            return ").Append(FromJs("__raw", m.Return)).AppendLine(";");
                sb.AppendLine("        }");
            }

            if (m.HasSetter)
            {
                sb.AppendLine("        set");
                sb.AppendLine("        {");
                sb.Append("            _target.Set(\"").Append(Escape(m.JsName)).Append("\", ").Append(ToJs("value", m.Return)).AppendLine(");");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            return;
        }

        // Method
        var retType = m.Return.Kind == TypeKind.Void ? "void" : m.TypeDisplay;
        sb.Append("    ").Append(retType).Append(' ').Append(ifaceFq).Append('.').Append(m.CsharpName).Append('(');
        for (var i = 0; i < m.Parameters.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(m.Parameters[i].TypeDisplay).Append(' ').Append(EscapeIdent(m.Parameters[i].Name));
        }

        sb.AppendLine(")");
        sb.AppendLine("    {");
        sb.Append("        var __fn = global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.RequireFunction(_target, \"").Append(Escape(m.JsName)).AppendLine("\");");

        // Build args array
        var hasRest = m.Parameters.Length > 0 && m.Parameters[m.Parameters.Length - 1].Type.Kind == TypeKind.JsValueArrayRest;
        var fixedCount = hasRest ? m.Parameters.Length - 1 : m.Parameters.Length;

        if (hasRest)
        {
            // Compose an array combining fixed args and the rest slot.
            sb.Append("        var __rest = ").Append(EscapeIdent(m.Parameters[m.Parameters.Length - 1].Name)).AppendLine(" ?? global::System.Array.Empty<global::SuperRender.EcmaScript.Runtime.JsValue>();");
            sb.Append("        var __args = new global::SuperRender.EcmaScript.Runtime.JsValue[").Append(fixedCount).AppendLine(" + __rest.Length];");
            for (var i = 0; i < fixedCount; i++)
            {
                sb.Append("        __args[").Append(i).Append("] = ").Append(ToJs(EscapeIdent(m.Parameters[i].Name), m.Parameters[i].Type)).AppendLine(";");
            }

            sb.Append("        global::System.Array.Copy(__rest, 0, __args, ").Append(fixedCount).AppendLine(", __rest.Length);");
        }
        else if (fixedCount == 0)
        {
            sb.AppendLine("        var __args = global::System.Array.Empty<global::SuperRender.EcmaScript.Runtime.JsValue>();");
        }
        else
        {
            sb.Append("        var __args = new global::SuperRender.EcmaScript.Runtime.JsValue[").Append(fixedCount).AppendLine("];");
            for (var i = 0; i < fixedCount; i++)
            {
                sb.Append("        __args[").Append(i).Append("] = ").Append(ToJs(EscapeIdent(m.Parameters[i].Name), m.Parameters[i].Type)).AppendLine(";");
            }
        }

        if (m.Return.Kind == TypeKind.Void)
        {
            sb.AppendLine("        __fn.Call(_target, __args);");
        }
        else
        {
            sb.AppendLine("        var __ret = __fn.Call(_target, __args);");
            sb.Append("        return ").Append(FromJs("__ret", m.Return)).AppendLine(";");
        }

        sb.AppendLine("    }");
    }

    /// <summary>C# value (typed) → JsValue expression.</summary>
    private static string ToJs(string expr, TypeInfo info)
    {
        return info.Kind switch
        {
            TypeKind.JsValue => expr,
            TypeKind.IJsTypeInterface => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.UnwrapIJsType({expr})",
            TypeKind.PString => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PBool => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PDouble => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PFloat => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PDecimal => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PInt32 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PInt64 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PInt16 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PByte => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PUInt32 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PUInt64 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PSByte => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            TypeKind.PUInt16 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.ToJs({expr})",
            _ => expr,
        };
    }

    /// <summary>JsValue expression → C# typed value.</summary>
    private static string FromJs(string expr, TypeInfo info)
    {
        return info.Kind switch
        {
            TypeKind.JsValue => info.TypeDisplay is "global::SuperRender.EcmaScript.Runtime.JsValue" or "SuperRender.EcmaScript.Runtime.JsValue" or "JsValue"
                ? expr
                : $"(({info.TypeDisplay}){expr})",
            TypeKind.IJsTypeInterface => $"global::SuperRender.EcmaScript.Runtime.Interop.JsValueExtension.AsInterface<global::{info.TypeDisplay}>({expr})",
            TypeKind.PString => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsString({expr})",
            TypeKind.PBool => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsBool({expr})",
            TypeKind.PDouble => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsDouble({expr})",
            TypeKind.PFloat => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsFloat({expr})",
            TypeKind.PDecimal => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsDecimal({expr})",
            TypeKind.PInt32 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsInt({expr})",
            TypeKind.PInt64 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsLong({expr})",
            TypeKind.PInt16 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsShort({expr})",
            TypeKind.PByte => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsByte({expr})",
            TypeKind.PUInt32 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsUInt({expr})",
            TypeKind.PUInt64 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsULong({expr})",
            TypeKind.PSByte => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsSByte({expr})",
            TypeKind.PUInt16 => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.FromJsUShort({expr})",
            _ => expr,
        };
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeIdent(string s)
    {
        // Prefix @ if the identifier collides with a C# keyword; safe blanket approach.
        return "@" + s;
    }

    private static string SanitizeFileName(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '.' ? ch : '_');
        }

        return sb.ToString();
    }

    private enum MemberKind
    {
        Property,
        Method,
    }

    private enum TypeKind
    {
        Unsupported,
        Void,
        JsValue,
        IJsTypeInterface,
        JsValueArrayRest,
        PString,
        PBool,
        PDouble,
        PFloat,
        PDecimal,
        PInt32,
        PInt64,
        PInt16,
        PByte,
        PUInt32,
        PUInt64,
        PSByte,
        PUInt16,
    }

    private sealed record TypeInfo(TypeKind Kind, string? TypeDisplay);

    private sealed record ParamModel(string Name, string TypeDisplay, TypeInfo Type);

    private sealed record MemberModel(
        MemberKind Kind,
        string DeclaringInterfaceFullyQualified,
        string CsharpName,
        string JsName,
        ImmutableArray<ParamModel> Parameters,
        TypeInfo Return,
        bool HasGetter,
        bool HasSetter,
        string TypeDisplay);

    private sealed record InterfaceModel(
        string? Namespace,
        string InterfaceName,
        string InterfaceFullyQualified,
        ImmutableArray<MemberModel> Members,
        ImmutableArray<Diagnostic> Diagnostics);
}
