using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class DeadCodeAnalysisService(DocumentFinder documentFinder)
{
	public async Task<DeadCodeAnalysisResult> FindDeadCodeAsync(int maxResults, bool includeInternal, bool includePublic)
	{
		var result = new DeadCodeAnalysisResult();

		try
		{
			var solution = documentFinder.Workspace.CurrentSolution;
			var candidates = await CollectCandidatesAsync(solution, includeInternal, includePublic);
			var deadCount = 0;

			foreach (var candidate in candidates
				         .OrderBy(c => c.ProjectName, StringComparer.OrdinalIgnoreCase)
				         .ThenBy(c => c.Location.SourceTree?.FilePath, StringComparer.OrdinalIgnoreCase)
				         .ThenBy(c => c.Location.SourceSpan.Start))
			{
				if (await HasSourceReferencesAsync(candidate.Symbol, solution))
					continue;

				deadCount++;

				if (result.Members.Count >= maxResults)
					continue;

				result.Members.Add(CodeMemberInfoFactory.Create(
					candidate.Symbol,
					candidate.Symbol.Name,
					GetMemberType(candidate.Symbol),
					candidate.Location,
					candidate.ProjectName));
			}

			result.TotalCount = deadCount;
			result.Truncated = deadCount > result.Members.Count;
		}
		catch (Exception ex)
		{
			result.ErrorMessage = ex.Message;
		}

		return result;
	}

	private static async Task<List<CandidateSymbol>> CollectCandidatesAsync(Solution solution, bool includeInternal, bool includePublic)
	{
		var candidates = new List<CandidateSymbol>();
		var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

		foreach (var project in solution.Projects)
		{
			foreach (var document in project.Documents)
			{
				if (!IsAnalyzableDocument(document))
					continue;

				var root = await document.GetSyntaxRootAsync(CancellationToken.None);
				var semanticModel = await document.GetSemanticModelAsync(CancellationToken.None);
				if (root == null || semanticModel == null)
					continue;

				foreach (var symbol in CollectDeclaredSymbols(root, semanticModel, includeInternal, includePublic))
				{
					if (!seen.Add(symbol))
						continue;

					var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
					if (location == null)
						continue;

					candidates.Add(new CandidateSymbol(symbol, project.Name, location));
				}
			}
		}

		return candidates;
	}

	private static IEnumerable<ISymbol> CollectDeclaredSymbols(SyntaxNode root, SemanticModel semanticModel, bool includeInternal, bool includePublic)
	{
		foreach (var node in root.DescendantNodes())
		{
			switch (node)
			{
				case BaseTypeDeclarationSyntax typeDeclaration:
				{
					var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
					if (IsCandidate(typeSymbol, includeInternal, includePublic))
						yield return typeSymbol!;
					break;
				}
				case DelegateDeclarationSyntax delegateDeclaration:
				{
					var delegateSymbol = semanticModel.GetDeclaredSymbol(delegateDeclaration);
					if (IsCandidate(delegateSymbol, includeInternal, includePublic))
						yield return delegateSymbol!;
					break;
				}
				case MethodDeclarationSyntax methodDeclaration:
				{
					var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
					if (IsCandidate(methodSymbol, includeInternal, includePublic))
						yield return methodSymbol!;
					break;
				}
				case ConstructorDeclarationSyntax constructorDeclaration:
				{
					var constructorSymbol = semanticModel.GetDeclaredSymbol(constructorDeclaration);
					if (IsCandidate(constructorSymbol, includeInternal, includePublic))
						yield return constructorSymbol!;
					break;
				}
				case FieldDeclarationSyntax fieldDeclaration:
				{
					foreach (var variable in fieldDeclaration.Declaration.Variables)
					{
						var fieldSymbol = semanticModel.GetDeclaredSymbol(variable);
						if (IsCandidate(fieldSymbol, includeInternal, includePublic))
							yield return fieldSymbol!;
					}
					break;
				}
			}
		}
	}

	private static bool IsAnalyzableDocument(Document document)
	{
		if (document.SourceCodeKind != SourceCodeKind.Regular || document.FilePath == null)
			return false;

		var filePath = document.FilePath;
		var fileName = Path.GetFileName(filePath);

		return !fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
		       && !fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
		       && !fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
		       && !fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
		       && !fileName.Equals(".NETFramework,Version=v4.8.AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsCandidate(ISymbol? symbol, bool includeInternal, bool includePublic)
	{
		if (symbol == null || symbol.IsImplicitlyDeclared || symbol.Locations.All(l => !l.IsInSource))
			return false;

		if (!IsAccessibilityIncluded(symbol.DeclaredAccessibility, includeInternal, includePublic))
			return false;

		return symbol switch
		{
			IMethodSymbol method => IsCandidate(method),
			IFieldSymbol field => IsCandidate(field),
			INamedTypeSymbol type => IsCandidate(type),
			_ => false
		};
	}

	private static bool IsCandidate(IMethodSymbol method)
	{
		if (method.MethodKind is not (MethodKind.Ordinary or MethodKind.Constructor))
			return false;

		return method.AssociatedSymbol == null
		       && !method.IsAbstract
		       && !method.IsOverride
		       && !method.IsVirtual
		       && !method.IsExtern
		       && method.ExplicitInterfaceImplementations.Length == 0;
	}

	private static bool IsCandidate(IFieldSymbol field)
	{
		return field.AssociatedSymbol == null
		       && field.ContainingType?.TypeKind != TypeKind.Enum;
	}

	private static bool IsCandidate(INamedTypeSymbol type)
	{
		return type.TypeKind != TypeKind.Error
		       && !type.IsAnonymousType
		       && !type.IsImplicitClass;
	}

	private static bool IsAccessibilityIncluded(Accessibility accessibility, bool includeInternal, bool includePublic)
	{
		return accessibility switch
		{
			Accessibility.Private => true,
			Accessibility.Internal or Accessibility.ProtectedAndInternal => includeInternal,
			Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal => includePublic,
			_ => false
		};
	}

	private static async Task<bool> HasSourceReferencesAsync(ISymbol symbol, Solution solution)
	{
		var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken: CancellationToken.None);
		return references.Any(reference => reference.Locations.Any(location => location.Location.IsInSource));
	}

	private static string GetMemberType(ISymbol symbol)
	{
		return CodeMemberInfoFactory.GetMemberType(symbol);
	}

	private sealed class CandidateSymbol(ISymbol symbol, string projectName, Location location)
	{
		public ISymbol Symbol { get; } = symbol;
		public string ProjectName { get; } = projectName;
		public Location Location { get; } = location;
	}
}
