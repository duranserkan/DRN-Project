using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed class ConventionExceptions
{
    private readonly ConventionFacts _facts;
    private readonly DateOnly _reviewDate;
    private readonly List<(ConventionInput Input, ISymbol? Symbol)> _valid = [];
    private readonly Dictionary<ISymbol, string?> _fingerprints = new(SymbolEqualityComparer.Default);

    internal ConventionExceptions(ConventionFacts facts) : this(facts, DateOnly.FromDateTime(DateTime.UtcNow)) { }

    internal ConventionExceptions(ConventionFacts facts, DateOnly reviewDate)
    {
        _facts = facts;
        _reviewDate = reviewDate;
        foreach (var input in facts.Records.Where(input => input.Kind == "Exception")) Validate(input);
        foreach (var type in facts.Types)
        {
            ValidateAttributes(type);
            foreach (var member in type.GetMembers())
                if (member is not INamedTypeSymbol) ValidateAttributes(member);
        }
        ValidateAssemblyAttributes();
        ValidatePragmas();
    }

    private static bool IsRule(string id) => ConventionDiagnosticDescriptors.All.Any(rule => rule.Id == id);

    private void Validate(ConventionInput input)
    {
        var target = input.Get("Target");
        var symbol = target.StartsWith("~", StringComparison.Ordinal)
            ? DocumentationCommentId.GetFirstSymbolForDeclarationId(target[1..], _facts.Compilation)
            : DocumentationCommentId.GetFirstSymbolForDeclarationId(target, _facts.Compilation);
        var edge = input.Identity == "DRNCV015" && _facts.Records.Any(record => record.Kind == "Reference" &&
            record.Get("Stage") is "Project" or "Package" && record.Identity == target);
        var prefix = _facts.Project.AssemblyName + ":";
        var projectTarget = input.Identity switch
        {
            "DRNCV021" => target.StartsWith(prefix, StringComparison.Ordinal) &&
                target[prefix.Length..] is "Application" or "Infra" or "Domain" or "Contract",
            "DRNCV022" => target == prefix + "Domain",
            "DRNCV032" => target == prefix + "Module",
            _ => false
        };
        var valid = IsRule(input.Identity) && target.Length > 0 && !target.Contains('*') &&
                    (IsSuppressionTarget(symbol) || edge || projectTarget) && !string.IsNullOrWhiteSpace(input.Get("Reason")) &&
                    !string.IsNullOrWhiteSpace(input.Get("Owner")) &&
                    DateOnly.TryParseExact(input.Get("Review"), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var review) && review > _reviewDate;
        foreach (var field in new[] { "Entity", "Context" })
        {
            var name = input.Get(field);
            if (name.Length > 0 && _facts.Compilation.GetTypeByMetadataName(name) is null) valid = false;
        }
        var fingerprint = input.Get("Fingerprint");
        if (fingerprint.Length != 0)
            valid &= symbol is not null && string.Equals(fingerprint, Fingerprint(symbol), StringComparison.Ordinal);
        if (!valid)
        {
            Issue(input.Identity, "malformed, unresolved, review-due or changed since its baseline", input.Location);
            return;
        }
        _valid.Add((input, symbol));
    }

    private string? Fingerprint(ISymbol symbol)
    {
        if (_fingerprints.TryGetValue(symbol, out var cached)) return cached;
        var references = symbol.DeclaringSyntaxReferences.OrderBy(reference =>
                ConventionProject.NormalizeSeparators(_facts.LogicalPath(reference.SyntaxTree) ?? reference.SyntaxTree.FilePath), StringComparer.Ordinal)
            .ThenBy(reference => reference.Span.Start).ToArray();
        if (references.Length == 0 || references.Any(reference =>
                _facts.LogicalPath(reference.SyntaxTree) is null || _facts.HasInvalidPath(reference.SyntaxTree)))
        {
            _fingerprints.Add(symbol, null);
            return null;
        }
        // A baseline binds exact declarations and logical homes, so edits and moves require fresh review.
        var source = string.Join("\n", references.Select(reference =>
            ConventionProject.NormalizeSeparators(_facts.LogicalPath(reference.SyntaxTree)!) + "\n" +
            reference.GetSyntax(_facts.Cancellation).ToFullString()));
        _facts.Cancellation.ThrowIfCancellationRequested();
        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        _fingerprints.Add(symbol, actual);
        return actual;
    }

    internal bool Applies(Diagnostic diagnostic)
    {
        if (diagnostic.Id == "DRNCV018" || _valid.Count == 0) return false;
        ISymbol? source = null;
        var sourceResolved = false;
        foreach (var (input, symbol) in _valid)
        {
            _facts.Cancellation.ThrowIfCancellationRequested();
            if (input.Identity != diagnostic.Id) continue;
            var target = input.Get("Target").TrimStart('~');
            var matches = diagnostic.Properties.TryGetValue("Target", out var declared) && declared == target;
            if (!matches && symbol is not null)
            {
                if (!sourceResolved)
                {
                    sourceResolved = true;
                    source = ResolveSourceSymbol(diagnostic);
                }
                matches = source is not null && SymbolEqualityComparer.Default.Equals(source, symbol);
            }
            if (!matches) continue;
            if (!Matches(input, diagnostic, "Entity") || !Matches(input, diagnostic, "Context")) continue;
            return true;
        }
        return false;
    }

    private ISymbol? ResolveSourceSymbol(Diagnostic diagnostic)
    {
        if (diagnostic.Location.SourceTree is not { } tree) return null;
        var model = _facts.Compilation.GetSemanticModel(tree);
        var node = tree.GetRoot(_facts.Cancellation).FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        return node.AncestorsAndSelf().Select(candidate => model.GetDeclaredSymbol(candidate, _facts.Cancellation))
            .FirstOrDefault(symbol => symbol is not null) ?? model.GetEnclosingSymbol(diagnostic.Location.SourceSpan.Start, _facts.Cancellation);
    }

    private static bool Matches(ConventionInput input, Diagnostic diagnostic, string field) => input.Get(field).Length == 0 ||
        diagnostic.Properties.TryGetValue(field, out var value) && value == input.Get(field);

    private void ValidateAttributes(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            _facts.Cancellation.ThrowIfCancellationRequested();
            if (!ConventionSymbols.Same(attribute.AttributeClass, _facts.Symbols.SuppressionAttribute) ||
                attribute.ConstructorArguments.Length < 2 || attribute.ConstructorArguments[1].Value is not string check) continue;
            var id = check.Split(':')[0];
            if (!id.StartsWith("DRNCV", StringComparison.Ordinal)) continue;
            var reason = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Justification").Value.Value as string;
            var location = attribute.ApplicationSyntaxReference?.GetSyntax(_facts.Cancellation).GetLocation() ?? _facts.Location(symbol);
            if (!IsRule(id) || string.IsNullOrWhiteSpace(reason)) Issue(id, "unknown or missing its justification", location);
        }
    }

    private void ValidateAssemblyAttributes()
    {
        ValidateAttributes(_facts.Compilation.Assembly);
        foreach (var attribute in _facts.Compilation.Assembly.GetAttributes())
        {
            if (!ConventionSymbols.Same(attribute.AttributeClass, _facts.Symbols.SuppressionAttribute) ||
                attribute.ConstructorArguments.Length < 2 || attribute.ConstructorArguments[1].Value is not string check ||
                !check.StartsWith("DRNCV", StringComparison.Ordinal)) continue;
            var target = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Target").Value.Value as string;
            var scope = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Scope").Value.Value as string;
            var symbol = target is not null && !target.Contains('*')
                ? DocumentationCommentId.GetFirstSymbolForDeclarationId(target.TrimStart('~'), _facts.Compilation) : null;
            if (symbol is INamedTypeSymbol && scope == "type" ||
                IsSuppressionTarget(symbol) && symbol is not INamedTypeSymbol && scope == "member") continue;
            Issue(check.Split(':')[0], "not bound to one resolvable symbol",
                attribute.ApplicationSyntaxReference?.GetSyntax(_facts.Cancellation).GetLocation() ?? Location.None);
        }
    }

    private static bool IsSuppressionTarget(ISymbol? symbol) =>
        symbol is INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol;

    private void ValidatePragmas()
    {
        foreach (var tree in _facts.Compilation.SyntaxTrees)
        {
            if (_facts.IsGenerated(tree)) continue;
            var active = new Dictionary<string, PragmaWarningDirectiveTriviaSyntax>(StringComparer.Ordinal);
            var root = tree.GetRoot(_facts.Cancellation);
            foreach (var pragma in root.DescendantTrivia(descendIntoTrivia: true)
                         .Select(trivia => trivia.GetStructure()).OfType<PragmaWarningDirectiveTriviaSyntax>())
            {
                _facts.Cancellation.ThrowIfCancellationRequested();
                if (!pragma.IsActive) continue;
                if (pragma.ErrorCodes.Count == 0)
                {
                    if (pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                        Issue("DRNCV*", "a blanket warning disable", pragma.GetLocation());
                    continue;
                }
                foreach (var code in pragma.ErrorCodes)
                {
                    var id = code.ToString();
                    if (!id.StartsWith("DRNCV", StringComparison.Ordinal)) continue;
                    if (!IsRule(id)) { Issue(id, "unknown", code.GetLocation()); continue; }
                    if (pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                    {
                        var text = tree.GetText(_facts.Cancellation);
                        var line = text.Lines.GetLineFromPosition(pragma.SpanStart).LineNumber;
                        var previous = line > 0 ? text.Lines[line - 1].ToString().Trim() : "";
                        var current = text.Lines[line].ToString();
                        var comment = current.IndexOf("//", StringComparison.Ordinal);
                        var reason = comment >= 0 ? current[(comment + 2)..].Trim() :
                            previous.StartsWith("//", StringComparison.Ordinal) ? previous[2..].Trim() : "";
                        if (reason.Length == 0 || !active.TryAdd(id, pragma))
                            Issue(id, "missing its adjacent reason or already disabled", pragma.GetLocation());
                    }
                    else if (!active.Remove(id, out var disabled))
                        Issue(id, "restored without a matching local disable", pragma.GetLocation());
                    else
                    {
                        var span = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(disabled.Span.End, pragma.SpanStart);
                        var declarations = root.DescendantNodes(span).OfType<MemberDeclarationSyntax>()
                            .Where(member => member is not BaseNamespaceDeclarationSyntax && span.Contains(member.Span) &&
                                !member.Ancestors().OfType<MemberDeclarationSyntax>().Any(parent =>
                                    parent is not BaseNamespaceDeclarationSyntax && span.Contains(parent.Span))).Take(2).Count();
                        if (declarations > 1)
                            Issue(id, "spanning multiple declarations", disabled.GetLocation());
                    }
                }
            }
            foreach (var (id, pragma) in active) Issue(id, "not restored in the same file", pragma.GetLocation());
        }
    }

    private void Issue(string id, string problem, Location location) =>
        _facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Exception, location, id, problem));
}
