using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class DeadCodeTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_find_dead_code")]
	[Description("Finds potentially dead code in the active Visual Studio workspace by looking for types, methods, and fields with no source references in the current solution. By default it focuses on non-public members; public APIs can be included explicitly.")]
	public async Task<string> FindDeadCode(
		[Description("Maximum number of dead code entries to return (default: 200)")] int maxResults = 200,
		[Description("Include internal and private members (default: true). When false, only private members are reported unless includePublic is also true.")] bool includeInternal = true,
		[Description("Include public, protected, and protected internal members (default: false). This is broader and may include externally used APIs.")] bool includePublic = false)
	{
		var result = await rpc.FindDeadCodeAsync(maxResults, includeInternal, includePublic);

		if (result.ErrorMessage != null)
			return $"Error: {result.ErrorMessage}";

		if (result.TotalCount == 0)
			return "No dead code candidates found in the active workspace.";

		var sb = new StringBuilder();
		sb.AppendLine($"Potential dead code members: {result.TotalCount}{(result.Truncated ? $" (showing first {result.Members.Count})" : "")}");

		foreach (var member in result.Members)
		{
			sb.AppendLine();
			sb.AppendLine($"- FullName: {member.FullName}");
			sb.AppendLine($"  MemberType: {member.Kind}");
			sb.AppendLine($"  FilePath: {member.FilePath}");
			sb.AppendLine($"  Location: {member.StartLine}:{member.StartColumn}");
			sb.AppendLine($"  Project: {member.ProjectName}");
		}

		return sb.ToString();
	}
}
