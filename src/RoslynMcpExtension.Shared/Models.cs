using System.Collections.Generic;

namespace RoslynMcpExtension.Shared;

public class ValidateFileResult
{
    public bool Success { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public List<DiagnosticInfo> Errors { get; set; } = [];
    public List<DiagnosticInfo> Warnings { get; set; } = [];
    public List<DiagnosticInfo> AnalyzerDiagnostics { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public class SourceLocationInfo
{
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
}

public class DiagnosticInfo : SourceLocationInfo
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public class FindReferencesResult
    : MemberLookupResult
{
    public List<ReferenceLocationInfo> References { get; set; } = [];
    public int TotalCount { get; set; }
    public bool Truncated { get; set; }
}

public class ReferenceLocationInfo : SourceLocationInfo
{
    public string Preview { get; set; } = string.Empty;
    public string? ContainingMember { get; set; }
    public bool IsDefinition { get; set; }
}

public class GoToDefinitionResult
    : MemberLookupResult
{
    public List<DefinitionLocationInfo> Definitions { get; set; } = [];
}

public class DefinitionLocationInfo : SourceLocationInfo
{
    public string Preview { get; set; } = string.Empty;
    public bool IsFromMetadata { get; set; }
    public string? AssemblyName { get; set; }
}

public class MemberQueryResult
{
    public List<CodeMemberInfo> Members { get; set; } = [];
    public int TotalCount { get; set; }
    public bool Truncated { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MemberLookupResult
{
    public bool Found { get; set; }
    public CodeMemberInfo? Member { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SearchSymbolsResult : MemberQueryResult
{
}

public class DeadCodeAnalysisResult : MemberQueryResult
{
}

public class CodeMemberInfo : SourceLocationInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MemberType { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public string? ContainingType { get; set; }
    public string? ContainingNamespace { get; set; }
    public string? ReturnType { get; set; }
    public string? TypeName { get; set; }
    public string Accessibility { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public bool IsSealed { get; set; }
    public List<string> Modifiers { get; set; } = [];
    public List<string> BaseTypes { get; set; } = [];
    public List<string> Interfaces { get; set; } = [];
    public List<ParameterInfo> Parameters { get; set; } = [];
    public string? Documentation { get; set; }
    public List<CodeMemberInfo> Children { get; set; } = [];
}

public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public string? DefaultValue { get; set; }
}
