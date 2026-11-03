using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class DiagnosticNameExemption
{
    private const string Marker = "DiagnosticNameExemption";
    private const int MaxCommentLength = 2048;

    private readonly ConcurrentDictionary<(SyntaxTree Tree, int Start), SyntaxTrivia> _comments = new();
    private readonly ConcurrentDictionary<(SyntaxTree Tree, int Start), byte> _used = new();

    internal bool Allows(SyntaxNode call, string name)
    {
        var statement = call.AncestorsAndSelf().TakeWhile(node => node is not AnonymousFunctionExpressionSyntax)
            .OfType<StatementSyntax>().FirstOrDefault();
        if (statement == null || !Supported(statement)) return false;
        foreach (var comment in Comments(statement))
        {
            if (!TryParse(comment.ToString(), out var target) || !string.Equals(target, name, StringComparison.Ordinal)) continue;
            _used.TryAdd((call.SyntaxTree, comment.SpanStart), 0);
            return true;
        }
        return false;
    }

    internal void Collect(SyntaxTreeAnalysisContext context)
    {
        if (context.IsGeneratedCode) return;
        foreach (var comment in context.Tree.GetRoot(context.CancellationToken).DescendantTrivia())
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!comment.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                !comment.ToString().AsSpan(2).TrimStart().StartsWith(Marker, StringComparison.Ordinal)) continue;
            if (!TryParse(comment.ToString(), out _))
                context.ReportDiagnostic(Diagnostic.Create(ClassificationDiagnosticDescriptors.InvalidNameExemptionComment,
                    comment.GetLocation()));
            else _comments.TryAdd((context.Tree, comment.SpanStart), comment);
        }
    }

    internal void ReportUnused(CompilationAnalysisContext context)
    {
        foreach (var (key, comment) in _comments)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!_used.ContainsKey(key))
                context.ReportDiagnostic(Diagnostic.Create(ClassificationDiagnosticDescriptors.UnusedNameExemptionComment,
                    comment.GetLocation()));
        }
    }

    private static bool Supported(SyntaxNode node) => node is ExpressionStatementSyntax or
        LocalDeclarationStatementSyntax or UsingStatementSyntax or ReturnStatementSyntax;

    private static IEnumerable<SyntaxTrivia> Comments(SyntaxNode statement)
    {
        // Only the adjacent leading comment block belongs to this statement. Blank lines end it.
        var newLines = 0;
        foreach (var trivia in statement.GetLeadingTrivia().Reverse())
        {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia)) continue;
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                if (++newLines > 1) yield break;
                continue;
            }
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)) yield break;
            newLines = 0;
            if (trivia.ToString().AsSpan(2).TrimStart().StartsWith(Marker, StringComparison.Ordinal))
                yield return trivia;
        }
    }

    private static bool TryParse(string comment, out string name)
    {
        name = "";
        if (comment.Length > MaxCommentLength) return false;
        var text = comment.AsSpan(2).Trim();
        if (!text.StartsWith(Marker + ":", StringComparison.Ordinal)) return false;
        text = text[(Marker.Length + 1)..].Trim();
        var separator = text.IndexOf('|');
        if (separator <= 0 || text[(separator + 1)..].Trim().Length == 0) return false;
        name = text[..separator].Trim().ToString();
        return name.Length is > 0 and <= 512 && name != "*";
    }

    internal static string NameMessage(string name, bool configuredPath) => configuredPath
        ? $"Logging name '{name}' matches a configured sensitive path. Remove or redact the value, or review the project classification rule. DiagnosticNameExemption comments cannot override configured paths."
        : $"Logging name '{name}' matches a sensitive-name rule. Remove or redact the value. For a reviewed name-only false positive, place '// DiagnosticNameExemption: {name} | <nonempty justification>' immediately above this statement. The comment matches this exact name only and does not exempt classified property values.";
}
