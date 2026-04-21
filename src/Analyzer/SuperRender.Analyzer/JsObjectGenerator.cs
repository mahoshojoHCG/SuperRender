using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SuperRender.Analyzer;

[Generator(LanguageNames.CSharp)]
public sealed class JsObjectGenerator : IIncrementalGenerator
{
    private const string Ns = "SuperRender.EcmaScript.Runtime";
    private const string InteropNs = "SuperRender.EcmaScript.Runtime.Interop";
    private const string IJsTypeName = "IJsType";
    private const string JsObjectAttr = "JsObjectAttribute";
    private const string JsMethodAttr = "JsMethodAttribute";
    private const string JsPropertyAttr = "JsPropertyAttribute";

    private static readonly DiagnosticDescriptor UnsupportedParamType = new(
        "JSGEN001",
        "Unsupported parameter type for [JsMethod]/[JsProperty]",
        "Parameter type '{0}' on {1}.{2} is not supported. Use JsValue, JsObject, a C# primitive (string/bool/double/int/long/float/short/byte/uint/ulong/sbyte/ushort/decimal), or JsValue[] (last parameter only, for rest args).",
        "SuperRender.Analyzer",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedReturnType = new(
        "JSGEN002",
        "Unsupported return type for [JsMethod]/[JsProperty]",
        "Return type '{0}' on {1}.{2} is not supported. Use void, a JsValue-derived type, or a C# primitive.",
        "SuperRender.Analyzer",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MemberNotRepresentableInInterface = new(
        "JSGEN004",
        "Member skipped for GenerateInterface",
        "Member '{0}.{1}' is skipped from generated interface: {2}",
        "SuperRender.Analyzer",
        DiagnosticSeverity.Info,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("JsObjectAttributes.g.cs", AttributeSource);
        });

        var classes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{Ns}.{JsObjectAttr}",
                static (node, _) => node is ClassDeclarationSyntax cds
                    && cds.Modifiers.Any(m => m.ValueText == "partial"),
                static (ctx, _) => Collect(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        var targetDir = context.AnalyzerConfigOptionsProvider.Select(static (opts, _) =>
        {
            opts.GlobalOptions.TryGetValue("build_property.TargetDir", out var dir);
            return dir;
        });

        var combined = classes.Combine(targetDir);

        context.RegisterSourceOutput(combined, static (ctx, tuple) => Emit(ctx, tuple.Left, tuple.Right));
    }

    private static JsObjectModel? Collect(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol cls)
        {
            return null;
        }

        if (!InheritsJsObject(cls))
        {
            return null;
        }

        var generateInterface = false;
        foreach (var attr in ctx.Attributes)
        {
            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "GenerateInterface" && named.Value.Value is bool b)
                {
                    generateInterface = b;
                }
            }
        }

        var methods = new List<MethodModel>();
        var properties = new List<PropertyModel>();
        var diagnostics = new List<Diagnostic>();

        foreach (var member in cls.GetMembers())
        {
            foreach (var attr in member.GetAttributes())
            {
                var attrName = attr.AttributeClass?.Name;
                if (attrName == JsMethodAttr && member is IMethodSymbol m)
                {
                    var model = BuildMethod(m, attr, cls.Name, diagnostics);
                    if (model is not null)
                    {
                        methods.Add(model);
                    }
                }
                else if (attrName == JsPropertyAttr)
                {
                    var models = BuildProperty(member, attr, cls.Name, diagnostics);
                    properties.AddRange(models);
                }
            }
        }

        if (methods.Count == 0 && properties.Count == 0 && diagnostics.Count == 0 && !generateInterface)
        {
            return null;
        }

        return new JsObjectModel(
            cls.ContainingNamespace.IsGlobalNamespace ? null : cls.ContainingNamespace.ToDisplayString(),
            cls.Name,
            cls.IsSealed,
            generateInterface,
            methods.ToImmutableArray(),
            properties.ToImmutableArray(),
            diagnostics.ToImmutableArray());
    }

    private static MethodModel? BuildMethod(IMethodSymbol m, AttributeData attr, string className, List<Diagnostic> diagnostics)
    {
        var jsName = attr.ConstructorArguments.Length > 0
            ? attr.ConstructorArguments[0].Value as string
            : m.Name;

        // Legacy: (JsValue, JsValue[]) => JsValue-derived
        var isLegacy = m.Parameters.Length == 2
            && IsJsValue(m.Parameters[0].Type)
            && IsJsValueArray(m.Parameters[1].Type);

        var parameters = ImmutableArray.CreateBuilder<ParamModel>();
        if (!isLegacy)
        {
            for (var i = 0; i < m.Parameters.Length; i++)
            {
                var p = m.Parameters[i];
                var kind = ClassifyParam(p.Type, i == m.Parameters.Length - 1);
                if (kind == ParamKind.Unsupported)
                {
                    diagnostics.Add(Diagnostic.Create(
                        UnsupportedParamType,
                        p.Locations.FirstOrDefault() ?? m.Locations.FirstOrDefault(),
                        p.Type.ToDisplayString(),
                        className,
                        m.Name));
                    return null;
                }

                parameters.Add(new ParamModel(p.Name, p.Type.ToDisplayString(), kind));
            }
        }

        var retKind = ClassifyReturn(m.ReturnType);
        if (retKind == ReturnKind.Unsupported)
        {
            diagnostics.Add(Diagnostic.Create(
                UnsupportedReturnType,
                m.Locations.FirstOrDefault(),
                m.ReturnType.ToDisplayString(),
                className,
                m.Name));
            return null;
        }

        ReturnKind innerKind = ReturnKind.Void;
        string? innerDisplay = null;
        if (retKind == ReturnKind.ROptional && IsJsOptional(m.ReturnType, out var innerType) && innerType is not null)
        {
            innerKind = ClassifyReturn(innerType);
            if (innerKind == ReturnKind.Unsupported || innerKind == ReturnKind.Void || innerKind == ReturnKind.ROptional)
            {
                diagnostics.Add(Diagnostic.Create(
                    UnsupportedReturnType,
                    m.Locations.FirstOrDefault(),
                    m.ReturnType.ToDisplayString(),
                    className,
                    m.Name));
                return null;
            }

            innerDisplay = innerType.ToDisplayString();
        }

        // Auto-derive function.length from signature:
        //   typed mode: count of parameters excluding a trailing JsValue[] rest slot
        //   legacy mode: 0 (source signature carries no arity info)
        var length = isLegacy
            ? 0
            : parameters.Count(static p => p.Kind != ParamKind.ArgsArray);

        return new MethodModel(
            jsName ?? m.Name,
            m.Name,
            m.IsStatic,
            length,
            isLegacy,
            parameters.ToImmutable(),
            new ReturnModel(m.ReturnType.ToDisplayString(), retKind, innerKind, innerDisplay));
    }

    private static IEnumerable<PropertyModel> BuildProperty(ISymbol member, AttributeData attr, string className, List<Diagnostic> diagnostics)
    {
        var jsName = attr.ConstructorArguments.Length > 0
            ? attr.ConstructorArguments[0].Value as string
            : member.Name;

        if (member is not IPropertySymbol prop)
        {
            yield break;
        }

        var valueType = prop.Type;
        var isStatic = prop.IsStatic;

        var getterKind = ClassifyReturn(valueType);
        if (getterKind == ReturnKind.Unsupported || getterKind == ReturnKind.Void)
        {
            diagnostics.Add(Diagnostic.Create(
                UnsupportedReturnType,
                member.Locations.FirstOrDefault(),
                valueType.ToDisplayString(),
                className,
                member.Name));
            yield break;
        }

        ReturnKind innerKind = ReturnKind.Void;
        string? innerDisplay = null;
        if (getterKind == ReturnKind.ROptional && IsJsOptional(valueType, out var innerType) && innerType is not null)
        {
            innerKind = ClassifyReturn(innerType);
            if (innerKind == ReturnKind.Unsupported || innerKind == ReturnKind.Void || innerKind == ReturnKind.ROptional)
            {
                diagnostics.Add(Diagnostic.Create(
                    UnsupportedReturnType,
                    member.Locations.FirstOrDefault(),
                    valueType.ToDisplayString(),
                    className,
                    member.Name));
                yield break;
            }

            innerDisplay = innerType.ToDisplayString();
        }

        yield return new PropertyModel(jsName ?? member.Name, member.Name, true, isStatic, false,
            valueType.ToDisplayString(), null, (ReturnKind?)getterKind, innerKind, innerDisplay);

        if (prop.SetMethod is not null)
        {
            var setterKind = ClassifyParam(valueType, true);
            if (setterKind == ParamKind.Unsupported || setterKind == ParamKind.ArgsArray)
            {
                diagnostics.Add(Diagnostic.Create(
                    UnsupportedParamType,
                    member.Locations.FirstOrDefault(),
                    valueType.ToDisplayString(),
                    className,
                    member.Name));
                yield break;
            }

            yield return new PropertyModel(jsName ?? member.Name, member.Name, true, isStatic, true,
                valueType.ToDisplayString(), (ParamKind?)setterKind, null);
        }
    }

    private static bool InheritsJsObject(INamedTypeSymbol cls)
    {
        for (var t = cls.BaseType; t is not null; t = t.BaseType)
        {
            if (t.Name == "JsObject" && t.ContainingNamespace?.ToDisplayString() == Ns)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsJsValue(ITypeSymbol t) =>
        t.Name == "JsValue" && t.ContainingNamespace?.ToDisplayString() == Ns;

    private static bool IsJsObject(ITypeSymbol t) =>
        t.Name == "JsObject" && t.ContainingNamespace?.ToDisplayString() == Ns;

    private static bool IsJsValueArray(ITypeSymbol t) =>
        t is IArrayTypeSymbol arr && IsJsValue(arr.ElementType);

    private static bool IsJsValueDerived(ITypeSymbol t)
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

    private static bool InheritsIJsType(ITypeSymbol t)
    {
        if (t.TypeKind != Microsoft.CodeAnalysis.TypeKind.Interface)
        {
            return false;
        }

        if (t is INamedTypeSymbol nts)
        {
            if (nts.Name == IJsTypeName && nts.ContainingNamespace?.ToDisplayString() == InteropNs)
            {
                return true;
            }

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

    private static ParamKind ClassifyParam(ITypeSymbol t, bool isLast)
    {
        if (IsJsValue(t))
        {
            return ParamKind.JsValue;
        }

        if (IsJsObject(t))
        {
            return ParamKind.JsObject;
        }

        if (InheritsIJsType(t))
        {
            return ParamKind.IJsTypeInterface;
        }

        if (IsJsValueArray(t) && isLast)
        {
            return ParamKind.ArgsArray;
        }

        // Primitives — keyed by SpecialType
        return t.SpecialType switch
        {
            SpecialType.System_String => ParamKind.PString,
            SpecialType.System_Boolean => ParamKind.PBool,
            SpecialType.System_Double => ParamKind.PDouble,
            SpecialType.System_Single => ParamKind.PFloat,
            SpecialType.System_Int32 => ParamKind.PInt32,
            SpecialType.System_Int64 => ParamKind.PInt64,
            SpecialType.System_Int16 => ParamKind.PInt16,
            SpecialType.System_Byte => ParamKind.PByte,
            SpecialType.System_UInt32 => ParamKind.PUInt32,
            SpecialType.System_UInt64 => ParamKind.PUInt64,
            SpecialType.System_SByte => ParamKind.PSByte,
            SpecialType.System_UInt16 => ParamKind.PUInt16,
            SpecialType.System_Decimal => ParamKind.PDecimal,
            _ => ParamKind.Unsupported,
        };
    }

    private static bool IsJsOptional(ITypeSymbol t, out ITypeSymbol? inner)
    {
        inner = null;
        if (t is INamedTypeSymbol nts
            && nts.Name == "JsOptional"
            && nts.ContainingNamespace?.ToDisplayString() == Ns
            && nts.TypeArguments.Length == 1)
        {
            inner = nts.TypeArguments[0];
            return true;
        }

        return false;
    }

    private static ReturnKind ClassifyReturn(ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_Void)
        {
            return ReturnKind.Void;
        }

        if (IsJsOptional(t, out _))
        {
            return ReturnKind.ROptional;
        }

        if (IsJsValueDerived(t))
        {
            return ReturnKind.JsValue;
        }

        if (InheritsIJsType(t))
        {
            return ReturnKind.RIJsTypeInterface;
        }

        return t.SpecialType switch
        {
            SpecialType.System_String => ReturnKind.RString,
            SpecialType.System_Boolean => ReturnKind.RBool,
            SpecialType.System_Double => ReturnKind.RDouble,
            SpecialType.System_Single => ReturnKind.RFloat,
            SpecialType.System_Int32 => ReturnKind.RInt32,
            SpecialType.System_Int64 => ReturnKind.RInt64,
            SpecialType.System_Int16 => ReturnKind.RInt16,
            SpecialType.System_Byte => ReturnKind.RByte,
            SpecialType.System_UInt32 => ReturnKind.RUInt32,
            SpecialType.System_UInt64 => ReturnKind.RUInt64,
            SpecialType.System_SByte => ReturnKind.RSByte,
            SpecialType.System_UInt16 => ReturnKind.RUInt16,
            SpecialType.System_Decimal => ReturnKind.RDecimal,
            _ => ReturnKind.Unsupported,
        };
    }

    private static string ConvertArg(ParamKind kind, string typeDisplay, string argExpr, string localName, string memberJsName, int paramIndex)
    {
        return kind switch
        {
            ParamKind.JsValue => $"var {localName} = {argExpr};",
            ParamKind.JsObject =>
                $"var {localName}__raw = {argExpr};\n" +
                $"            if ({localName}__raw is not JsObject {localName}) " +
                $"throw new SuperRender.EcmaScript.Runtime.Errors.JsTypeError(" +
                $"\"{Escape(memberJsName)}: argument {paramIndex} must be an object\", " +
                $"SuperRender.EcmaScript.Runtime.ExecutionContext.CurrentLine, " +
                $"SuperRender.EcmaScript.Runtime.ExecutionContext.CurrentColumn);",
            ParamKind.IJsTypeInterface =>
                $"var {localName} = global::SuperRender.EcmaScript.Runtime.Interop.JsValueExtension.AsInterface<global::{typeDisplay}>({argExpr});",
            ParamKind.PString => $"var {localName} = {argExpr}.ToJsString();",
            ParamKind.PBool => $"var {localName} = {argExpr}.ToBoolean();",
            ParamKind.PDouble => $"var {localName} = {argExpr}.ToNumber();",
            ParamKind.PFloat => $"var {localName} = (float){argExpr}.ToNumber();",
            ParamKind.PInt32 => $"var {localName} = (int){argExpr}.ToNumber();",
            ParamKind.PInt64 => $"var {localName} = (long){argExpr}.ToNumber();",
            ParamKind.PInt16 => $"var {localName} = (short){argExpr}.ToNumber();",
            ParamKind.PByte => $"var {localName} = (byte){argExpr}.ToNumber();",
            ParamKind.PUInt32 => $"var {localName} = (uint){argExpr}.ToNumber();",
            ParamKind.PUInt64 => $"var {localName} = (ulong){argExpr}.ToNumber();",
            ParamKind.PSByte => $"var {localName} = (sbyte){argExpr}.ToNumber();",
            ParamKind.PUInt16 => $"var {localName} = (ushort){argExpr}.ToNumber();",
            ParamKind.PDecimal => $"var {localName} = (decimal){argExpr}.ToNumber();",
            _ => $"// unsupported param kind {kind}",
        };
    }

    private static string WrapReturn(ReturnKind kind, string typeDisplay, string valueExpr)
    {
        return kind switch
        {
            ReturnKind.JsValue => valueExpr,
            ReturnKind.RIJsTypeInterface => $"global::SuperRender.EcmaScript.Runtime.Interop.InteropConversions.UnwrapIJsType({valueExpr})",
            ReturnKind.RString => valueExpr,
            ReturnKind.RBool => valueExpr,
            ReturnKind.RDouble => valueExpr,
            ReturnKind.RFloat => valueExpr,
            ReturnKind.RInt32 => valueExpr,
            ReturnKind.RInt64 => valueExpr,
            ReturnKind.RInt16 => valueExpr,
            ReturnKind.RByte => valueExpr,
            ReturnKind.RUInt32 => valueExpr,
            ReturnKind.RUInt64 => valueExpr,
            ReturnKind.RSByte => valueExpr,
            ReturnKind.RUInt16 => valueExpr,
            ReturnKind.RDecimal => valueExpr,
            _ => valueExpr,
        };
    }

    private static void Emit(SourceProductionContext ctx, JsObjectModel model, string? targetDir)
    {
        foreach (var d in model.Diagnostics)
        {
            ctx.ReportDiagnostic(d);
        }

        if (model.Methods.Length == 0 && model.Properties.Length == 0 && !model.GenerateInterface)
        {
            return;
        }

        if (model.Methods.Length > 0 || model.Properties.Length > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using SuperRender.EcmaScript.Runtime;");
            sb.AppendLine();

            if (model.Namespace is not null)
            {
                sb.Append("namespace ").Append(model.Namespace).AppendLine(";");
                sb.AppendLine();
            }

            sb.Append("partial class ").AppendLine(model.ClassName);
            sb.AppendLine("{");

            foreach (var m in model.Methods)
            {
                sb.Append("    private JsFunction? __jsfn_").Append(SanitizeIdent(m.JsName)).AppendLine(";");
            }

            sb.AppendLine();

            EmitGet(sb, model);
            sb.AppendLine();
            EmitHasProperty(sb, model);

            var setters = model.Properties.Where(p => p.IsSetter).ToList();
            if (setters.Count > 0)
            {
                sb.AppendLine();
                EmitSet(sb, model, setters);
            }

            sb.AppendLine("}");
            ctx.AddSource($"{model.ClassName}.JsObject.g.cs", sb.ToString());
        }

        if (model.GenerateInterface)
        {
            EmitInterface(ctx, model);
            EmitDts(ctx, model, targetDir);
        }
    }

    private static void EmitInterface(SourceProductionContext ctx, JsObjectModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8019");
        sb.AppendLine("using global::SuperRender.EcmaScript.Runtime;");
        sb.AppendLine("using global::SuperRender.EcmaScript.Runtime.Interop;");
        sb.AppendLine();

        if (model.Namespace is not null)
        {
            sb.Append("namespace ").Append(model.Namespace).AppendLine(";");
            sb.AppendLine();
        }

        var ifaceName = "I" + model.ClassName;

        sb.Append("public partial interface ").Append(ifaceName)
          .AppendLine(" : global::SuperRender.EcmaScript.Runtime.Interop.IJsType");
        sb.AppendLine("{");

        foreach (var m in model.Methods)
        {
            if (m.IsLegacy)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    MemberNotRepresentableInInterface,
                    Location.None,
                    model.ClassName, m.CsharpName,
                    "legacy (JsValue, JsValue[]) signatures cannot be represented as typed interface members"));
                continue;
            }

            var retType = m.Return.Kind == ReturnKind.Void ? "void" : m.Return.TypeDisplay;
            sb.Append("    ").Append(retType).Append(' ').Append(m.CsharpName).Append('(');
            for (var i = 0; i < m.Parameters.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                var p = m.Parameters[i];
                if (p.Kind == ParamKind.ArgsArray)
                {
                    sb.Append("params ").Append(p.TypeDisplay).Append(' ').Append(SanitizeIdent(p.CsharpName));
                }
                else
                {
                    sb.Append(p.TypeDisplay).Append(' ').Append(SanitizeIdent(p.CsharpName));
                }
            }

            sb.AppendLine(");");
        }

        foreach (var p in model.Properties)
        {
            if (p.IsSetter && !p.IsCsharpProperty)
            {
                // Setter method — cannot be paired with its getter in the interface; skip.
                ctx.ReportDiagnostic(Diagnostic.Create(
                    MemberNotRepresentableInInterface,
                    Location.None,
                    model.ClassName, p.CsharpName,
                    "setter methods cannot be expressed as interface properties"));
                continue;
            }

            if (p.IsSetter)
            {
                // Setters are paired with their getter via C# property syntax in the interface; skip.
                continue;
            }

            if (p.IsCsharpProperty)
            {
                var hasSetter = model.Properties.Any(x => x.IsSetter && x.CsharpName == p.CsharpName);
                sb.Append("    ").Append(p.ValueTypeDisplay).Append(' ').Append(p.CsharpName)
                  .AppendLine(hasSetter ? " { get; set; }" : " { get; }");
            }
            else
            {
                sb.Append("    ").Append(p.ValueTypeDisplay).Append(' ').Append(p.CsharpName).AppendLine("();");
            }
        }

        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append("partial class ").Append(model.ClassName).Append(" : ").AppendLine(ifaceName);
        sb.AppendLine("{");
        sb.AppendLine("}");

        ctx.AddSource($"{model.ClassName}.JsObject.Interface.g.cs", sb.ToString());
    }

    private static void EmitDts(SourceProductionContext ctx, JsObjectModel model, string? targetDir)
    {
        if (string.IsNullOrEmpty(targetDir))
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.Append("export interface I").Append(model.ClassName).AppendLine(" {");

        foreach (var m in model.Methods)
        {
            if (m.IsLegacy)
            {
                continue;
            }

            sb.Append("    ").Append(m.JsName).Append('(');
            for (var i = 0; i < m.Parameters.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                var p = m.Parameters[i];
                if (p.Kind == ParamKind.ArgsArray)
                {
                    sb.Append("...").Append(SanitizeIdent(p.CsharpName)).Append(": any[]");
                }
                else
                {
                    sb.Append(SanitizeIdent(p.CsharpName)).Append(": ").Append(TsParamType(p.Kind));
                }
            }

            sb.Append("): ").Append(TsReturnType(m.Return.Kind, m.Return.InnerKind)).AppendLine(";");
        }

        var seenProps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in model.Properties)
        {
            if (p.IsSetter)
            {
                continue;
            }

            if (!seenProps.Add(p.JsName))
            {
                continue;
            }

            var hasSetter = model.Properties.Any(x => x.IsSetter && x.JsName == p.JsName);
            var ts = p.ReturnKind is { } rk ? TsReturnType(rk, p.InnerKind) : "any";
            if (!hasSetter)
            {
                sb.Append("    readonly ").Append(p.JsName).Append(": ").Append(ts).AppendLine(";");
            }
            else
            {
                sb.Append("    ").Append(p.JsName).Append(": ").Append(ts).AppendLine(";");
            }
        }

        sb.AppendLine("}");

        var nsPath = model.Namespace is null
            ? string.Empty
            : model.Namespace.Replace('.', Path.DirectorySeparatorChar);
        var dir = Path.Combine(targetDir!, "types", nsPath);
