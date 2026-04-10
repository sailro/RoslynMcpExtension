using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class DeadCodeAnalysisService(DocumentFinder documentFinder)
{
	private static readonly string[] TestMethodAttributeNames =
	[
		// xUnit
		"FactAttribute",
		"TheoryAttribute",
		"InlineDataAttribute",
		"MemberDataAttribute",
		"ClassDataAttribute",

		// NUnit
		"TestAttribute",
		"TestCaseAttribute",
		"TestCaseSourceAttribute",
		"ValuesAttribute",
		"ValueSourceAttribute",
		"RangeAttribute",
		"RandomAttribute",
		"CombinatorialAttribute",
		"PairwiseAttribute",
		"SequentialAttribute",
		"DatapointAttribute",
		"DatapointSourceAttribute",
		"SetUpAttribute",
		"TearDownAttribute",
		"OneTimeSetUpAttribute",
		"OneTimeTearDownAttribute",

		// MSTest
		"TestMethodAttribute",
		"DataTestMethodAttribute",
		"DataRowAttribute",
		"DynamicDataAttribute",
		"TestInitializeAttribute",
		"TestCleanupAttribute",
		"ClassInitializeAttribute",
		"ClassCleanupAttribute",
		"AssemblyInitializeAttribute",
		"AssemblyCleanupAttribute"
	];

	private static readonly string[] TestContainerAttributeNames =
	[
		"TestClassAttribute",
		"TestFixtureAttribute",
		"TestFixtureSourceAttribute",
		"CollectionAttribute",
		"CollectionDefinitionAttribute"
	];

	public async Task<DeadCodeAnalysisResult> FindDeadCodeAsync(int maxResults, bool includeInternal, bool includePublic)
	{
		var result = new DeadCodeAnalysisResult();

		try
		{
			var solution = documentFinder.Workspace.CurrentSolution;
			var xamlReferences = XamlReferenceIndex.Create(solution);
			var candidates = await CollectCandidatesAsync(solution, includeInternal, includePublic);
			var deadCount = 0;

			foreach (var candidate in candidates
				         .OrderBy(c => c.ProjectName, StringComparer.OrdinalIgnoreCase)
				         .ThenBy(c => c.Location.SourceTree?.FilePath, StringComparer.OrdinalIgnoreCase)
				         .ThenBy(c => c.Location.SourceSpan.Start))
			{
				if (await HasSourceReferencesAsync(candidate.Symbol, solution, xamlReferences))
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

		return !IsGeneratedFilePath(document.FilePath);
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

		if (IsDeclaredInExtensionBlock(method))
			return false;

		if (ImplementsInterfaceContract(method))
			return false;

		if (IsWithinTestContainer(method))
			return false;

		if (HasTestMethodAttributes(method))
			return false;

		if (HasGeneratedCodeAttributes(method) || HasGeneratedCodeAttributes(method.ContainingType))
			return false;

		if (HasFrameworkCompositionAttributes(method) || HasFrameworkCompositionAttributes(method.ContainingType))
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
		       && field.ContainingType?.TypeKind != TypeKind.Enum
		       && !IsWithinTestContainer(field)
		       && !HasGeneratedCodeAttributes(field)
		       && !HasGeneratedCodeAttributes(field.ContainingType)
		       && !HasFrameworkCompositionAttributes(field)
		       && !HasFrameworkCompositionAttributes(field.ContainingType)
		       && !IsInteropField(field);
	}

	private static bool IsCandidate(INamedTypeSymbol type)
	{
		return type.TypeKind != TypeKind.Error
		       && !type.IsAnonymousType
		       && !type.IsImplicitClass
		       && !IsWithinTestContainer(type)
		       && !HasGeneratedCodeAttributes(type)
		       && !HasFrameworkCompositionAttributes(type)
		       && !IsExtensionContainer(type);
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

	private static async Task<bool> HasSourceReferencesAsync(ISymbol symbol, Solution solution, XamlReferenceIndex xamlReferences)
	{
		var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken: CancellationToken.None);
		if (references.Any(reference => reference.Locations.Any(location => location.Location.IsInSource)))
			return true;

		if (symbol is IMethodSymbol method)
		{
			if (await HasPairedXamlReferenceAsync(method))
				return true;

			if (method.MethodKind == MethodKind.Constructor
			    && method.Parameters.Length == 0
			    && method.ContainingType != null
			    && xamlReferences.ReferencesType(method.ContainingType))
			{
				return true;
			}
		}

		if (symbol is INamedTypeSymbol type && xamlReferences.ReferencesType(type))
			return true;

		return symbol is IFieldSymbol field && await HasSameDocumentReferenceAsync(field, solution);
	}

	private static string GetMemberType(ISymbol symbol)
	{
		return CodeMemberInfoFactory.GetMemberType(symbol);
	}

	private static bool IsGeneratedFilePath(string filePath)
	{
		var normalizedPath = filePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
		var fileName = Path.GetFileName(normalizedPath);
		var separator = Path.DirectorySeparatorChar.ToString();

		return normalizedPath.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase)
		       || normalizedPath.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase)
		       || fileName.StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase)
		       || fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
		       || fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
		       || fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
		       || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
		       || fileName.Equals(".NETFramework,Version=v4.8.AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
	}

	private static bool HasGeneratedCodeAttributes(ISymbol? symbol)
	{
		return HasAnyAttribute(symbol,
			"CompilerGeneratedAttribute",
			"GeneratedCodeAttribute",
			"DebuggerNonUserCodeAttribute");
	}

	private static bool HasFrameworkCompositionAttributes(ISymbol? symbol)
	{
		return HasAnyAttribute(symbol,
			"ExportAttribute",
			"InheritedExportAttribute",
			"ImportAttribute",
			"ImportManyAttribute",
			"ImportingConstructorAttribute",
			"PackageRegistrationAttribute",
			"Microsoft.VisualStudio.Shell.PackageRegistrationAttribute");
	}

	private static bool HasTestMethodAttributes(IMethodSymbol method)
	{
		return HasAnyAttribute(method, TestMethodAttributeNames);
	}

	private static bool IsWithinTestContainer(ISymbol symbol)
	{
		for (var type = symbol as INamedTypeSymbol ?? symbol.ContainingType; type != null; type = type.ContainingType)
		{
			if (IsTestContainer(type))
				return true;
		}

		return false;
	}

	private static bool ImplementsInterfaceContract(IMethodSymbol method)
	{
		if (method.ExplicitInterfaceImplementations.Length > 0)
			return true;

		var containingType = method.ContainingType;
		if (containingType == null || containingType.AllInterfaces.IsDefaultOrEmpty)
			return false;

		foreach (var interfaceType in containingType.AllInterfaces)
		{
			foreach (var interfaceMember in interfaceType.GetMembers())
			{
				var implementation = containingType.FindImplementationForInterfaceMember(interfaceMember);
				if (MatchesMethodImplementation(implementation, method))
					return true;
			}
		}

		return false;
	}

	private static bool MatchesMethodImplementation(ISymbol? implementation, IMethodSymbol method)
	{
		return implementation switch
		{
			IMethodSymbol implementationMethod => SymbolEqualityComparer.Default.Equals(implementationMethod, method)
			                                     || SymbolEqualityComparer.Default.Equals(implementationMethod.OriginalDefinition, method.OriginalDefinition),
			IPropertySymbol property => MatchesMethodImplementation(property.GetMethod, method)
			                           || MatchesMethodImplementation(property.SetMethod, method),
			IEventSymbol @event => MatchesMethodImplementation(@event.AddMethod, method)
			                       || MatchesMethodImplementation(@event.RemoveMethod, method)
			                       || MatchesMethodImplementation(@event.RaiseMethod, method),
			_ => false
		};
	}

	private static bool IsTestContainer(INamedTypeSymbol type)
	{
		for (var current = type; current != null; current = current.BaseType)
		{
			if (HasAnyAttribute(current, TestContainerAttributeNames)
			    || current.GetMembers().OfType<IMethodSymbol>().Any(HasTestMethodAttributes))
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasAnyAttribute(ISymbol? symbol, params string[] attributeNames)
	{
		if (symbol == null)
			return false;

		foreach (var attribute in symbol.GetAttributes())
		{
			for (var attributeType = attribute.AttributeClass; attributeType != null; attributeType = attributeType.BaseType)
			{
				var name = attributeType.Name;
				var fullName = attributeType.ToDisplayString();
				if (attributeNames.Contains(name, StringComparer.Ordinal)
				    || attributeNames.Contains(fullName, StringComparer.Ordinal))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static bool IsInteropField(IFieldSymbol field)
	{
		if (HasAnyAttribute(field, "FieldOffsetAttribute", "MarshalAsAttribute"))
			return true;

		var containingType = field.ContainingType;
		return containingType?.TypeKind == TypeKind.Struct
		       && HasAnyAttribute(containingType, "StructLayoutAttribute", "InlineArrayAttribute");
	}

	private static bool IsExtensionContainer(INamedTypeSymbol type)
	{
		if (IsDeclaredInExtensionBlock(type))
			return true;

		if (!type.IsStatic)
			return false;

		return type.GetMembers().OfType<IMethodSymbol>().Any(member => member.IsExtensionMethod || IsDeclaredInExtensionBlock(member))
		       || type.GetTypeMembers().Any(IsDeclaredInExtensionBlock);
	}

	private static bool IsDeclaredInExtensionBlock(ISymbol symbol)
	{
		foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
		{
			for (var current = syntaxReference.GetSyntax(CancellationToken.None); current != null; current = current.Parent)
			{
				if (string.Equals(current.GetType().Name, "ExtensionDeclarationSyntax", StringComparison.Ordinal))
					return true;
			}
		}

		return false;
	}

	private static Task<bool> HasPairedXamlReferenceAsync(IMethodSymbol method)
	{
		if (method.MethodKind != MethodKind.Ordinary)
			return Task.FromResult(false);

		foreach (var filePath in method.Locations
			         .Where(location => location.IsInSource)
			         .Select(location => location.SourceTree?.FilePath)
			         .Where(path => !string.IsNullOrWhiteSpace(path))
			         .Distinct(StringComparer.OrdinalIgnoreCase))
		{
			if (!filePath!.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase))
				continue;

			var xamlPath = filePath.Substring(0, filePath.Length - 3);
			if (!File.Exists(xamlPath))
				continue;

			var xamlText = File.ReadAllText(xamlPath);
			if (xamlText.Contains($"=\"{method.Name}\"", StringComparison.Ordinal)
			    || xamlText.Contains($"='{method.Name}'", StringComparison.Ordinal))
			{
				return Task.FromResult(true);
			}
		}

		return Task.FromResult(false);
	}

	private static async Task<bool> HasSameDocumentReferenceAsync(IFieldSymbol field, Solution solution)
	{
		foreach (var location in field.Locations.Where(loc => loc.IsInSource && loc.SourceTree != null))
		{
			var document = solution.GetDocument(location.SourceTree!);
			if (document == null)
				continue;

			var root = await document.GetSyntaxRootAsync(CancellationToken.None);
			var semanticModel = await document.GetSemanticModelAsync(CancellationToken.None);
			if (root == null || semanticModel == null)
				continue;

			foreach (var token in root.DescendantTokens())
			{
				if (!token.IsKind(SyntaxKind.IdentifierToken)
				    || token.ValueText != field.Name
				    || location.SourceSpan.Contains(token.Span))
					continue;

				if (ReferencesFieldSymbol(token, semanticModel, field) || IsLikelyFieldReferenceToken(token))
					return true;
			}
		}

		return false;
	}

	private static bool ReferencesFieldSymbol(SyntaxToken token, SemanticModel semanticModel, IFieldSymbol field)
	{
		for (SyntaxNode? node = token.Parent; node != null; node = node.Parent)
		{
			var symbolInfo = semanticModel.GetSymbolInfo(node, CancellationToken.None);
			if (SymbolMatchesField(symbolInfo.Symbol, field)
			    || symbolInfo.CandidateSymbols.Any(candidate => SymbolMatchesField(candidate, field)))
			{
				return true;
			}

			if (node is ExpressionSyntax)
				break;
		}

		return false;
	}

	private static bool SymbolMatchesField(ISymbol? candidate, IFieldSymbol field)
	{
		return candidate is IFieldSymbol referencedField
		       && SymbolEqualityComparer.Default.Equals(referencedField, field);
	}

	private static bool IsLikelyFieldReferenceToken(SyntaxToken token)
	{
		return token.Parent switch
		{
			null => false,
			VariableDeclaratorSyntax declarator when declarator.Identifier == token => false,
			BaseTypeDeclarationSyntax declaration when declaration.Identifier == token => false,
			DelegateDeclarationSyntax declaration when declaration.Identifier == token => false,
			MethodDeclarationSyntax declaration when declaration.Identifier == token => false,
			ConstructorDeclarationSyntax declaration when declaration.Identifier == token => false,
			PropertyDeclarationSyntax declaration when declaration.Identifier == token => false,
			EventDeclarationSyntax declaration when declaration.Identifier == token => false,
			ParameterSyntax declaration when declaration.Identifier == token => false,
			TypeParameterSyntax declaration when declaration.Identifier == token => false,
			SingleVariableDesignationSyntax designation when designation.Identifier == token => false,
			ForEachStatementSyntax statement when statement.Identifier == token => false,
			CatchDeclarationSyntax declaration when declaration.Identifier == token => false,
			LabeledStatementSyntax statement when statement.Identifier == token => false,
			_ => true
		};
	}

	private sealed class CandidateSymbol(ISymbol symbol, string projectName, Location location)
	{
		public ISymbol Symbol { get; } = symbol;
		public string ProjectName { get; } = projectName;
		public Location Location { get; } = location;
	}

	private sealed class XamlReferenceIndex(List<XamlFile> xamlFiles)
	{
		private readonly Dictionary<string, bool> _typeUsageCache = new(StringComparer.Ordinal);

		public bool ReferencesType(INamedTypeSymbol type)
		{
			var key = type.ToDisplayString();
			if (_typeUsageCache.TryGetValue(key, out var isUsed))
				return isUsed;

			isUsed = ReferencesTypeCore(type);
			_typeUsageCache[key] = isUsed;
			return isUsed;
		}

		private bool ReferencesTypeCore(INamedTypeSymbol type)
		{
			if (string.IsNullOrWhiteSpace(type.Name))
				return false;

			var fullName = type.ToDisplayString();
			var namespaceName = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
			var pairedXamlPaths = GetPairedXamlPaths(type);

			foreach (var xamlFile in xamlFiles)
			{
				if (pairedXamlPaths.Contains(xamlFile.Path))
					return true;

				if (ContainsXamlAttributeValue(xamlFile.Content, "x:Class", fullName))
					return true;

				if (!string.IsNullOrEmpty(namespaceName)
				    && xamlFile.Content.IndexOf($"clr-namespace:{namespaceName}", StringComparison.OrdinalIgnoreCase) >= 0
				    && ContainsXamlTypeName(xamlFile.Content, type.Name))
				{
					return true;
				}
			}

			return false;
		}

		private static HashSet<string> GetPairedXamlPaths(INamedTypeSymbol type)
		{
			var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var filePath in type.Locations
				         .Where(location => location.IsInSource)
				         .Select(location => location.SourceTree?.FilePath)
				         .Where(path => !string.IsNullOrWhiteSpace(path)))
			{
				if (filePath!.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase))
				{
					paths.Add(filePath.Substring(0, filePath.Length - 3));
					continue;
				}

				paths.Add(Path.ChangeExtension(filePath, ".xaml"));
			}

			return paths;
		}

		private static bool ContainsXamlAttributeValue(string xamlText, string attributeName, string value)
		{
			return xamlText.IndexOf($"{attributeName}=\"{value}\"", StringComparison.Ordinal) >= 0
			       || xamlText.IndexOf($"{attributeName}='{value}'", StringComparison.Ordinal) >= 0;
		}

		private static bool ContainsXamlTypeName(string xamlText, string typeName)
		{
			return xamlText.IndexOf($":{typeName}", StringComparison.Ordinal) >= 0
			       || xamlText.IndexOf($" {typeName}\"", StringComparison.Ordinal) >= 0
			       || xamlText.IndexOf($" {typeName}'", StringComparison.Ordinal) >= 0
			       || xamlText.IndexOf($"{{x:Type {typeName}}}", StringComparison.Ordinal) >= 0;
		}

		public static XamlReferenceIndex Create(Solution solution)
		{
			var files = new List<XamlFile>();
			var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var root in GetProjectDirectories(solution))
			{
				if (!Directory.Exists(root))
					continue;

				IEnumerable<string> xamlPaths;
				try
				{
					xamlPaths = Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories);
				}
				catch (IOException)
				{
					continue;
				}
				catch (UnauthorizedAccessException)
				{
					continue;
				}

				foreach (var path in xamlPaths)
				{
					if (!seenPaths.Add(path) || IsGeneratedFilePath(path))
						continue;

					try
					{
						files.Add(new XamlFile(path, File.ReadAllText(path)));
					}
					catch (IOException)
					{
					}
					catch (UnauthorizedAccessException)
					{
					}
				}
			}

			return new XamlReferenceIndex(files);
		}

		private static IEnumerable<string> GetProjectDirectories(Solution solution)
		{
			var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var project in solution.Projects)
			{
				var projectFilePath = project.FilePath;
				if (!string.IsNullOrWhiteSpace(projectFilePath))
				{
					var projectDirectory = Path.GetDirectoryName(projectFilePath);
					if (!string.IsNullOrWhiteSpace(projectDirectory))
						directories.Add(projectDirectory);
					continue;
				}

				var documentDirectory = project.Documents
					.Select(document => document.FilePath)
					.Where(path => !string.IsNullOrWhiteSpace(path))
					.Select(Path.GetDirectoryName)
					.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

				if (!string.IsNullOrWhiteSpace(documentDirectory))
					directories.Add(documentDirectory!);
			}

			return directories;
		}
	}

	private sealed class XamlFile(string path, string content)
	{
		public string Path { get; } = path;
		public string Content { get; } = content;
	}
}
