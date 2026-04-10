# Roslyn MCP Extension — Visual Studio Extension

A Visual Studio extension that exposes **semantic C# code analysis** via the [Model Context Protocol (MCP)](https://modelcontextprotocol.io/), powered by the **live Roslyn workspace** inside Visual Studio.

Unlike standalone Roslyn MCP servers that create their own `MSBuildWorkspace`, this extension uses Visual Studio's actual `VisualStudioWorkspace` — giving you access to unsaved changes, live diagnostics, and the full compilation state that VS already maintains.

## MCP Tools

| Tool | Description |
|------|-------------|
| `roslyn_validate_file` | Get live diagnostics (errors, warnings, analyzer results) for a C# file |
| `roslyn_find_references` | Semantic find-all-references using `SymbolFinder` (not text search) |
| `roslyn_go_to_definition` | Navigate to symbol definition via Roslyn semantic model |
| `roslyn_get_document_symbols` | List all symbols in a file with types, modifiers, and spans |
| `roslyn_search_symbols` | Search for symbol declarations across the solution by name |
| `roslyn_find_dead_code` | Find potentially dead types, methods, and fields across the active workspace, with optional inclusion of public APIs |
| `roslyn_get_symbol_info` | Get detailed type information, base types, interfaces, parameters |

## Prerequisites

- Visual Studio 2022 or 2026
- .NET 10.0 SDK (for the server process)

## Building

The solution file is at `src/RoslynMcpExtension.slnx`. Since the VSIX project requires MSBuild, build via Visual Studio or `msbuild`:

```bash
# Full solution (requires Visual Studio / MSBuild)
msbuild src\RoslynMcpExtension.slnx

# Server and Shared projects only (dotnet CLI)
dotnet build src\RoslynMcpExtension.Server\RoslynMcpExtension.Server.csproj
```

The VSIX project automatically publishes the MCP server process to its output directory.

## Installation

1. Build the solution in Release mode
2. Install the generated `.vsix` from `src/RoslynMcpExtension/bin/Release/`
3. Restart Visual Studio

## Usage

### Starting the Server

The server auto-starts when a solution is loaded (configurable in **Tools > Options > Roslyn MCP Extension**).

You can also manually start/stop via **Tools > Start/Stop Roslyn MCP Server**.

### Configuration

In **Tools > Options > Roslyn MCP Server**:
- **Port**: HTTP port (default: `5050`)
- **Server Name**: Name shown to MCP clients
- **Auto Start**: Start automatically when a solution loads

### Connecting MCP Clients

#### Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "roslyn-mcp": {
      "url": "http://localhost:5050/mcp"
    }
  }
}
```

#### VS Code (GitHub Copilot)

Add to your `.vscode/mcp.json` or user settings:

```json
{
  "servers": {
    "roslyn-mcp": {
      "url": "http://localhost:5050/mcp"
    }
  }
}
```

#### Any MCP Client

Use **Streamable HTTP** with `http://localhost:5050/mcp` for GitHub Copilot and modern MCP clients.

This server is intentionally configured in **stateless HTTP mode** so reconnects remain reliable across server restarts and solution changes. Legacy SSE endpoints are not required for the Roslyn tools and are not used by the recommended GitHub Copilot setup.

### Dead Code Analysis

`roslyn_find_dead_code` reports **potentially** unused methods, fields, and types from the live Visual Studio workspace. It uses Roslyn semantic references plus additional heuristics for framework-driven and runtime-driven code paths that do not always appear as normal source references.

The analysis is intentionally conservative and already filters several common false-positive patterns:

- **Test code**: xUnit, NUnit, and MSTest attributes such as `Fact`, `Theory`, `Test`, `TestCase`, `TestMethod`, `DataTestMethod`, setup/cleanup attributes, and types that contain or inherit test methods
- **Interface contracts**: both explicit and implicit interface implementations
- **XAML usage**: event handlers, code-behind types, and parameterless constructors for controls and windows instantiated from `.xaml`
- **Visual Studio / MEF composition**: `Export`, `Import`, `ImportingConstructor`, and Visual Studio package types decorated with `PackageRegistrationAttribute`
- **Generated and interop code**: common generated files, compiler-generated members, and marshaling / `StructLayout` fields
- **Extension patterns**: static extension containers, classic `this` extension methods, and newer C# `extension(...) { }` blocks

Dead-code detection can never be perfect, especially for reflection-heavy or externally activated code, so results should still be reviewed before deletion.

## Example Prompts

```
Validate the file C:\MyProject\src\UserService.cs for errors and warnings
```

```
Find all references to the method ProcessOrder in C:\MyProject\src\OrderService.cs at line 42, column 20
```

```
Search for all symbols named "Repository" in the current solution
```

```
Find dead code in the active workspace and list unused methods, fields, and types
```

```
Find dead code in the active workspace and include public members as well
```

## How It Differs from Other Roslyn MCP Servers

| Feature | This Extension | Others roslyn/mcp servers |
|---------|---------------|---------------------------------------|
| Workspace | Live VS `VisualStudioWorkspace` | Standalone `MSBuildWorkspace` |
| Unsaved changes | ✅ Sees current editor state | ❌ Only saved files |
| Find References | ✅ Semantic `SymbolFinder` | ❌ Text search or separate workspace |
| Diagnostics | ✅ Live from VS compiler | ⚠️ Re-compiled separately |
| Build integration | ✅ Uses VS compilation state | ❌ Separate compilation |
| Setup | Install VSIX, no config needed | Configure solution path per project |

## License

MIT
