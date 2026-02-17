using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.LanguageServices;

namespace RoslynMcpExtension.Services;

internal class DocumentFinder(VisualStudioWorkspace workspace)
{
	public VisualStudioWorkspace Workspace => workspace;

	public Document? FindDocument(string filePath)
	{
		var allDocuments = workspace.CurrentSolution.Projects
			.SelectMany(p => p.Documents)
			.Where(d => d.FilePath != null)
			.ToList();

		var normalizedPath = Path.GetFullPath(filePath);
		var exact = allDocuments.FirstOrDefault(d =>
			string.Equals(d.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase));

		if (exact != null)
			return exact;

		var fileName = Path.GetFileName(filePath);
		var candidates = allDocuments
			.Where(d => string.Equals(Path.GetFileName(d.FilePath), fileName, StringComparison.OrdinalIgnoreCase))
			.ToList();

		if (candidates.Count == 1)
			return candidates[0];

		if (candidates.Count > 1)
		{
			var inputSegments = GetSegments(filePath);
			Document? best = null;
			var bestScore = 0;
			foreach (var candidate in candidates)
			{
				var docSegments = GetSegments(candidate.FilePath!);
				var score = CountMatchingTrailingSegments(inputSegments, docSegments);
				if (score > bestScore)
				{
					bestScore = score;
					best = candidate;
				}
			}
			return best;
		}

		return null;
	}

	public static int GetPosition(SyntaxTree syntaxTree, int line, int column)
	{
		var text = syntaxTree.GetText();
		var lineInfo = text.Lines[line - 1];
		return lineInfo.Start + (column - 1);
	}

	public static string? GetContainingMemberName(SyntaxNode root, TextSpan span)
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

	private static string[] GetSegments(string path)
	{
		return path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
	}

	private static int CountMatchingTrailingSegments(string[] a, string[] b)
	{
		var count = 0;
		var ai = a.Length - 1;
		var bi = b.Length - 1;
		while (ai >= 0 && bi >= 0)
		{
			if (string.Equals(a[ai], b[bi], StringComparison.OrdinalIgnoreCase))
			{
				count++;
				ai--;
				bi--;
			}
			else
			{
				break;
			}
		}
		return count;
	}
}
