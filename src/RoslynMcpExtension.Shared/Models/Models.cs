using System.Collections.Generic;

namespace RoslynMcpExtension.Shared.Models;

public class ValidateFileResult
{
    public bool Success { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public List<DiagnosticInfo> Errors { get; set; } = new();
    public List<DiagnosticInfo> Warnings { get; set; } = new();
    public List<DiagnosticInfo> AnalyzerDiagnostics { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class DiagnosticInfo
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string? Category { get; set; }
}

public class FindReferencesResult
{
    public bool Found { get; set; }
    public string SymbolName { get; set; } = string.Empty;
    public string SymbolKind { get; set; } = string.Empty;
    public List<ReferenceLocationInfo> References { get; set; } = new();
    public int TotalCount { get; set; }
    public bool Truncated { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ReferenceLocationInfo
{
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string Preview { get; set; } = string.Empty;
    public string? ContainingMember { get; set; }
    public bool IsDefinition { get; set; }
}

public class GoToDefinitionResult
{
    public bool Found { get; set; }
    public string SymbolName { get; set; } = string.Empty;
    public string SymbolKind { get; set; } = string.Empty;
    public string? ContainingType { get; set; }
    public string? ContainingNamespace { get; set; }
    public List<DefinitionLocationInfo> Definitions { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class DefinitionLocationInfo
{
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string Preview { get; set; } = string.Empty;
    public bool IsFromMetadata { get; set; }
    public string? AssemblyName { get; set; }
}

public class DocumentSymbolInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? ReturnType { get; set; }
    public string Accessibility { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public List<string> Modifiers { get; set; } = new();
    public List<DocumentSymbolInfo> Children { get; set; } = new();
}

public class SearchSymbolsResult
{
    public List<SymbolSearchInfo> Symbols { get; set; } = new();
    public int TotalCount { get; set; }
    public bool Truncated { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SymbolSearchInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public string? ContainingType { get; set; }
    public string? ContainingNamespace { get; set; }
}

public class SymbolDetailInfo
{
    public bool Found { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? TypeName { get; set; }
    public string? ReturnType { get; set; }
    public string Accessibility { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public bool IsSealed { get; set; }
    public string? ContainingType { get; set; }
    public string? ContainingNamespace { get; set; }
    public List<string> BaseTypes { get; set; } = new();
    public List<string> Interfaces { get; set; } = new();
    public List<ParameterInfo> Parameters { get; set; } = new();
    public string? Documentation { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public string? DefaultValue { get; set; }
}

public class ComplexityInfo
{
    public string MemberName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int CyclomaticComplexity { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int LineCount { get; set; }
    public string Rating { get; set; } = string.Empty;
}
