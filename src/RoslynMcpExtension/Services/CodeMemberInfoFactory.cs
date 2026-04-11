using Microsoft.CodeAnalysis;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal static class CodeMemberInfoFactory
{
    public static SymbolLocation Create(
        ISymbol? symbol,
        string fallbackName,
        string fallbackMemberType,
        Location? location = null,
        string? projectName = null)
    {
        var info = new SymbolLocation
        {
            Name = fallbackName,
            FullName = fallbackName,
            MemberType = fallbackMemberType,
            ProjectName = projectName
        };

        if (location?.IsInSource == true)
        {
            var lineSpan = location.GetLineSpan();
            info.FilePath = location.SourceTree?.FilePath ?? string.Empty;
            info.StartLine = lineSpan.StartLinePosition.Line + 1;
            info.StartColumn = lineSpan.StartLinePosition.Character + 1;
            info.EndLine = lineSpan.EndLinePosition.Line + 1;
        }

        if (symbol == null)
            return info;

        if (string.IsNullOrWhiteSpace(info.Name))
            info.Name = symbol.Name;

        info.FullName = symbol.ToDisplayString();
        info.MemberType = GetMemberType(symbol);
        info.Accessibility = symbol.DeclaredAccessibility.ToString();

        return info;
    }

    public static SymbolInfoResult CreateSymbolInfo(
        ISymbol symbol,
        Location? location = null)
    {
        return new SymbolInfoResult
        {
            Symbol = Create(symbol, symbol.Name, GetMemberType(symbol), location),
            Detail = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Documentation = symbol.GetDocumentationCommentXml()
        };
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
