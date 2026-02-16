using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class GoToDefinitionTool
{
    private readonly RpcClient _rpc;
    public GoToDefinitionTool(RpcClient rpc) => _rpc = rpc;

    [McpServerTool(Name = "roslyn_go_to_definition")]
    [Description("Navigates to the definition of a symbol at a given position using Roslyn's semantic model. Returns the file path and location of the symbol's definition, including metadata references.")]
    public async Task<string> GoToDefinition(
        [Description("Absolute path to the C# file")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column)
    {
        var result = await _rpc.GoToDefinitionAsync(filePath, line, column);

        if (result.ErrorMessage != null)
            return $"Error: {result.ErrorMessage}";

        var sb = new StringBuilder();
        sb.AppendLine($"Symbol: {result.SymbolName} ({result.SymbolKind})");

        if (result.ContainingType != null)
            sb.AppendLine($"Containing type: {result.ContainingType}");
        if (result.ContainingNamespace != null)
            sb.AppendLine($"Namespace: {result.ContainingNamespace}");

        sb.AppendLine($"\nDefinitions ({result.Definitions.Count}):");
        foreach (var d in result.Definitions)
        {
            if (d.IsFromMetadata)
            {
                sb.AppendLine($"  [Metadata] Assembly: {d.AssemblyName}");
                sb.AppendLine($"    {d.Preview}");
            }
            else
            {
                sb.AppendLine($"  {d.FilePath}:{d.StartLine}:{d.StartColumn}");
                sb.AppendLine($"    {d.Preview}");
            }
        }

        return sb.ToString();
    }
}
