using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class DocumentSymbolsService(DocumentFinder documentFinder)
{
	public async Task<SymbolListResult> GetDocumentSymbolsAsync(string filePath)
	{
		var result = new SymbolListResult();

		try
		{
			var document = documentFinder.FindDocument(filePath);
			if (document == null) return result;

			var root = await document.GetSyntaxRootAsync();
			var semanticModel = await document.GetSemanticModelAsync();
			if (root == null || semanticModel == null) return result;

			CollectSymbols(root, semanticModel, result.Members);
		}
		catch
		{
		}

		return result;
	}

	private static void CollectSymbols(SyntaxNode node, SemanticModel semanticModel, List<SymbolLocation> symbols)
	{
		foreach (var child in node.ChildNodes())
		{
			var childSymbols = CreateSymbolInfos(child, semanticModel);
			foreach (var symbolInfo in childSymbols)
				symbols.Add(symbolInfo);

			CollectSymbols(child, semanticModel, symbols);
		}
	}

	private static IEnumerable<SymbolLocation> CreateSymbolInfos(SyntaxNode node, SemanticModel semanticModel)
	{
		switch (node)
		{
			case BaseNamespaceDeclarationSyntax namespaceDeclaration:
				yield return CreateSymbolInfo(namespaceDeclaration.Name.ToString(), namespaceDeclaration, semanticModel);
				yield break;
			case BaseTypeDeclarationSyntax typeDeclaration:
			{
				var info = CreateSymbolInfo(GetDeclaredName(typeDeclaration), typeDeclaration, semanticModel);
				AddModifiers(info, typeDeclaration.Modifiers);
				yield return info;
				yield break;
			}
			case MethodDeclarationSyntax method:
			{
				var info = CreateSymbolInfo(method.Identifier.Text, method, semanticModel);
				info.ReturnType = method.ReturnType.ToString();
				AddModifiers(info, method.Modifiers);
				yield return info;
				yield break;
			}
			case PropertyDeclarationSyntax property:
			{
				var info = CreateSymbolInfo(property.Identifier.Text, property, semanticModel);
				info.ReturnType = property.Type.ToString();
				AddModifiers(info, property.Modifiers);
				yield return info;
				yield break;
			}
			case FieldDeclarationSyntax field:
				foreach (var variable in field.Declaration.Variables)
				{
					var info = CreateSymbolInfo(variable.Identifier.Text, variable, semanticModel);
					info.ReturnType = field.Declaration.Type.ToString();
					AddModifiers(info, field.Modifiers);
					yield return info;
				}
				yield break;
			case EventFieldDeclarationSyntax eventField:
				foreach (var variable in eventField.Declaration.Variables)
				{
					var info = CreateSymbolInfo(variable.Identifier.Text, variable, semanticModel);
					info.ReturnType = eventField.Declaration.Type.ToString();
					AddModifiers(info, eventField.Modifiers);
					yield return info;
				}
				yield break;
			case EventDeclarationSyntax eventDeclaration:
			{
				var info = CreateSymbolInfo(eventDeclaration.Identifier.Text, eventDeclaration, semanticModel);
				AddModifiers(info, eventDeclaration.Modifiers);
				yield return info;
				yield break;
			}
			case ConstructorDeclarationSyntax constructor:
			{
				var info = CreateSymbolInfo(constructor.Identifier.Text, constructor, semanticModel);
				AddModifiers(info, constructor.Modifiers);
				yield return info;
				yield break;
			}
			case DelegateDeclarationSyntax @delegate:
			{
				var info = CreateSymbolInfo(@delegate.Identifier.Text, @delegate, semanticModel);
				info.ReturnType = @delegate.ReturnType.ToString();
				AddModifiers(info, @delegate.Modifiers);
				yield return info;
				yield break;
			}
		}
	}

	private static SymbolLocation CreateSymbolInfo(string name, SyntaxNode node, SemanticModel model)
	{
		var declaredSymbol = model.GetDeclaredSymbol(node);
		return CodeMemberInfoFactory.Create(
			declaredSymbol,
			name,
			"member",
			node.GetLocation(),
			model.Compilation.AssemblyName);
	}

	private static string GetDeclaredName(BaseTypeDeclarationSyntax typeDeclaration)
	{
		return typeDeclaration switch
		{
			ClassDeclarationSyntax cls => cls.Identifier.Text,
			StructDeclarationSyntax str => str.Identifier.Text,
			InterfaceDeclarationSyntax iface => iface.Identifier.Text,
			EnumDeclarationSyntax enm => enm.Identifier.Text,
			RecordDeclarationSyntax rec => rec.Identifier.Text,
			_ => typeDeclaration.GetType().Name
		};
	}

	private static void AddModifiers(SymbolLocation info, SyntaxTokenList modifiers)
	{
		info.Modifiers = [.. modifiers.Select(m => m.Text)];
	}
}
