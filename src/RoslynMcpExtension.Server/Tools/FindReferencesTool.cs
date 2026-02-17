using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class FindReferencesTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_find_references")]
    [Description("Finds all references to a symbol at a given position using Roslyn's semantic analysis. Returns definition and usage locations across the entire solution. Much more accurate than text search.")]
    public async Task<string> FindReferences(
        [Description("Absolute path to the C# file")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column,
        [Description("Maximum number of results to return (default: 50)")] int maxResults = 50)
    {
        var result = await rpc.FindReferencesAsync(filePath, line, column, maxResults);

        if (result.ErrorMessage != null)
            return $"Error: {result.ErrorMessage}";

        var sb = new StringBuilder();
        sb.AppendLine($"Symbol: {result.SymbolName} ({result.SymbolKind})");
        sb.AppendLine($"References found: {result.TotalCount}{(result.Truncated ? " (truncated)" : "")}");

        foreach (var r in result.References)
        {
            var tag = r.IsDefinition ? " [DEFINITION]" : "";
            var member = r.ContainingMember != null ? $" in {r.ContainingMember}" : "";
            sb.AppendLine($"  {r.FilePath}:{r.StartLine}:{r.StartColumn}{tag}{member}");
            sb.AppendLine($"    {r.Preview}");
        }

        return sb.ToString();
    }
}
