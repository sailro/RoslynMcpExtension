using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class GoToDefinitionTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_go_to_definition")]
    [Description("Navigates to the definition of a symbol at a given position using Roslyn's semantic model. Returns the file path and location of the symbol's definition, including metadata references.")]
    public async Task<string> GoToDefinition(
        [Description("Absolute path to the C# file")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column)
    {
        var result = await rpc.GoToDefinitionAsync(filePath, line, column);

        if (result.ErrorMessage != null)
            return $"Error: {result.ErrorMessage}";

        var sb = new StringBuilder();
        if (result.Member != null)
            sb.AppendLine($"Symbol: {result.Member.FullName} ({result.Member.MemberType})");

        if (result.Member?.ContainingType != null)
            sb.AppendLine($"Containing type: {result.Member.ContainingType}");
        if (result.Member?.ContainingNamespace != null)
            sb.AppendLine($"Namespace: {result.Member.ContainingNamespace}");

        sb.AppendLine($"\nDefinitions ({result.Definitions.Count}):");
        foreach (var d in result.Definitions)
        {
	        if (d.IsFromMetadata)
            {
                sb.AppendLine($"  [Metadata] Assembly: {d.AssemblyName}");
            }
            else
            {
                sb.AppendLine($"  {d.FilePath}:{d.StartLine}:{d.StartColumn}");
            }
	        sb.AppendLine($"    {d.Preview}");
        }

        return sb.ToString();
    }
}
