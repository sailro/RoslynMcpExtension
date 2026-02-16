using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class ValidateFileTool
{
    private readonly RpcClient _rpc;
    public ValidateFileTool(RpcClient rpc) => _rpc = rpc;

    [McpServerTool(Name = "roslyn_validate_file")]
    [Description("Validates a C# file using the live Roslyn workspace. Returns compiler errors, warnings, and optional analyzer diagnostics. The file must be part of the currently open Visual Studio solution.")]
    public async Task<string> ValidateFile(
        [Description("Absolute path to the C# file to validate")] string filePath,
        [Description("Include warnings in output (default: true)")] bool includeWarnings = true,
        [Description("Run code analyzers in addition to compiler diagnostics (default: false)")] bool runAnalyzers = false)
    {
        var result = await _rpc.ValidateFileAsync(filePath, includeWarnings, runAnalyzers);

        if (result.ErrorMessage != null)
            return $"Error: {result.ErrorMessage}";

        var sb = new StringBuilder();
        sb.AppendLine($"File: {result.FilePath}");
        sb.AppendLine($"Project: {result.ProjectName ?? "unknown"}");
        sb.AppendLine($"Status: {(result.Success ? "✅ Valid" : "❌ Has errors")}");

        if (result.Errors.Count > 0)
        {
            sb.AppendLine($"\n--- Errors ({result.Errors.Count}) ---");
            foreach (var e in result.Errors)
                sb.AppendLine($"  {e.Id} [{e.StartLine}:{e.StartColumn}]: {e.Message}");
        }

        if (result.Warnings.Count > 0)
        {
            sb.AppendLine($"\n--- Warnings ({result.Warnings.Count}) ---");
            foreach (var w in result.Warnings)
                sb.AppendLine($"  {w.Id} [{w.StartLine}:{w.StartColumn}]: {w.Message}");
        }

        if (result.AnalyzerDiagnostics.Count > 0)
        {
            sb.AppendLine($"\n--- Analyzer Diagnostics ({result.AnalyzerDiagnostics.Count}) ---");
            foreach (var a in result.AnalyzerDiagnostics)
                sb.AppendLine($"  {a.Id} [{a.Severity}] [{a.StartLine}:{a.StartColumn}]: {a.Message}");
        }

        return sb.ToString();
    }
}
