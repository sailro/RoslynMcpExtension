using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Server.Tools;

[McpServerToolType]
public sealed class DocumentSymbolsTool(RpcClient rpc)
{
	[McpServerTool(Name = "roslyn_get_document_symbols")]
    [Description("Lists all symbols (classes, methods, properties, fields, etc.) in a C# file with their types, accessibility modifiers, and line spans. Uses the live Roslyn semantic model.")]
    public async Task<string> GetDocumentSymbols([Description("Absolute path to the C# file")] string filePath)
    {
        var symbols = await rpc.GetDocumentSymbolsAsync(filePath);

        if (symbols.Count == 0)
            return "No symbols found (file may not be in the current solution).";

        var sb = new StringBuilder();
        sb.AppendLine($"Symbols in {filePath}:");
        FormatSymbols(symbols, sb, 0);
        return sb.ToString();
    }

    private static void FormatSymbols(System.Collections.Generic.List<DocumentSymbolInfo> symbols, StringBuilder sb, int indent)
    {
        var prefix = new string(' ', indent * 2);
        foreach (var s in symbols)
        {
            var modifiers = s.Modifiers.Count > 0 ? $" [{string.Join(", ", s.Modifiers)}]" : "";
            var returnType = s.ReturnType != null ? $" : {s.ReturnType}" : "";
            sb.AppendLine($"{prefix}{s.Kind} {s.Name}{returnType}{modifiers} ({s.Accessibility}) [{s.StartLine}-{s.EndLine}]");

            if (s.Children.Count > 0)
                FormatSymbols(s.Children, sb, indent + 1);
        }
    }
}
