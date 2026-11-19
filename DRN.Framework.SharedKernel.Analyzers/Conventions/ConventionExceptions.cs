using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed class ConventionExceptions
{
    private static readonly string[] ScopedExceptionFields = [ConventionInputSyntax.EntityField, ConventionInputSyntax.ContextKind];
    private readonly ConventionFacts _facts;
    private readonly DateOnly _reviewDate;
    private readonly List<(ConventionInput Input, ISymbol? Symbol)> _valid = [];
    private readonly Dictionary<ISymbol, string?> _fingerprints = new(SymbolEqualityComparer.Default);

    internal ConventionExceptions(ConventionFacts facts) : this(facts, DateOnly.FromDateTime(DateTime.UtcNow)) { }

    internal ConventionExceptions(ConventionFacts facts, DateOnly reviewDate)
    {
        _facts = facts;
        _reviewDate = reviewDate;
        foreach (var input in facts.Records.Where(input => input.Kind == ConventionInputSyntax.ExceptionKind)) Validate(input);
        ValidateSourceTypeAttributes();
        ValidateGlobalAttributes(_facts.Compilation.Assembly);
        ValidateGlobalAttributes(_facts.Compilation.SourceModule);
        ValidatePragmas();
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Iterating GetMembers directly avoids boxing ImmutableArray for every source type.")]
    private void ValidateSourceTypeAttributes()
    {
        foreach (var type in _facts.Types)
        {
            ValidateAttributes(type);
            foreach (var member in type.GetMembers())
                if (member is not INamedTypeSymbol) ValidateAttributes(member);
        }
    }

    private static bool IsRule(string id) => ConventionDiagnosticDescriptors.All.Any(rule => rule.Id == id);

    private void Validate(ConventionInput input)
    {
        var target = input.Get(ConventionInputSyntax.TargetField);
        var symbol = ResolveDeclarationSymbol(target);
        var valid = IsRule(input.Identity) && target.Length > 0 && !target.Contains('*') &&
                    IsValidTarget(input.Identity, target, symbol) &&
                    !string.IsNullOrWhiteSpace(input.Get(ConventionInputSyntax.ReasonField)) &&
                    !string.IsNullOrWhiteSpace(input.Get(ConventionInputSyntax.OwnerField)) &&
                    DateOnly.TryParseExact(input.Get(ConventionInputSyntax.ReviewField), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var review) &&
                    review > _reviewDate &&
                    HasResolvableMetadataTypes(input) &&
                    MatchesBaselineFingerprint(input, symbol);
        if (!valid)
        {
            Issue(input.Identity, "malformed, unresolved, review-due or changed since its baseline", input.Location);
            return;
        }
        _valid.Add((input, symbol));
    }

    private ISymbol? ResolveDeclarationSymbol(string target)
    {
        var id = target.StartsWith('~') ? target[1..] : target;
        return DocumentationCommentId.GetFirstSymbolForDeclarationId(id, _facts.Compilation);
    }

    private bool IsValidTarget(string ruleId, string target, ISymbol? symbol)
    {
        if (IsSuppressionTarget(symbol)) return true;
        if (ruleId == "DRNARCHITECTURE003" && _facts.Records.Any(record =>
                record.Kind == ConventionInputSyntax.ReferenceKind &&
                record.Get(ConventionInputSyntax.StageField) is ConventionInputSyntax.ProjectField or ConventionInputSyntax.PackageField &&
                record.Identity == target))
            return true;
        var prefix = _facts.Project.AssemblyName + ":";
        return ruleId switch
        {
            "DRNARCHITECTURE006" => target.StartsWith(prefix, StringComparison.Ordinal) &&
                target[prefix.Length..] is ConventionArchitecture.Application or ConventionArchitecture.Infra or
                    ConventionArchitecture.Domain or ConventionArchitecture.Contract,
            "DRNOWNERSHIP003" => target == prefix + ConventionArchitecture.Domain,
            "DRNDEPENDENCYINJECTION003" => target == prefix + "Module",
            _ => false
        };
    }

    private bool HasResolvableMetadataTypes(ConventionInput input)
    {
        foreach (var field in ScopedExceptionFields)
        {
            var name = input.Get(field);
            if (name.Length > 0 && _facts.Compilation.GetTypeByMetadataName(name) is null) return false;
        }
        return true;
    }

    private bool MatchesBaselineFingerprint(ConventionInput input, ISymbol? symbol)
    {
        var fingerprint = input.Get(ConventionInputSyntax.FingerprintField);
        return fingerprint.Length == 0 ||
               symbol is not null && string.Equals(fingerprint, Fingerprint(symbol), StringComparison.Ordinal);
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
        if (diagnostic.Id == "DRNCONVENTIONEXCEPTION001" || _valid.Count == 0) return false;
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
        var model = _facts.SemanticModel(tree);
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
            if (ConventionInputSyntax.ConventionRulePrefix(id) is null) continue;
            var reason = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Justification").Value.Value as string;
            var location = attribute.ApplicationSyntaxReference?.GetSyntax(_facts.Cancellation).GetLocation() ?? _facts.Location(symbol);
            if (!IsRule(id) || string.IsNullOrWhiteSpace(reason)) Issue(id, "unknown or missing its justification", location);
        }
    }

    private void ValidateGlobalAttributes(ISymbol container)
    {
        ValidateAttributes(container);
        foreach (var attribute in container.GetAttributes())
        {
            if (!ConventionSymbols.Same(attribute.AttributeClass, _facts.Symbols.SuppressionAttribute) ||
                attribute.ConstructorArguments.Length < 2 || attribute.ConstructorArguments[1].Value is not string check ||
                ConventionInputSyntax.ConventionRulePrefix(check) is null) continue;
            var target = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == ConventionInputSyntax.TargetField).Value.Value as string;
            var scope = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Scope").Value.Value as string;
            var symbol = target is not null && !target.Contains('*')
                ? DocumentationCommentId.GetFirstSymbolForDeclarationId(target.TrimStart('~'), _facts.Compilation) : null;
            if (IsValidScopedSymbol(symbol, scope)) continue;
            Issue(check.Split(':')[0], "not bound to one resolvable symbol",
                attribute.ApplicationSyntaxReference?.GetSyntax(_facts.Cancellation).GetLocation() ?? Location.None);
        }
    }

    private static bool IsValidScopedSymbol(ISymbol? symbol, string? scope) =>
        symbol is INamedTypeSymbol && string.Equals(scope, "type", StringComparison.OrdinalIgnoreCase) ||
        IsSuppressionTarget(symbol) && symbol is not INamedTypeSymbol &&
        string.Equals(scope, "member", StringComparison.OrdinalIgnoreCase);

    private static bool IsSuppressionTarget(ISymbol? symbol) =>
        symbol is INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol;

    [SuppressMessage("SonarQube", "S3267", Justification = "Scan compilation trees directly without allocating a filtered sequence per compilation.")]
    private void ValidatePragmas()
    {
        foreach (var tree in _facts.Compilation.SyntaxTrees)
        {
            if (!_facts.IsGenerated(tree)) ValidateTreePragmas(tree);
        }
    }

    private void ValidateTreePragmas(SyntaxTree tree)
    {
        var active = new Dictionary<string, PragmaWarningDirectiveTriviaSyntax>(StringComparer.Ordinal);
        var root = tree.GetRoot(_facts.Cancellation);
        foreach (var pragma in root.DescendantTrivia(descendIntoTrivia: true)
                     .Select(trivia => trivia.GetStructure()).OfType<PragmaWarningDirectiveTriviaSyntax>())
        {
            _facts.Cancellation.ThrowIfCancellationRequested();
            if (pragma.IsActive) ValidatePragmaDirective(tree, root, pragma, active);
        }
        foreach (var (id, pragma) in active) Issue(id, "not restored in the same file", pragma.GetLocation());
    }

    private void ValidatePragmaDirective(SyntaxTree tree, SyntaxNode root, PragmaWarningDirectiveTriviaSyntax pragma,
        Dictionary<string, PragmaWarningDirectiveTriviaSyntax> active)
    {
        if (pragma.ErrorCodes.Count == 0)
        {
            if (pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                Issue("all warnings", "a blanket warning disable", pragma.GetLocation());
            return;
        }
        foreach (var code in pragma.ErrorCodes) ValidatePragmaCode(tree, root, pragma, code, active);
    }

    private void ValidatePragmaCode(SyntaxTree tree, SyntaxNode root, PragmaWarningDirectiveTriviaSyntax pragma,
        ExpressionSyntax code, Dictionary<string, PragmaWarningDirectiveTriviaSyntax> active)
    {
        var id = code.ToString();
        if (ConventionInputSyntax.ConventionRulePrefix(id) is null) return;
        if (!IsRule(id))
        {
            Issue(id, "unknown", code.GetLocation());
            return;
        }
        if (pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
        {
            if (ReadPragmaReason(tree, pragma).Length == 0 || !active.TryAdd(id, pragma))
                Issue(id, "missing its adjacent reason or already disabled", pragma.GetLocation());
            return;
        }
        if (!active.Remove(id, out var disabled))
            Issue(id, "restored without a matching local disable", pragma.GetLocation());
        else if (SpansMultipleDeclarations(root, disabled, pragma))
            Issue(id, "spanning multiple declarations", disabled.GetLocation());
    }

    private string ReadPragmaReason(SyntaxTree tree, PragmaWarningDirectiveTriviaSyntax pragma)
    {
        var text = tree.GetText(_facts.Cancellation);
        var line = text.Lines.GetLineFromPosition(pragma.SpanStart).LineNumber;
        var current = text.Lines[line].ToString();
        var comment = current.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0) return current[(comment + 2)..].Trim();
        var previous = line > 0 ? text.Lines[line - 1].ToString().Trim() : "";
        return previous.StartsWith("//", StringComparison.Ordinal) ? previous[2..].Trim() : "";
    }

    private static bool SpansMultipleDeclarations(SyntaxNode root, PragmaWarningDirectiveTriviaSyntax disabled,
        PragmaWarningDirectiveTriviaSyntax restored)
    {
        var span = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(disabled.Span.End, restored.SpanStart);
        var declarations = root.DescendantNodes(span).OfType<MemberDeclarationSyntax>()
            .Where(member => member is not BaseNamespaceDeclarationSyntax && span.Contains(member.Span) &&
                !member.Ancestors().OfType<MemberDeclarationSyntax>().Any(parent =>
                    parent is not BaseNamespaceDeclarationSyntax && span.Contains(parent.Span))).Take(2).Count();
        return declarations > 1;
    }

    private void Issue(string id, string problem, Location location) =>
        _facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Exception, location, id, problem));
}
