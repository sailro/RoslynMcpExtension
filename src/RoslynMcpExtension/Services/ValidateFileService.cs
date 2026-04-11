using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using RoslynMcpExtension.Shared;

namespace RoslynMcpExtension.Services;

internal class ValidateFileService(DocumentFinder documentFinder)
{
	public async Task<ValidateFileResult> ValidateFileAsync(string filePath, bool includeWarnings, bool runAnalyzers)
	{
		var result = new ValidateFileResult { FilePath = filePath };

		try
		{
			var document = documentFinder.FindDocument(filePath);
			if (document == null)
			{
				result.ErrorMessage = $"File not found in any project: {filePath}";
				return result;
			}

			result.ProjectName = document.Project.Name;
			result.FilePath = document.FilePath!;
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
				.Where(d => d.Location.SourceTree?.FilePath != null && string.Equals(d.Location.SourceTree.FilePath, Path.GetFullPath(document.FilePath!), StringComparison.OrdinalIgnoreCase))
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
								 d.Severity is >= DiagnosticSeverity.Warning or DiagnosticSeverity.Info))
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
			Category = diag.Descriptor.Category
		};
	}
}
