using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class DiagnosticNameExemption
{
    private const string Marker = "DiagnosticNameExemption";
    private const int MaxCommentLength = 2048;

    private readonly ConcurrentDictionary<(SyntaxTree Tree, int Start), SyntaxTrivia> _comments = new();
    private readonly ConcurrentDictionary<(SyntaxTree Tree, int Start), byte> _used = new();
    private readonly ConcurrentDictionary<(SyntaxTree Tree, int Start), ParsedComment> _parsed = new();
    private readonly ConcurrentDictionary<SyntaxNode, ImmutableArray<ParsedComment>> _callComments = new();

    private readonly record struct ParsedComment(SyntaxTrivia Trivia, string? Target);

    internal bool Allows(SyntaxNode call, string name)
    {
        foreach (var comment in _callComments.GetOrAdd(call, ReadCallComments))
        {
            if (comment.Target is null || !string.Equals(comment.Target, name, StringComparison.Ordinal)) continue;
            _used.TryAdd((call.SyntaxTree, comment.Trivia.SpanStart), 0);
            return true;
        }
        return false;
    }

    private ImmutableArray<ParsedComment> ReadCallComments(SyntaxNode call)
    {
        var statement = call.AncestorsAndSelf().TakeWhile(node => node is not AnonymousFunctionExpressionSyntax)
            .OfType<StatementSyntax>().FirstOrDefault();
        if (statement == null || !Supported(statement)) return [];
        return Comments(statement).Select(comment => Parse(call.SyntaxTree, comment)).ToImmutableArray();
    }

    private ParsedComment Parse(SyntaxTree tree, SyntaxTrivia comment) =>
        _parsed.GetOrAdd((tree, comment.SpanStart), static (_, trivia) =>
            new(trivia, TryParse(trivia.ToString(), out var target) ? target : null), comment);

    internal void Collect(SyntaxTreeAnalysisContext context)
    {
        if (context.IsGeneratedCode) return;
        context.Tree.TryGetText(out var text);
        foreach (var comment in context.Tree.GetRoot(context.CancellationToken).DescendantTrivia())
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!IsExemptionCandidate(comment, text)) continue;
            if (Parse(context.Tree, comment).Target is null)
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
        statement.SyntaxTree.TryGetText(out var text);
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
            if (IsExemptionCandidate(trivia, text))
                yield return trivia;
        }
    }

    private static bool IsExemptionCandidate(SyntaxTrivia comment, SourceText? text)
    {
        if (!comment.IsKind(SyntaxKind.SingleLineCommentTrivia) || comment.Span.Length < 2 + Marker.Length)
            return false;
        if (text is null)
            return comment.ToString().AsSpan(2).TrimStart().StartsWith(Marker, StringComparison.Ordinal);

        var index = comment.SpanStart + 2;
        var end = Math.Min(comment.Span.End, text.Length);
        while (index < end && char.IsWhiteSpace(text[index])) index++;
        if (end - index < Marker.Length) return false;
        for (var i = 0; i < Marker.Length; i++)
            if (text[index + i] != Marker[i]) return false;
        return true;
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
