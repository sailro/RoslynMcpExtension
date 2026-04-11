using System.Collections.Generic;

namespace RoslynMcpExtension.Shared;

public class SymbolLocation
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MemberType { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public string? Accessibility { get; set; }
    public string? ReturnType { get; set; }
    public List<string>? Modifiers { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
}

public class DiagnosticInfo
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
}

public class SymbolListResult
{
    public SymbolLocation? Symbol { get; set; }
    public List<SymbolLocation> Members { get; set; } = [];
    public int TotalCount { get; set; }
    public bool Truncated { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SymbolInfoResult
{
    public SymbolLocation? Symbol { get; set; }
    public string? Detail { get; set; }
    public string? Documentation { get; set; }
    public string? ErrorMessage { get; set; }
}

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