#pragma warning disable RS1035
        try
        {
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, $"I{model.ClassName}.d.ts");
            File.WriteAllText(filePath, sb.ToString());
        }
        catch (IOException)
        {
            // ignore IO races — .d.ts is best-effort
        }
        catch (UnauthorizedAccessException)
        {
            // ignore — likely IDE read-only scenario
        }
#pragma warning restore RS1035
    }

    private static string TsParamType(ParamKind kind) => kind switch
    {
        ParamKind.PString => "string",
        ParamKind.PBool => "boolean",
        ParamKind.PDouble or ParamKind.PFloat or ParamKind.PInt32 or ParamKind.PInt64
            or ParamKind.PInt16 or ParamKind.PByte or ParamKind.PUInt32 or ParamKind.PUInt64
            or ParamKind.PSByte or ParamKind.PUInt16 or ParamKind.PDecimal => "number",
        _ => "any",
    };

    private static string TsReturnType(ReturnKind kind, ReturnKind innerKind = ReturnKind.Void) => kind switch
    {
        ReturnKind.Void => "void",
        ReturnKind.ROptional => TsReturnType(innerKind) + " | undefined",
        ReturnKind.RString => "string",
        ReturnKind.RBool => "boolean",
        ReturnKind.RDouble or ReturnKind.RFloat or ReturnKind.RInt32 or ReturnKind.RInt64
            or ReturnKind.RInt16 or ReturnKind.RByte or ReturnKind.RUInt32 or ReturnKind.RUInt64
            or ReturnKind.RSByte or ReturnKind.RUInt16 or ReturnKind.RDecimal => "number",
        _ => "any",
    };

    private static void EmitGet(StringBuilder sb, JsObjectModel model)
    {
        sb.AppendLine("    public override JsValue Get(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (name)");
        sb.AppendLine("        {");

        foreach (var m in model.Methods)
        {
            sb.Append("            case \"").Append(Escape(m.JsName)).AppendLine("\":");
            sb.Append("                return __jsfn_").Append(SanitizeIdent(m.JsName))
              .Append(" ??= JsFunction.CreateNative(\"").Append(Escape(m.JsName)).AppendLine("\",");
            EmitMethodTrampoline(sb, model, m);
            sb.Append("                    ").Append(m.Length).AppendLine(");");
        }

        foreach (var p in model.Properties.Where(p => !p.IsSetter))
        {
            sb.Append("            case \"").Append(Escape(p.JsName)).AppendLine("\":");
            var target = p.IsStatic ? model.ClassName : "this";
            var access = p.IsCsharpProperty ? $"{target}.{p.CsharpName}" : $"{target}.{p.CsharpName}()";
            if (p.ReturnKind == ReturnKind.ROptional)
            {
                sb.AppendLine("            {");
                sb.Append("                var __r = ").Append(access).AppendLine(";");
                var innerWrapped = WrapReturn(p.InnerKind, p.InnerTypeDisplay ?? string.Empty, "__r.Value!");
                sb.Append("                return __r.HasValue ? ").Append(innerWrapped).AppendLine(" : JsValue.Undefined;");
                sb.AppendLine("            }");
            }
            else
            {
                var wrapped = WrapReturn(p.ReturnKind ?? ReturnKind.JsValue, p.ValueTypeDisplay, access);
                sb.Append("                return ").Append(wrapped).AppendLine(";");
            }
        }

        sb.AppendLine("            default:");
        sb.AppendLine("                return base.Get(name);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static void EmitMethodTrampoline(StringBuilder sb, JsObjectModel model, MethodModel m)
    {
        string target;
        if (m.IsStatic)
        {
            target = model.ClassName;
        }
        else
        {
            target = $"(({model.ClassName})__thisArg)";
        }

        if (m.IsLegacy)
        {
            if (m.IsStatic)
            {
                sb.Append("                    ").Append(model.ClassName).Append('.').Append(m.CsharpName).AppendLine(",");
            }
            else
            {
                sb.Append("                    this.").Append(m.CsharpName).AppendLine(",");
            }

            return;
        }

        sb.AppendLine("                    static (__thisArg, __args) =>");
        sb.AppendLine("                    {");

        var callArgs = new List<string>();
        for (var i = 0; i < m.Parameters.Length; i++)
        {
            var p = m.Parameters[i];
            var local = "__p" + i;
            if (p.Kind == ParamKind.ArgsArray)
            {
                if (i == 0)
                {
                    sb.AppendLine($"                        var {local} = __args;");
                }
                else
                {
                    sb.AppendLine($"                        var {local} = __args.Length > {i} ? __args[{i}..] : System.Array.Empty<JsValue>();");
                }

                callArgs.Add(local);
            }
            else
            {
                var argExpr = $"(__args.Length > {i} ? __args[{i}] : JsValue.Undefined)";
                var convert = ConvertArg(p.Kind, p.TypeDisplay, argExpr, local, m.JsName, i);
                sb.Append("                        ").AppendLine(convert);
                callArgs.Add(local);
            }
        }

        var call = $"{target}.{m.CsharpName}({string.Join(", ", callArgs)})";
        if (m.Return.Kind == ReturnKind.Void)
        {
            sb.Append("                        ").Append(call).AppendLine(";");
            sb.AppendLine("                        return JsValue.Undefined;");
        }
        else if (m.Return.Kind == ReturnKind.ROptional)
        {
            sb.Append("                        var __r = ").Append(call).AppendLine(";");
            var innerWrapped = WrapReturn(m.Return.InnerKind, m.Return.InnerTypeDisplay ?? string.Empty, "__r.Value!");
            sb.Append("                        return __r.HasValue ? ").Append(innerWrapped).AppendLine(" : JsValue.Undefined;");
        }
        else
        {
            var wrapped = WrapReturn(m.Return.Kind, m.Return.TypeDisplay, call);
            sb.Append("                        return ").Append(wrapped).AppendLine(";");
        }

        sb.AppendLine("                    },");
    }

    private static void EmitHasProperty(StringBuilder sb, JsObjectModel model)
    {
        sb.AppendLine("    public override bool HasProperty(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (name)");
        sb.AppendLine("        {");

        var names = new List<string>();
        foreach (var m in model.Methods)
        {
            names.Add(m.JsName);
        }

        foreach (var p in model.Properties.Where(p => !p.IsSetter))
        {
            names.Add(p.JsName);
        }

        if (names.Count > 0)
        {
            foreach (var n in names)
            {
                sb.Append("            case \"").Append(Escape(n)).AppendLine("\":");
            }

            sb.AppendLine("                return true;");
        }

        sb.AppendLine("            default:");
        sb.AppendLine("                return base.HasProperty(name);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static void EmitSet(StringBuilder sb, JsObjectModel model, List<PropertyModel> setters)
    {
        sb.AppendLine("    public override void Set(string name, JsValue value)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (name)");
        sb.AppendLine("        {");

        foreach (var s in setters)
        {
            sb.Append("            case \"").Append(Escape(s.JsName)).AppendLine("\":");
            sb.AppendLine("            {");
            var target = s.IsStatic ? model.ClassName : "this";
            var kind = s.SetterParamKind ?? ParamKind.JsValue;
            var local = "__v";
            var convert = ConvertArg(kind, s.ValueTypeDisplay, "value", local, s.JsName, 0);
            sb.Append("                ").AppendLine(convert);
            if (s.IsCsharpProperty)
            {
                sb.Append("                ").Append(target).Append('.').Append(s.CsharpName).Append(" = ").Append(local).AppendLine(";");
            }
            else
            {
                sb.Append("                ").Append(target).Append('.').Append(s.CsharpName).Append('(').Append(local).AppendLine(");");
            }
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine("            default:");
        sb.AppendLine("                base.Set(name, value);");
        sb.AppendLine("                return;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static string SanitizeIdent(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private enum ParamKind
    {
        Unsupported,
        JsValue,
        JsObject,
        IJsTypeInterface,
        ArgsArray,
        PString,
        PBool,
        PDouble,
        PFloat,
        PInt32,
        PInt64,
        PInt16,
        PByte,
        PUInt32,
        PUInt64,
        PSByte,
        PUInt16,
        PDecimal,
    }

    private enum ReturnKind
    {
        Unsupported,
        Void,
        JsValue,
        RIJsTypeInterface,
        ROptional,
        RString,
        RBool,
        RDouble,
        RFloat,
        RInt32,
        RInt64,
        RInt16,
        RByte,
        RUInt32,
        RUInt64,
        RSByte,
        RUInt16,
        RDecimal,
    }

    private sealed record JsObjectModel(
        string? Namespace,
        string ClassName,
        bool IsSealed,
        bool GenerateInterface,
        ImmutableArray<MethodModel> Methods,
        ImmutableArray<PropertyModel> Properties,
        ImmutableArray<Diagnostic> Diagnostics);

    private sealed record MethodModel(
        string JsName,
        string CsharpName,
        bool IsStatic,
        int Length,
        bool IsLegacy,
        ImmutableArray<ParamModel> Parameters,
        ReturnModel Return);

    private sealed record ParamModel(string CsharpName, string TypeDisplay, ParamKind Kind);

    private sealed record ReturnModel(
        string TypeDisplay,
        ReturnKind Kind,
        ReturnKind InnerKind = ReturnKind.Void,
        string? InnerTypeDisplay = null);

    private sealed record PropertyModel(
        string JsName,
        string CsharpName,
        bool IsCsharpProperty,
        bool IsStatic,
        bool IsSetter,
        string ValueTypeDisplay,
        ParamKind? SetterParamKind,
        ReturnKind? ReturnKind,
        ReturnKind InnerKind = JsObjectGenerator.ReturnKind.Void,
        string? InnerTypeDisplay = null);

    private const string AttributeSource = """
        // <auto-generated/>
        #nullable enable
        using System;

        namespace SuperRender.EcmaScript.Runtime;

        [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
        internal sealed class JsObjectAttribute : Attribute
        {
            public string? Name { get; set; }
            public bool GenerateInterface { get; set; }
        }

        [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
        internal sealed class JsMethodAttribute : Attribute
        {
            public JsMethodAttribute(string name) { Name = name; }
            public string Name { get; }
        }

        [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        internal sealed class JsPropertyAttribute : Attribute
        {
            public JsPropertyAttribute(string name) { Name = name; }
            public string Name { get; }
        }

        [Flags]
        internal enum JsPropertyAccess
        {
            Get = 1,
            Set = 2
        }
        """;
}
