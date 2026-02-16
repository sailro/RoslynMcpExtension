using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using RoslynMcpExtension.Shared;
using RoslynMcpExtension.Shared.Models;

namespace RoslynMcpExtension.Services;

[Export(typeof(RoslynAnalysisService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class RoslynAnalysisService : IRoslynAnalysisRpc
{
    private readonly VisualStudioWorkspace _workspace;

    [ImportingConstructor]
    public RoslynAnalysisService(VisualStudioWorkspace workspace)
    {
        _workspace = workspace;
    }

    private Document? FindDocument(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return _workspace.CurrentSolution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(d.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    // ─── Tool 1: ValidateFile ────────────────────────────────────────────

    public async Task<ValidateFileResult> ValidateFileAsync(string filePath, bool includeWarnings, bool runAnalyzers)
    {
        var result = new ValidateFileResult { FilePath = filePath };

        try
        {
            var document = FindDocument(filePath);
            if (document == null)
            {
                result.ErrorMessage = $"File not found in any project: {filePath}";
                return result;
            }

            result.ProjectName = document.Project.Name;
            var compilation = await document.Project.GetCompilationAsync();
            if (compilation == null)
            {
                result.ErrorMessage = "Failed to get compilation";
                return result;
            }

            var syntaxTree = await document.GetSyntaxTreeAsync();
            if (syntaxTree == null)
            {
                result.ErrorMessage = "Failed to get syntax tree";
                return result;
            }

            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Location.SourceTree?.FilePath != null &&
                            string.Equals(d.Location.SourceTree.FilePath, Path.GetFullPath(filePath),
                                StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var diag in diagnostics)
            {
                var info = ToDiagnosticInfo(diag);
                switch (diag.Severity)
                {
                    case DiagnosticSeverity.Error:
                        result.Errors.Add(info);
                        break;
                    case DiagnosticSeverity.Warning when includeWarnings:
                        result.Warnings.Add(info);
                        break;
                }
            }

            if (runAnalyzers)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                if (semanticModel != null)
                {
                    var analyzerDiags = semanticModel.GetDiagnostics();
                    foreach (var diag in analyzerDiags.Where(d =>
                                 d.Severity >= DiagnosticSeverity.Warning || d.Severity == DiagnosticSeverity.Info))
                    {
                        if (!diagnostics.Any(existing => existing.Id == diag.Id &&
                                                          existing.Location.GetLineSpan().StartLinePosition ==
                                                          diag.Location.GetLineSpan().StartLinePosition))
                        {
                            result.AnalyzerDiagnostics.Add(ToDiagnosticInfo(diag));
                        }
                    }
                }
            }

            result.Success = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static DiagnosticInfo ToDiagnosticInfo(Diagnostic diag)
    {
        var lineSpan = diag.Location.GetLineSpan();
        return new DiagnosticInfo
        {
            Id = diag.Id,
            Message = diag.GetMessage(),
            Severity = diag.Severity.ToString(),
            FilePath = diag.Location.SourceTree?.FilePath ?? string.Empty,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            StartColumn = lineSpan.StartLinePosition.Character + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            EndColumn = lineSpan.EndLinePosition.Character + 1,
            Category = diag.Descriptor.Category
        };
    }

    // ─── Tool 2: FindReferences ──────────────────────────────────────────

    public async Task<FindReferencesResult> FindReferencesAsync(string filePath, int line, int column, int maxResults)
    {
        var result = new FindReferencesResult();

        try
        {
            var document = FindDocument(filePath);
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

            var position = GetPosition(syntaxTree, line, column);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);
            if (symbol == null)
            {
                result.ErrorMessage = $"No symbol found at line {line}, column {column}";
                return result;
            }

            result.SymbolName = symbol.Name;
            result.SymbolKind = symbol.Kind.ToString();
            result.Found = true;

            var references = await SymbolFinder.FindReferencesAsync(symbol, _workspace.CurrentSolution);
            var count = 0;

            foreach (var refSymbol in references)
            {
                // Add definition location
                foreach (var loc in refSymbol.Definition.Locations.Where(l => l.IsInSource))
                {
                    if (count >= maxResults) break;
                    var lineSpan = loc.GetLineSpan();
                    var sourceText = await loc.SourceTree!.GetTextAsync();
                    var refLine = sourceText.Lines[lineSpan.StartLinePosition.Line];

                    result.References.Add(new ReferenceLocationInfo
                    {
                        FilePath = loc.SourceTree.FilePath,
                        StartLine = lineSpan.StartLinePosition.Line + 1,
                        StartColumn = lineSpan.StartLinePosition.Character + 1,
                        EndLine = lineSpan.EndLinePosition.Line + 1,
                        EndColumn = lineSpan.EndLinePosition.Character + 1,
                        Preview = refLine.ToString().Trim(),
                        IsDefinition = true
                    });
                    count++;
                }

                // Add usage locations
                foreach (var loc in refSymbol.Locations)
                {
                    if (count >= maxResults) break;
                    var lineSpan = loc.Location.GetLineSpan();
                    var sourceText = await loc.Location.SourceTree!.GetTextAsync();
                    var refLine = sourceText.Lines[lineSpan.StartLinePosition.Line];

                    var containingMember = GetContainingMemberName(
                        await loc.Location.SourceTree.GetRootAsync(), loc.Location.SourceSpan);

                    result.References.Add(new ReferenceLocationInfo
                    {
                        FilePath = loc.Location.SourceTree.FilePath,
                        StartLine = lineSpan.StartLinePosition.Line + 1,
                        StartColumn = lineSpan.StartLinePosition.Character + 1,
                        EndLine = lineSpan.EndLinePosition.Line + 1,
                        EndColumn = lineSpan.EndLinePosition.Character + 1,
                        Preview = refLine.ToString().Trim(),
                        ContainingMember = containingMember,
                        IsDefinition = false
                    });
                    count++;
                }
            }

            result.TotalCount = count;
            result.Truncated = count >= maxResults;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    // ─── Tool 3: GoToDefinition ──────────────────────────────────────────

    public async Task<GoToDefinitionResult> GoToDefinitionAsync(string filePath, int line, int column)
    {
        var result = new GoToDefinitionResult();

        try
        {
            var document = FindDocument(filePath);
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

            var position = GetPosition(syntaxTree, line, column);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);
            if (symbol == null)
            {
                result.ErrorMessage = $"No symbol found at line {line}, column {column}";
                return result;
            }

            result.Found = true;
            result.SymbolName = symbol.Name;
            result.SymbolKind = symbol.Kind.ToString();
            result.ContainingType = symbol.ContainingType?.ToDisplayString();
            result.ContainingNamespace = symbol.ContainingNamespace?.ToDisplayString();

            foreach (var loc in symbol.Locations)
            {
                if (loc.IsInSource)
                {
                    var lineSpan = loc.GetLineSpan();
                    var sourceText = await loc.SourceTree!.GetTextAsync();
                    var defLine = sourceText.Lines[lineSpan.StartLinePosition.Line];

                    result.Definitions.Add(new DefinitionLocationInfo
                    {
                        FilePath = loc.SourceTree.FilePath,
                        StartLine = lineSpan.StartLinePosition.Line + 1,
                        StartColumn = lineSpan.StartLinePosition.Character + 1,
                        EndLine = lineSpan.EndLinePosition.Line + 1,
                        EndColumn = lineSpan.EndLinePosition.Character + 1,
                        Preview = defLine.ToString().Trim(),
                        IsFromMetadata = false
                    });
                }
                else if (loc.IsInMetadata)
                {
                    result.Definitions.Add(new DefinitionLocationInfo
                    {
                        FilePath = symbol.ContainingAssembly?.Name ?? "metadata",
                        Preview = symbol.ToDisplayString(),
                        IsFromMetadata = true,
                        AssemblyName = symbol.ContainingAssembly?.Name
                    });
                }
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    // ─── Tool 4: GetDocumentSymbols ──────────────────────────────────────

    public async Task<List<DocumentSymbolInfo>> GetDocumentSymbolsAsync(string filePath)
    {
        var symbols = new List<DocumentSymbolInfo>();

        try
        {
            var document = FindDocument(filePath);
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
        info.Modifiers = modifiers.Select(m => m.Text).ToList();
    }

    // ─── Tool 5: SearchSymbols ───────────────────────────────────────────

    public async Task<SearchSymbolsResult> SearchSymbolsAsync(string query, int maxResults)
    {
        var result = new SearchSymbolsResult();

        try
        {
            var solution = _workspace.CurrentSolution;

            foreach (var project in solution.Projects)
            {
                if (result.Symbols.Count >= maxResults) break;

                var symbols = await SymbolFinder.FindDeclarationsAsync(
                    project, query, ignoreCase: true,
                    filter: SymbolFilter.TypeAndMember,
                    cancellationToken: CancellationToken.None);

                foreach (var symbol in symbols)
                {
                    if (result.Symbols.Count >= maxResults) break;
                    if (symbol.Locations.Length == 0) continue;

                    var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
                    if (loc == null) continue;

                    var lineSpan = loc.GetLineSpan();
                    result.Symbols.Add(new SymbolSearchInfo
                    {
                        Name = symbol.Name,
                        FullName = symbol.ToDisplayString(),
                        Kind = symbol.Kind.ToString(),
                        FilePath = loc.SourceTree?.FilePath ?? string.Empty,
                        StartLine = lineSpan.StartLinePosition.Line + 1,
                        StartColumn = lineSpan.StartLinePosition.Character + 1,
                        ContainingType = symbol.ContainingType?.ToDisplayString(),
                        ContainingNamespace = symbol.ContainingNamespace?.ToDisplayString()
                    });
                }
            }

            result.TotalCount = result.Symbols.Count;
            result.Truncated = result.Symbols.Count >= maxResults;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    // ─── Tool 6: GetSymbolInfo ───────────────────────────────────────────

    public async Task<SymbolDetailInfo> GetSymbolInfoAsync(string filePath, int line, int column)
    {
        var result = new SymbolDetailInfo();

        try
        {
            var document = FindDocument(filePath);
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

            var position = GetPosition(syntaxTree, line, column);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);
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
                    result.Interfaces = typeSymbol.Interfaces.Select(i => i.ToDisplayString()).ToList();
                    break;
                case IMethodSymbol methodSymbol:
                    result.ReturnType = methodSymbol.ReturnType.ToDisplayString();
                    result.Parameters = methodSymbol.Parameters.Select(p => new Shared.Models.ParameterInfo
                    {
                        Name = p.Name,
                        Type = p.Type.ToDisplayString(),
                        IsOptional = p.IsOptional,
                        DefaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null
                    }).ToList();
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

    // ─── Tool 7: AnalyzeComplexity ───────────────────────────────────────

    public async Task<List<ComplexityInfo>> AnalyzeComplexityAsync(string filePath)
    {
        var results = new List<ComplexityInfo>();

        try
        {
            var document = FindDocument(filePath);
            if (document == null) return results;

            var root = await document.GetSyntaxRootAsync();
            if (root == null) return results;

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var walker = new CyclomaticComplexityWalker();
                walker.Visit(method);

                var lineSpan = method.GetLocation().GetLineSpan();
                var lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;

                results.Add(new ComplexityInfo
                {
                    MemberName = method.Identifier.Text,
                    Kind = "Method",
                    CyclomaticComplexity = walker.Complexity,
                    StartLine = lineSpan.StartLinePosition.Line + 1,
                    EndLine = lineSpan.EndLinePosition.Line + 1,
                    LineCount = lineCount,
                    Rating = GetComplexityRating(walker.Complexity)
                });
            }

            var constructors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();
            foreach (var ctor in constructors)
            {
                var walker = new CyclomaticComplexityWalker();
                walker.Visit(ctor);

                var lineSpan = ctor.GetLocation().GetLineSpan();
                var lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;

                results.Add(new ComplexityInfo
                {
                    MemberName = ctor.Identifier.Text,
                    Kind = "Constructor",
                    CyclomaticComplexity = walker.Complexity,
                    StartLine = lineSpan.StartLinePosition.Line + 1,
                    EndLine = lineSpan.EndLinePosition.Line + 1,
                    LineCount = lineCount,
                    Rating = GetComplexityRating(walker.Complexity)
                });
            }

            // Properties with logic in accessors
            var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .Where(p => p.AccessorList?.Accessors.Any(a => a.Body != null || a.ExpressionBody != null) == true);
            foreach (var prop in properties)
            {
                var walker = new CyclomaticComplexityWalker();
                walker.Visit(prop);

                if (walker.Complexity > 1)
                {
                    var lineSpan = prop.GetLocation().GetLineSpan();
                    results.Add(new ComplexityInfo
                    {
                        MemberName = prop.Identifier.Text,
                        Kind = "Property",
                        CyclomaticComplexity = walker.Complexity,
                        StartLine = lineSpan.StartLinePosition.Line + 1,
                        EndLine = lineSpan.EndLinePosition.Line + 1,
                        LineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1,
                        Rating = GetComplexityRating(walker.Complexity)
                    });
                }
            }
        }
        catch
        {
            // Return what we have
        }

        return results;
    }

    private static string GetComplexityRating(int complexity)
    {
        return complexity switch
        {
            <= 5 => "Low",
            <= 10 => "Moderate",
            <= 20 => "High",
            _ => "Very High"
        };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static int GetPosition(SyntaxTree syntaxTree, int line, int column)
    {
        var text = syntaxTree.GetText();
        var lineInfo = text.Lines[line - 1];
        return lineInfo.Start + (column - 1);
    }

    private static string? GetContainingMemberName(SyntaxNode root, TextSpan span)
    {
        var node = root.FindNode(span);
        while (node != null)
        {
            switch (node)
            {
                case MethodDeclarationSyntax m: return m.Identifier.Text;
                case PropertyDeclarationSyntax p: return p.Identifier.Text;
                case ConstructorDeclarationSyntax c: return c.Identifier.Text;
                case ClassDeclarationSyntax cls: return cls.Identifier.Text;
            }
            node = node.Parent;
        }
        return null;
    }
}

/// <summary>
/// Walks a syntax tree and counts decision points for cyclomatic complexity.
/// </summary>
internal class CyclomaticComplexityWalker : CSharpSyntaxWalker
{
    public int Complexity { get; private set; } = 1;

    public override void VisitIfStatement(IfStatementSyntax node) { Complexity++; base.VisitIfStatement(node); }
    public override void VisitElseClause(ElseClauseSyntax node)
    {
        if (node.Statement is not IfStatementSyntax) Complexity++;
        base.VisitElseClause(node);
    }
    public override void VisitWhileStatement(WhileStatementSyntax node) { Complexity++; base.VisitWhileStatement(node); }
    public override void VisitForStatement(ForStatementSyntax node) { Complexity++; base.VisitForStatement(node); }
    public override void VisitForEachStatement(ForEachStatementSyntax node) { Complexity++; base.VisitForEachStatement(node); }
    public override void VisitDoStatement(DoStatementSyntax node) { Complexity++; base.VisitDoStatement(node); }
    public override void VisitCaseSwitchLabel(CaseSwitchLabelSyntax node) { Complexity++; base.VisitCaseSwitchLabel(node); }
    public override void VisitCasePatternSwitchLabel(CasePatternSwitchLabelSyntax node) { Complexity++; base.VisitCasePatternSwitchLabel(node); }
    public override void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node) { Complexity++; base.VisitSwitchExpressionArm(node); }
    public override void VisitConditionalExpression(ConditionalExpressionSyntax node) { Complexity++; base.VisitConditionalExpression(node); }
    public override void VisitCatchClause(CatchClauseSyntax node) { Complexity++; base.VisitCatchClause(node); }
    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.LogicalAndExpression) ||
            node.IsKind(SyntaxKind.LogicalOrExpression) ||
            node.IsKind(SyntaxKind.CoalesceExpression))
            Complexity++;
        base.VisitBinaryExpression(node);
    }
}
