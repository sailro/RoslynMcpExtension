using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class ComplexityTool
{
    private readonly RpcClient _rpc;
    public ComplexityTool(RpcClient rpc) => _rpc = rpc;

    [McpServerTool(Name = "roslyn_analyze_complexity")]
    [Description("Analyzes cyclomatic complexity of methods, constructors, and property accessors in a C# file. Rates each member as Low (1-5), Moderate (6-10), High (11-20), or Very High (>20).")]
    public async Task<string> AnalyzeComplexity(
        [Description("Absolute path to the C# file to analyze")] string filePath)
    {
        var results = await _rpc.AnalyzeComplexityAsync(filePath);

        if (results.Count == 0)
            return "No methods or members found to analyze (file may not be in the current solution).";

        var sb = new StringBuilder();
        sb.AppendLine($"Complexity analysis for {filePath}:");
        sb.AppendLine($"Members analyzed: {results.Count}");

        var sorted = results.OrderByDescending(r => r.CyclomaticComplexity).ToList();

        sb.AppendLine($"\n{"Member",-40} {"Kind",-15} {"Complexity",10} {"Lines",6} {"Rating",-10}");
        sb.AppendLine(new string('-', 85));

        foreach (var r in sorted)
        {
            sb.AppendLine($"{r.MemberName,-40} {r.Kind,-15} {r.CyclomaticComplexity,10} {r.LineCount,6} {r.Rating,-10}");
        }

        var avg = sorted.Average(r => r.CyclomaticComplexity);
        var highCount = sorted.Count(r => r.CyclomaticComplexity > 10);
        sb.AppendLine($"\nAverage complexity: {avg:F1}");
        if (highCount > 0)
            sb.AppendLine($"⚠️ {highCount} member(s) with high complexity (>10)");

        return sb.ToString();
    }
}
