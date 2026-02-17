using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class SymbolInfoService(DocumentFinder documentFinder)
{
	public async Task<SymbolDetailInfo> GetSymbolInfoAsync(string filePath, int line, int column)
	{
		var result = new SymbolDetailInfo();

		try
		{
			var document = documentFinder.FindDocument(filePath);
			if (document == null)
			{
				result.ErrorMessage = $"File not found in any project: {filePath}";
				return result;
			}

			var semanticModel = await document.GetSemanticModelAsync();
			var syntaxTree = await document.GetSyntaxTreeAsync();
			if (semanticModel == null || syntaxTree == null)
			{
				result.ErrorMessage = "Failed to get semantic model";
				return result;
			}

			var position = DocumentFinder.GetPosition(syntaxTree, line, column);
			var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, documentFinder.Workspace);
			if (symbol == null)
			{
				result.ErrorMessage = $"No symbol found at line {line}, column {column}";
				return result;
			}

			result.Found = true;
			result.Name = symbol.Name;
			result.FullName = symbol.ToDisplayString();
			result.Kind = symbol.Kind.ToString();
			result.Accessibility = symbol.DeclaredAccessibility.ToString();
			result.IsStatic = symbol.IsStatic;
			result.IsAbstract = symbol.IsAbstract;
			result.IsVirtual = symbol.IsVirtual;
			result.IsOverride = symbol.IsOverride;
			result.IsSealed = symbol.IsSealed;
			result.ContainingType = symbol.ContainingType?.ToDisplayString();
			result.ContainingNamespace = symbol.ContainingNamespace?.ToDisplayString();
			result.Documentation = symbol.GetDocumentationCommentXml();

			switch (symbol)
			{
				case INamedTypeSymbol typeSymbol:
					result.TypeName = typeSymbol.TypeKind.ToString();
					if (typeSymbol.BaseType != null)
						result.BaseTypes.Add(typeSymbol.BaseType.ToDisplayString());
					result.Interfaces = [.. typeSymbol.Interfaces.Select(i => i.ToDisplayString())];
					break;
				case IMethodSymbol methodSymbol:
					result.ReturnType = methodSymbol.ReturnType.ToDisplayString();
					result.Parameters = [.. methodSymbol.Parameters.Select(p => new ParameterInfo
					{
						Name = p.Name,
						Type = p.Type.ToDisplayString(),
						IsOptional = p.IsOptional,
						DefaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null
					})];
					break;
				case IPropertySymbol propSymbol:
					result.ReturnType = propSymbol.Type.ToDisplayString();
					break;
				case IFieldSymbol fieldSymbol:
					result.ReturnType = fieldSymbol.Type.ToDisplayString();
					break;
				case ILocalSymbol localSymbol:
					result.ReturnType = localSymbol.Type.ToDisplayString();
					break;
				case IParameterSymbol paramSymbol:
					result.ReturnType = paramSymbol.Type.ToDisplayString();
					break;
			}
		}
		catch (Exception ex)
		{
			result.ErrorMessage = ex.Message;
		}

		return result;
	}
}
