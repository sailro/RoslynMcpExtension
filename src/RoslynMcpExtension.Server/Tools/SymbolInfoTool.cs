using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class SymbolInfoTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_get_symbol_info")]
    [Description("Gets detailed type and metadata information for a symbol at a specific position. Returns accessibility, modifiers, base types, interfaces, parameters, and XML documentation.")]
    public async Task<string> GetSymbolInfo(
        [Description("Absolute path to the C# file")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column)
    {
        var result = await rpc.GetSymbolInfoAsync(filePath, line, column);

        if (result.ErrorMessage != null)
            return $"Error: {result.ErrorMessage}";

        if (!result.Found || result.Member == null)
            return "No symbol information available.";

        var member = result.Member;
        var sb = new StringBuilder();
        sb.AppendLine($"Symbol: {member.FullName}");
        sb.AppendLine($"Member type: {member.MemberType}");
        sb.AppendLine($"Accessibility: {member.Accessibility}");

        if (member.TypeName != null)
            sb.AppendLine($"Type kind: {member.TypeName}");
        if (member.ReturnType != null)
            sb.AppendLine($"Return/Type: {member.ReturnType}");

        var flags = new[] {
            member.IsStatic ? "static" : null,
            member.IsAbstract ? "abstract" : null,
            member.IsVirtual ? "virtual" : null,
            member.IsOverride ? "override" : null,
            member.IsSealed ? "sealed" : null
        }
	        .Where(f => f != null)
	        .ToArray();

        if (flags.Length != 0)
            sb.AppendLine($"Modifiers: {string.Join(", ", flags)}");

        if (member.ContainingType != null)
            sb.AppendLine($"Containing type: {member.ContainingType}");
        if (member.ContainingNamespace != null)
            sb.AppendLine($"Namespace: {member.ContainingNamespace}");

        if (member.BaseTypes.Count > 0)
            sb.AppendLine($"Base types: {string.Join(", ", member.BaseTypes)}");
        if (member.Interfaces.Count > 0)
            sb.AppendLine($"Interfaces: {string.Join(", ", member.Interfaces)}");

        if (member.Parameters.Count > 0)
        {
            sb.AppendLine("Parameters:");
            foreach (var p in member.Parameters)
            {
                var opt = p.IsOptional ? $" = {p.DefaultValue ?? "default"}" : "";
                sb.AppendLine($"  {p.Type} {p.Name}{opt}");
            }
        }

        if (!string.IsNullOrWhiteSpace(member.Documentation))
            sb.AppendLine($"\nDocumentation:\n{member.Documentation}");

        return sb.ToString();
    }
}
