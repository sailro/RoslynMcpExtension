using System.Linq;
using Microsoft.CodeAnalysis;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal static class CodeMemberInfoFactory
{
    public static CodeMemberInfo Create(
        ISymbol? symbol,
        string fallbackName,
        string fallbackMemberType,
        Location? location = null,
        string? projectName = null)
    {
        var info = new CodeMemberInfo();
        Populate(info, symbol, fallbackName, fallbackMemberType, location, projectName);
        return info;
    }

    public static DetailedCodeMemberInfo CreateDetailed(
        ISymbol? symbol,
        string fallbackName,
        string fallbackMemberType,
        Location? location = null,
        string? projectName = null)
    {
        var info = new DetailedCodeMemberInfo();
        Populate(info, symbol, fallbackName, fallbackMemberType, location, projectName);
        PopulateDetails(info, symbol);
        return info;
    }

    public static DocumentSymbolInfo CreateDocumentSymbol(
        ISymbol? symbol,
        string fallbackName,
        string fallbackMemberType,
        Location? location = null,
        string? projectName = null)
    {
        var info = new DocumentSymbolInfo();
        Populate(info, symbol, fallbackName, fallbackMemberType, location, projectName);

        switch (symbol)
        {
            case IMethodSymbol methodSymbol:
                info.ReturnType = methodSymbol.ReturnType.ToDisplayString();
                break;
            case IPropertySymbol propertySymbol:
                info.ReturnType = propertySymbol.Type.ToDisplayString();
                break;
            case IFieldSymbol fieldSymbol:
                info.ReturnType = fieldSymbol.Type.ToDisplayString();
                break;
            case IEventSymbol eventSymbol:
                info.ReturnType = eventSymbol.Type.ToDisplayString();
                break;
        }

        return info;
    }

    private static void Populate(
        CodeMemberInfo info,
        ISymbol? symbol,
        string fallbackName,
        string fallbackMemberType,
        Location? location,
        string? projectName)
    {
        info.Name = fallbackName;
        info.FullName = fallbackName;
        info.MemberType = fallbackMemberType;
        info.ProjectName = projectName;

        if (location?.IsInSource == true)
        {
            var lineSpan = location.GetLineSpan();
            info.FilePath = location.SourceTree?.FilePath ?? string.Empty;
            info.StartLine = lineSpan.StartLinePosition.Line + 1;
            info.StartColumn = lineSpan.StartLinePosition.Character + 1;
            info.EndLine = lineSpan.EndLinePosition.Line + 1;
            info.EndColumn = lineSpan.EndLinePosition.Character + 1;
        }

        if (symbol == null)
            return;

        if (string.IsNullOrWhiteSpace(info.Name))
            info.Name = symbol.Name;

        info.FullName = symbol.ToDisplayString();
        info.MemberType = GetMemberType(symbol);
        info.Accessibility = symbol.DeclaredAccessibility.ToString();
    }

    private static void PopulateDetails(DetailedCodeMemberInfo info, ISymbol? symbol)
    {
        if (symbol == null)
            return;

        info.ContainingType = symbol.ContainingType?.ToDisplayString();
        info.ContainingNamespace = symbol.ContainingNamespace?.ToDisplayString();
        info.IsStatic = symbol.IsStatic;
        info.IsAbstract = symbol.IsAbstract;
        info.IsVirtual = symbol.IsVirtual;
        info.IsOverride = symbol.IsOverride;
        info.IsSealed = symbol.IsSealed;
        info.Documentation = symbol.GetDocumentationCommentXml();

        switch (symbol)
        {
            case INamedTypeSymbol typeSymbol:
                info.TypeName = typeSymbol.TypeKind.ToString();
                if (typeSymbol.BaseType != null)
                    info.BaseTypes.Add(typeSymbol.BaseType.ToDisplayString());
                info.Interfaces = [.. typeSymbol.Interfaces.Select(i => i.ToDisplayString())];
                break;
            case IMethodSymbol methodSymbol:
                info.ReturnType = methodSymbol.ReturnType.ToDisplayString();
                info.Parameters = [.. methodSymbol.Parameters.Select(p => new ParameterInfo
                {
                    Name = p.Name,
                    Type = p.Type.ToDisplayString(),
                    IsOptional = p.IsOptional,
                    DefaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null
                })];
                break;
            case IPropertySymbol propertySymbol:
                info.ReturnType = propertySymbol.Type.ToDisplayString();
                break;
            case IEventSymbol eventSymbol:
                info.ReturnType = eventSymbol.Type.ToDisplayString();
                break;
            case IFieldSymbol fieldSymbol:
                info.ReturnType = fieldSymbol.Type.ToDisplayString();
                break;
            case ILocalSymbol localSymbol:
                info.ReturnType = localSymbol.Type.ToDisplayString();
                break;
            case IParameterSymbol parameterSymbol:
                info.ReturnType = parameterSymbol.Type.ToDisplayString();
                break;
        }
    }

    public static string GetMemberType(ISymbol symbol)
    {
        return symbol switch
        {
            INamespaceSymbol => "namespace",
            INamedTypeSymbol { IsRecord: true } => "record",
            INamedTypeSymbol typeSymbol => typeSymbol.TypeKind switch
            {
                TypeKind.Class => "class",
                TypeKind.Struct => "struct",
                TypeKind.Interface => "interface",
                TypeKind.Enum => "enum",
                TypeKind.Delegate => "delegate",
                _ => "type"
            },
            IMethodSymbol { MethodKind: MethodKind.Constructor } => "constructor",
            IMethodSymbol => "method",
            IPropertySymbol => "property",
            IFieldSymbol => "field",
            IEventSymbol => "event",
            IParameterSymbol => "parameter",
            ILocalSymbol => "local",
            _ => symbol.Kind.ToString().ToLowerInvariant()
        };
    }
}
