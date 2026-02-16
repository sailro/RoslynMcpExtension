using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class SymbolInfoTool
{
    private readonly RpcClient _rpc;
    public SymbolInfoTool(RpcClient rpc) => _rpc = rpc;

    [McpServerTool(Name = "roslyn_get_symbol_info")]
    [Description("Gets detailed type and metadata information for a symbol at a specific position. Returns accessibility, modifiers, base types, interfaces, parameters, and XML documentation.")]
    public async Task<string> GetSymbolInfo(
        [Description("Absolute path to the C# file")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column)
    {
        var result = await _rpc.GetSymbolInfoAsync(filePath, line, column);

        if (result.ErrorMessage != null)
            return $"Error: {result.ErrorMessage}";

        var sb = new StringBuilder();
        sb.AppendLine($"Symbol: {result.FullName}");
        sb.AppendLine($"Kind: {result.Kind}");
        sb.AppendLine($"Accessibility: {result.Accessibility}");

        if (result.TypeName != null)
            sb.AppendLine($"Type kind: {result.TypeName}");
        if (result.ReturnType != null)
            sb.AppendLine($"Return/Type: {result.ReturnType}");

        var flags = new[] {
            result.IsStatic ? "static" : null,
            result.IsAbstract ? "abstract" : null,
            result.IsVirtual ? "virtual" : null,
            result.IsOverride ? "override" : null,
            result.IsSealed ? "sealed" : null
        }.Where(f => f != null);

        if (flags.Any())
            sb.AppendLine($"Modifiers: {string.Join(", ", flags)}");

        if (result.ContainingType != null)
            sb.AppendLine($"Containing type: {result.ContainingType}");
        if (result.ContainingNamespace != null)
            sb.AppendLine($"Namespace: {result.ContainingNamespace}");

        if (result.BaseTypes.Count > 0)
            sb.AppendLine($"Base types: {string.Join(", ", result.BaseTypes)}");
        if (result.Interfaces.Count > 0)
            sb.AppendLine($"Interfaces: {string.Join(", ", result.Interfaces)}");

        if (result.Parameters.Count > 0)
        {
            sb.AppendLine("Parameters:");
            foreach (var p in result.Parameters)
            {
                var opt = p.IsOptional ? $" = {p.DefaultValue ?? "default"}" : "";
                sb.AppendLine($"  {p.Type} {p.Name}{opt}");
            }
        }

        if (!string.IsNullOrWhiteSpace(result.Documentation))
            sb.AppendLine($"\nDocumentation:\n{result.Documentation}");

        return sb.ToString();
    }
}
