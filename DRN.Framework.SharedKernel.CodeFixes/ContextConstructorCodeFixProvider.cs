using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Formatting;

namespace DRN.Framework.SharedKernel.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ContextConstructorCodeFixProvider)), Shared]
public sealed class ContextConstructorCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["DRNPERSISTENCE001"];
    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var target = await FindTarget(context.Document, context.Span.Start, context.CancellationToken);
        if (target is null) return;
        var snapshot = context.Document.Project.Solution;
        context.RegisterCodeFix(CodeAction.Create("Add context options constructor",
            token => AddConstructor(context.Document, context.Span.Start, snapshot, token), "DrnContextOptionsConstructor"), context.Diagnostics);
    }

    private static async Task<Solution> AddConstructor(Document document, int position, Solution snapshot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var current = snapshot.Workspace.CurrentSolution;
        if (!ReferenceEquals(current, snapshot) || current.GetDocument(document.Id) is null) return current;
        var target = await FindTarget(document, position, token);
        if (target is null) return snapshot.Workspace.CurrentSolution;
        var (declaration, options) = target.Value;
        var constructor = SyntaxFactory.ConstructorDeclaration(declaration.Identifier.WithoutTrivia())
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("options"))
                    .WithType(SyntaxFactory.ParseTypeName(options.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))))))
            .WithInitializer(SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer,
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("options"))))))
            .WithBody(SyntaxFactory.Block()).WithAdditionalAnnotations(Formatter.Annotation);
        var root = await document.GetSyntaxRootAsync(token);
        token.ThrowIfCancellationRequested();
        current = snapshot.Workspace.CurrentSolution;
        if (!ReferenceEquals(current, snapshot)) return current;
        var updatedDeclaration = declaration.OpenBraceToken.IsKind(SyntaxKind.OpenBraceToken)
            ? declaration.AddMembers(constructor)
            : declaration
                .WithSemicolonToken(default)
                .WithOpenBraceToken(SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(SyntaxFactory.Space),
                    SyntaxKind.OpenBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.Space)))
                .WithCloseBraceToken(SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(SyntaxFactory.Space),
                    SyntaxKind.CloseBraceToken,
                    declaration.SemicolonToken.TrailingTrivia))
                .AddMembers(constructor)
                .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root!.ReplaceNode(declaration, updatedDeclaration)).Project.Solution;
    }

    private static async Task<(ClassDeclarationSyntax Declaration, INamedTypeSymbol Options)?> FindTarget(
        Document document, int position, CancellationToken token)
    {
        if (await FindEditableClassDeclarationAsync(document, position, token) is not var (declaration, model, type))
            return null;
        if (ResolveSingleOptionsConstructor(type, model, declaration, token) is not var (baseConstructor, options))
            return null;
        if (!await ValidatePartialDeclarationsAsync(document, type, model, token))
            return null;
        if (!await ValidateExistingConstructorsAsync(type, model, baseConstructor, options, token))
            return null;
        return (declaration, options);
    }

    private static async Task<(ClassDeclarationSyntax Declaration, SemanticModel Model, INamedTypeSymbol Type)?> FindEditableClassDeclarationAsync(
        Document document, int position, CancellationToken token)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(token);
        if (syntaxRoot?.FindToken(position).Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault() is not { } declaration ||
            declaration.ParameterList is not null || declaration.ContainsDirectives) return null;
        var root = await ClassificationEdit.FindEditableRoot(document, position, token);
        if (root is null) return null;
        var model = await document.GetSemanticModelAsync(token);
        if (model?.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol type || type.IsAbstract ||
            type.TypeParameters.Length != 0 || type.BaseType is null) return null;
        for (var containing = type.ContainingType; containing is not null; containing = containing.ContainingType)
            if (containing.TypeParameters.Length != 0) return null;
        return (declaration, model, type);
    }

    private static (IMethodSymbol BaseConstructor, INamedTypeSymbol Options)? ResolveSingleOptionsConstructor(
        INamedTypeSymbol type, SemanticModel model, ClassDeclarationSyntax declaration, CancellationToken token)
    {
        var context = model.Compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.IDrnContext");
        var optionsDefinition = model.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContextOptions`1");
        if (context is null || optionsDefinition is null ||
            !type.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, context))) return null;
        var options = optionsDefinition.Construct(type);
        var baseConstructors = type.BaseType!.InstanceConstructors.Where(constructor => constructor.Parameters.Length == 1 &&
            constructor.Parameters[0].RefKind == RefKind.None &&
            SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, options) &&
            model.Compilation.IsSymbolAccessibleWithin(constructor, type)).ToArray();
        if (baseConstructors.Length != 1 || model.GetDiagnostics(declaration.Span, token).Any(item => item.Severity == DiagnosticSeverity.Error))
            return null;
        return (baseConstructors[0], options);
    }

    private static async Task<bool> ValidatePartialDeclarationsAsync(Document document, INamedTypeSymbol type,
        SemanticModel model, CancellationToken token)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            token.ThrowIfCancellationRequested();
            var partDocument = document.Project.Solution.GetDocument(reference.SyntaxTree);
            if (partDocument is null || partDocument.Project.Id != document.Project.Id || partDocument.GetLinkedDocumentIds().Length != 0 ||
                await ClassificationEdit.FindEditableRoot(partDocument, reference.Span.Start, token) is null ||
                await reference.GetSyntaxAsync(token) is not ClassDeclarationSyntax { ParameterList: null } part ||
                part.ContainsDirectives) return false;
            if (model.Compilation.GetSemanticModel(reference.SyntaxTree).GetDiagnostics(reference.Span, token)
                .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)) return false;
        }
        return true;
    }

    private static async Task<bool> ValidateExistingConstructorsAsync(INamedTypeSymbol type, SemanticModel model,
        IMethodSymbol baseConstructor, INamedTypeSymbol options, CancellationToken token)
    {
        // Do not add an alternative construction path around initialization or overload selection.
        foreach (var constructor in type.InstanceConstructors)
        {
            if (!await IsSupportedEmptyBaseConstructorAsync(constructor, model, baseConstructor, options, token))
                return false;
        }
        return true;
    }

    private static async Task<bool> IsSupportedEmptyBaseConstructorAsync(IMethodSymbol constructor, SemanticModel model,
        IMethodSymbol baseConstructor, INamedTypeSymbol options, CancellationToken token)
    {
        if (constructor.IsImplicitlyDeclared || constructor.Parameters.Length != 0 || constructor.DeclaringSyntaxReferences.Length != 1)
            return false;
        if (await constructor.DeclaringSyntaxReferences[0].GetSyntaxAsync(token) is not ConstructorDeclarationSyntax
            {
                Body.Statements.Count: 0, ExpressionBody: null, AttributeLists.Count: 0,
                Initializer: { RawKind: (int)SyntaxKind.BaseConstructorInitializer, ArgumentList.Arguments: [var argument] } initializer
            } || argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None)) return false;
        var expression = argument.Expression is PostfixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SuppressNullableWarningExpression
            } postfix
            ? postfix.Operand : argument.Expression;
        if (expression is not (DefaultExpressionSyntax or LiteralExpressionSyntax
            {
                RawKind: (int)SyntaxKind.NullLiteralExpression or (int)SyntaxKind.DefaultLiteralExpression
            })) return false;
        var constructorModel = ReferenceEquals(initializer.SyntaxTree, model.SyntaxTree)
            ? model : model.Compilation.GetSemanticModel(initializer.SyntaxTree);
        if (!SymbolEqualityComparer.Default.Equals(constructorModel.GetSymbolInfo(initializer, token).Symbol, baseConstructor))
            return false;
        return expression is not DefaultExpressionSyntax typedDefault ||
               SymbolEqualityComparer.Default.Equals(constructorModel.GetTypeInfo(typedDefault.Type, token).Type, options);
    }
}
