using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.SharedKernel.CodeFixes;

// TODO: Review all analyzers without code fixes and add fixes where the edit is clear and safe.
// Do not guess intent, classifications, exemption reasons, or model structure.

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClassificationCodeFixProvider)), Shared]
public sealed class ClassificationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["DRN0015", "DRN0018"];
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        if (await ClassificationEdit.FindTarget(context.Document, context.Span.Start, context.CancellationToken) == null) return;
        foreach (var name in new[] { "Personal", "Sensitive" })
            context.RegisterCodeFix(CodeAction.Create($"Add [{name}]",
                token => ClassificationEdit.Add(context.Document, context.Span.Start, name, false, token), name), context.Diagnostics);
    }
}

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NameExemptionCodeFixProvider)), Shared]
public sealed class NameExemptionCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["DRN0015"];
    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        if (await ClassificationEdit.FindTarget(context.Document, context.Span.Start, context.CancellationToken) == null) return;
        context.RegisterCodeFix(CodeAction.Create("Add [Sensitive(false)] (false positive)",
            token => ClassificationEdit.Add(context.Document, context.Span.Start, "Sensitive", true, token), "SensitiveFalse"), context.Diagnostics);
    }
}

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InvalidExemptionCodeFixProvider)), Shared]
public sealed class InvalidExemptionCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["DRN0016"];
    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        if (await FindArgument(context.Document, context.Span, context.CancellationToken) == null) return;
        context.RegisterCodeFix(CodeAction.Create("Use Sensitive(true)",
            token => UsePositiveClassification(context.Document, context.Span, token), "SensitiveTrue"), context.Diagnostics);
    }

    private static async Task<ExpressionSyntax?> FindArgument(Document document, TextSpan span, CancellationToken token)
    {
        var root = await ClassificationEdit.FindEditableRoot(document, span.Start, token);
        if (root == null) return null;
        var node = root.FindNode(span, getInnermostNodeForTie: true);
        if (node is AttributeSyntax { Parent: AttributeListSyntax list } &&
            list.Parent is not (ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax or FieldDeclarationSyntax) &&
            !(list.Parent is ParameterSyntax { Parent.Parent: RecordDeclarationSyntax } && list.Target?.Identifier.ValueText == "field")) return null;
        var model = await document.GetSemanticModelAsync(token);
        var arguments = node.DescendantNodesAndSelf()
            .Select(child => child switch
            {
                AttributeArgumentSyntax { NameEquals: null } attribute => attribute.Expression,
                ArgumentSyntax argument => argument.Expression,
                _ => null
            })
            .Where(expression => expression != null && model?.GetConstantValue(expression, token) is { HasValue: true, Value: false })
            .Take(2).ToArray();
        return arguments.Length == 1 ? arguments[0] : null;
    }

    private static async Task<Document> UsePositiveClassification(Document document, TextSpan span, CancellationToken token)
    {
        var argument = await FindArgument(document, span, token);
        if (argument == null) return document;
        var root = await document.GetSyntaxRootAsync(token);
        var replacement = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression).WithTriviaFrom(argument);
        return document.WithSyntaxRoot(root!.ReplaceNode(argument, replacement));
    }
}

internal static class ClassificationEdit
{
    internal static async Task<SyntaxNode?> FindEditableRoot(Document document, int position, CancellationToken token)
    {
        var path = document.FilePath ?? document.Name;
        if (path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)) return null;
        var root = await document.GetSyntaxRootAsync(token);
        if (root == null || root.GetLeadingTrivia().ToFullString().Contains("<auto-generated", StringComparison.OrdinalIgnoreCase)) return null;
        var model = await document.GetSemanticModelAsync(token);
        var declarations = root.FindToken(position).Parent?.AncestorsAndSelf()
            .Where(node => node is MemberDeclarationSyntax or ParameterSyntax) ?? [];
        foreach (var declaration in declarations)
        {
            var symbol = declaration is FieldDeclarationSyntax field
                ? model?.GetDeclaredSymbol(field.Declaration.Variables[0], token)
                : model?.GetDeclaredSymbol(declaration, token);
            if (symbol?.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() is
                    "System.CodeDom.Compiler.GeneratedCodeAttribute" or "System.Runtime.CompilerServices.CompilerGeneratedAttribute") == true) return null;
        }
        return root;
    }

    internal static async Task<SyntaxNode?> FindTarget(Document document, int position, CancellationToken token)
    {
        var root = await FindEditableRoot(document, position, token);
        return root?.FindToken(position).Parent?.AncestorsAndSelf().FirstOrDefault(node =>
            node is PropertyDeclarationSyntax || node is ParameterSyntax { Parent.Parent: RecordDeclarationSyntax });
    }

    internal static async Task<Document> Add(Document document, int position, string name, bool exempt, CancellationToken token)
    {
        var target = await FindTarget(document, position, token);
        if (target == null) return document;
        var model = await document.GetSemanticModelAsync(token);
        var lists = target switch { PropertyDeclarationSyntax property => property.AttributeLists, ParameterSyntax parameter => parameter.AttributeLists, _ => default };
        foreach (var existing in lists.Where(list => list.Target?.Identifier.ValueText == "property" ||
                     list.Target == null && target is PropertyDeclarationSyntax).SelectMany(list => list.Attributes))
        {
            if (model?.GetSymbolInfo(existing, token).Symbol is not IMethodSymbol constructor) continue;
            for (var type = constructor.ContainingType; type != null; type = type.BaseType)
                if (type.ToDisplayString() == "DRN.Framework.SharedKernel.Attributes." + name + "Attribute") return document;
        }
        var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::DRN.Framework.SharedKernel.Attributes." + name + "Attribute"));
        if (exempt) attribute = attribute.WithArgumentList(SyntaxFactory.AttributeArgumentList(
            SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)))));
        var list = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
        // Keep directives and documentation before the first attribute, not on the old first token.
        var leadingTrivia = target.GetLeadingTrivia();
        SyntaxNode replacement = target.WithoutLeadingTrivia() switch
        {
            PropertyDeclarationSyntax property => property.AddAttributeLists(list),
            ParameterSyntax parameter => parameter.AddAttributeLists(list.WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.PropertyKeyword)))),
            _ => target
        };
        var root = await document.GetSyntaxRootAsync(token);
        return document.WithSyntaxRoot(root!.ReplaceNode(target,
            replacement.WithLeadingTrivia(leadingTrivia).WithAdditionalAnnotations(Formatter.Annotation)));
    }
}
