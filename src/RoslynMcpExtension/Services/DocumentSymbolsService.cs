using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class DocumentSymbolsService(DocumentFinder documentFinder)
{
	public async Task<List<CodeMemberInfo>> GetDocumentSymbolsAsync(string filePath)
	{
		var symbols = new List<CodeMemberInfo>();

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

	private static void CollectSymbols(SyntaxNode node, SemanticModel semanticModel, List<CodeMemberInfo> symbols)
	{
		foreach (var child in node.ChildNodes())
		{
			CodeMemberInfo? symbolInfo = null;

			switch (child)
			{
				case NamespaceDeclarationSyntax ns:
					symbolInfo = CreateSymbolInfo(ns.Name.ToString(), "namespace", child, semanticModel);
					break;
				case FileScopedNamespaceDeclarationSyntax ns:
					symbolInfo = CreateSymbolInfo(ns.Name.ToString(), "namespace", child, semanticModel);
					break;
				case ClassDeclarationSyntax cls:
					symbolInfo = CreateSymbolInfo(cls.Identifier.Text, "class", child, semanticModel);
					AddModifiers(symbolInfo, cls.Modifiers);
					break;
				case StructDeclarationSyntax str:
					symbolInfo = CreateSymbolInfo(str.Identifier.Text, "struct", child, semanticModel);
					AddModifiers(symbolInfo, str.Modifiers);
					break;
				case InterfaceDeclarationSyntax iface:
					symbolInfo = CreateSymbolInfo(iface.Identifier.Text, "interface", child, semanticModel);
					AddModifiers(symbolInfo, iface.Modifiers);
					break;
				case EnumDeclarationSyntax enm:
					symbolInfo = CreateSymbolInfo(enm.Identifier.Text, "enum", child, semanticModel);
					AddModifiers(symbolInfo, enm.Modifiers);
					break;
				case RecordDeclarationSyntax rec:
					symbolInfo = CreateSymbolInfo(rec.Identifier.Text, "record", child, semanticModel);
					AddModifiers(symbolInfo, rec.Modifiers);
					break;
				case MethodDeclarationSyntax method:
					symbolInfo = CreateSymbolInfo(method.Identifier.Text, "method", child, semanticModel);
					symbolInfo.ReturnType = method.ReturnType.ToString();
					AddModifiers(symbolInfo, method.Modifiers);
					break;
				case PropertyDeclarationSyntax prop:
					symbolInfo = CreateSymbolInfo(prop.Identifier.Text, "property", child, semanticModel);
					symbolInfo.ReturnType = prop.Type.ToString();
					AddModifiers(symbolInfo, prop.Modifiers);
					break;
				case FieldDeclarationSyntax field:
					foreach (var variable in field.Declaration.Variables)
					{
						var fieldInfo = CreateSymbolInfo(variable.Identifier.Text, "field", child, semanticModel);
						fieldInfo.ReturnType = field.Declaration.Type.ToString();
						AddModifiers(fieldInfo, field.Modifiers);
						symbols.Add(fieldInfo);
					}
					continue;
				case EventDeclarationSyntax evt:
					symbolInfo = CreateSymbolInfo(evt.Identifier.Text, "event", child, semanticModel);
					AddModifiers(symbolInfo, evt.Modifiers);
					break;
				case ConstructorDeclarationSyntax ctor:
					symbolInfo = CreateSymbolInfo(ctor.Identifier.Text, "constructor", child, semanticModel);
					AddModifiers(symbolInfo, ctor.Modifiers);
					break;
				case DelegateDeclarationSyntax del:
					symbolInfo = CreateSymbolInfo(del.Identifier.Text, "delegate", child, semanticModel);
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

	private static CodeMemberInfo CreateSymbolInfo(string name, string memberType, SyntaxNode node, SemanticModel model)
	{
		var declaredSymbol = model.GetDeclaredSymbol(node);
		return CodeMemberInfoFactory.Create(
			declaredSymbol,
			name,
			memberType,
			node.GetLocation(),
			model.Compilation.AssemblyName);
	}

	private static void AddModifiers(CodeMemberInfo info, SyntaxTokenList modifiers)
	{
		info.Modifiers = [.. modifiers.Select(m => m.Text)];
	}
}
