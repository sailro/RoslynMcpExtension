using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class DocumentSymbolsService(DocumentFinder documentFinder)
{
	public async Task<List<DocumentSymbolInfo>> GetDocumentSymbolsAsync(string filePath)
	{
		var symbols = new List<DocumentSymbolInfo>();

		try
		{
			var document = documentFinder.FindDocument(filePath);
			if (document == null) return symbols;

			var root = await document.GetSyntaxRootAsync();
			var semanticModel = await document.GetSemanticModelAsync();
			if (root == null || semanticModel == null) return symbols;

			CollectSymbols(root, semanticModel, symbols);
		}
		catch
		{
			// Return what we have
		}

		return symbols;
	}

	private static void CollectSymbols(SyntaxNode node, SemanticModel semanticModel, List<DocumentSymbolInfo> symbols)
	{
		foreach (var child in node.ChildNodes())
		{
			DocumentSymbolInfo? symbolInfo = null;

			switch (child)
			{
				case NamespaceDeclarationSyntax ns:
					symbolInfo = CreateSymbolInfo(ns.Name.ToString(), "Namespace", child, semanticModel);
					break;
				case FileScopedNamespaceDeclarationSyntax ns:
					symbolInfo = CreateSymbolInfo(ns.Name.ToString(), "Namespace", child, semanticModel);
					break;
				case ClassDeclarationSyntax cls:
					symbolInfo = CreateSymbolInfo(cls.Identifier.Text, "Class", child, semanticModel);
					AddModifiers(symbolInfo, cls.Modifiers);
					break;
				case StructDeclarationSyntax str:
					symbolInfo = CreateSymbolInfo(str.Identifier.Text, "Struct", child, semanticModel);
					AddModifiers(symbolInfo, str.Modifiers);
					break;
				case InterfaceDeclarationSyntax iface:
					symbolInfo = CreateSymbolInfo(iface.Identifier.Text, "Interface", child, semanticModel);
					AddModifiers(symbolInfo, iface.Modifiers);
					break;
				case EnumDeclarationSyntax enm:
					symbolInfo = CreateSymbolInfo(enm.Identifier.Text, "Enum", child, semanticModel);
					AddModifiers(symbolInfo, enm.Modifiers);
					break;
				case RecordDeclarationSyntax rec:
					symbolInfo = CreateSymbolInfo(rec.Identifier.Text, "Record", child, semanticModel);
					AddModifiers(symbolInfo, rec.Modifiers);
					break;
				case MethodDeclarationSyntax method:
					symbolInfo = CreateSymbolInfo(method.Identifier.Text, "Method", child, semanticModel);
					symbolInfo.ReturnType = method.ReturnType.ToString();
					AddModifiers(symbolInfo, method.Modifiers);
					break;
				case PropertyDeclarationSyntax prop:
					symbolInfo = CreateSymbolInfo(prop.Identifier.Text, "Property", child, semanticModel);
					symbolInfo.ReturnType = prop.Type.ToString();
					AddModifiers(symbolInfo, prop.Modifiers);
					break;
				case FieldDeclarationSyntax field:
					foreach (var variable in field.Declaration.Variables)
					{
						var fieldInfo = CreateSymbolInfo(variable.Identifier.Text, "Field", child, semanticModel);
						fieldInfo.ReturnType = field.Declaration.Type.ToString();
						AddModifiers(fieldInfo, field.Modifiers);
						symbols.Add(fieldInfo);
					}
					continue;
				case EventDeclarationSyntax evt:
					symbolInfo = CreateSymbolInfo(evt.Identifier.Text, "Event", child, semanticModel);
					AddModifiers(symbolInfo, evt.Modifiers);
					break;
				case ConstructorDeclarationSyntax ctor:
					symbolInfo = CreateSymbolInfo(ctor.Identifier.Text, "Constructor", child, semanticModel);
					AddModifiers(symbolInfo, ctor.Modifiers);
					break;
				case DelegateDeclarationSyntax del:
					symbolInfo = CreateSymbolInfo(del.Identifier.Text, "Delegate", child, semanticModel);
					symbolInfo.ReturnType = del.ReturnType.ToString();
					AddModifiers(symbolInfo, del.Modifiers);
					break;
			}

			if (symbolInfo != null)
			{
				CollectSymbols(child, semanticModel, symbolInfo.Children);
				symbols.Add(symbolInfo);
			}
		}
	}

	private static DocumentSymbolInfo CreateSymbolInfo(string name, string kind, SyntaxNode node, SemanticModel model)
	{
		var lineSpan = node.GetLocation().GetLineSpan();
		var declaredSymbol = model.GetDeclaredSymbol(node);

		return new DocumentSymbolInfo
		{
			Name = name,
			FullName = declaredSymbol?.ToDisplayString() ?? name,
			Kind = kind,
			Accessibility = declaredSymbol?.DeclaredAccessibility.ToString() ?? "Unknown",
			StartLine = lineSpan.StartLinePosition.Line + 1,
			StartColumn = lineSpan.StartLinePosition.Character + 1,
			EndLine = lineSpan.EndLinePosition.Line + 1,
			EndColumn = lineSpan.EndLinePosition.Character + 1
		};
	}

	private static void AddModifiers(DocumentSymbolInfo info, SyntaxTokenList modifiers)
	{
		info.Modifiers = [.. modifiers.Select(m => m.Text)];
	}
}
