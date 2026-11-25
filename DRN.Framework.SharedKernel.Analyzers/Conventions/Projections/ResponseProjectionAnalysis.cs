using Microsoft.CodeAnalysis.Operations;
using static DRN.Framework.SharedKernel.Analyzers.ClassificationContract;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ResponseProjectionAnalysis
{
    internal static void CheckResponseProjections(
        ConventionFacts facts, INamedTypeSymbol type, Lazy<ClassificationContract?> classifications)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var tree = reference.SyntaxTree;
            if (facts.IsGenerated(tree) || !facts.Compilation.ContainsSyntaxTree(tree)) continue;
            if (!CheckResponseProjectionsInSyntax(facts, type, tree, reference.GetSyntax(facts.Cancellation), classifications))
                return;
        }
    }

    internal static void CheckDtoInitializerProjection(
        ConventionFacts facts, INamedTypeSymbol entity, INamedTypeSymbol dto,
        IObjectCreationOperation creation, ClassificationContract classifications)
    {
        var context = new ProjectionCheckContext(facts, entity, classifications);
        var assigned = AssignedProperties(creation.Initializer);
        if (creation.Initializer is { } initializer)
        {
            foreach (var (source, target, _) in MappedReadsCore(
                         initializer, facts, walkedCreations: null, matchConstructorArguments: false,
                         selectorScopes: null, bodyFlows: null))
            {
                var sourceReceiver = PropertyAccessFacts.Receiver(source, facts);
                CheckProjectedProperty(context, new(source.Property, sourceReceiver, source.Instance?.Type), target);
            }
        }
        foreach (var (source, target) in ImplicitMappings(facts, entity, dto, assigned))
            CheckProjectedProperty(context, new(source, entity), new(target, dto, null, creation.Syntax.GetLocation()));
    }

    internal static (IOperation? Target, IOperation? Value) ReadWrite(IOperation operation) => operation switch
    {
        ISimpleAssignmentOperation assignment => (assignment.Target, assignment.Value),
        IMemberInitializerOperation member => (member.InitializedMember, member.Initializer),
        _ => (null, null)
    };

    private static bool CheckResponseProjectionsInSyntax(
        ConventionFacts facts, INamedTypeSymbol type, SyntaxTree tree,
        SyntaxNode syntax, Lazy<ClassificationContract?> classifications)
    {
        SemanticModel? model = null;
        HashSet<SyntaxNode>? walkedCreations = null;
        Dictionary<(IAnonymousFunctionOperation, SelectorScope?), SelectorFlowResult>? selectorScopes = null;
        Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows = null;
        bool? hasResponseAlias = null;
        foreach (var node in syntax.DescendantNodes(descendIntoChildren: child =>
                     ReferenceEquals(child, syntax) || child is not BaseTypeDeclarationSyntax))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (walkedCreations?.Contains(node) == true) continue;
            if (!IsCandidateResponseProjection(facts, tree, node, ref hasResponseAlias, out var isWith)) continue;
            model ??= facts.SemanticModel(tree);
            if (ResolveResponseProjectionRoot(facts, model, node, isWith, ref bodyFlows) is not { } projectionRoot) continue;
            if (classifications.Value is not { } contract) return false;
            walkedCreations ??= [];
            selectorScopes ??= [];
            bodyFlows ??= [];
            CheckResponseProjectionReads(facts, type, projectionRoot, contract, walkedCreations, selectorScopes, bodyFlows);
        }
        return true;
    }

    private static bool IsCandidateResponseProjection(
        ConventionFacts facts, SyntaxTree tree, SyntaxNode node, ref bool? hasResponseAlias, out bool isWith)
    {
        isWith = false;
        if (node is BaseObjectCreationExpressionSyntax creationSyntax)
            return MayProjectContractResponse(facts, tree, creationSyntax, ref hasResponseAlias);
        if (node is InvocationExpressionSyntax invocation && IsSelectSyntax(invocation))
            return HasBlockSelectorArgument(invocation);
        isWith = node is WithExpressionSyntax { Initializer.Expressions.Count: > 0 };
        return isWith;
    }

    private static bool IsSelectSyntax(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Select" } or
            MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Select" } or
            SimpleNameSyntax { Identifier.ValueText: "Select" };

    private static bool HasBlockSelectorArgument(InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (UnwrapSelectorSyntax(argument.Expression) is AnonymousFunctionExpressionSyntax { Block: not null })
                return true;
        }
        return false;
    }

    private static ExpressionSyntax UnwrapSelectorSyntax(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;
                case BaseObjectCreationExpressionSyntax { ArgumentList.Arguments: [var single] }:
                    expression = single.Expression;
                    continue;
                default:
                    return expression;
            }
        }
    }

    private static IOperation? ResolveResponseProjectionRoot(
        ConventionFacts facts, SemanticModel model, SyntaxNode node, bool isWith,
        ref Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows)
    {
        var operation = model.GetOperation(node, facts.Cancellation);
        if (operation is IInvocationOperation call)
        {
            var isResponseSelect = LinqSequenceFacts.IsLinqSelect(call.TargetMethod, facts.Symbols) &&
                call.TargetMethod.TypeArguments is [.., INamedTypeSymbol result] &&
                facts.IsResponseModel(result);
            return isResponseSelect && ReturnedSequenceMatcher.IsReturnedSequence(call, facts, ref bodyFlows) ? call : null;
        }
        if (isWith)
        {
            return operation is IWithOperation { Type: INamedTypeSymbol withType } withOperation &&
                   facts.IsResponseModel(withType)
                ? withOperation.Initializer
                : null;
        }
        return operation is IObjectCreationOperation { Type: INamedTypeSymbol responseType } creation &&
               facts.IsResponseModel(responseType)
            ? creation
            : null;
    }

    private static bool MayProjectContractResponse(
        ConventionFacts facts, SyntaxTree tree, BaseObjectCreationExpressionSyntax creation, ref bool? hasResponseAlias)
    {
        // An empty creation with no arguments and no initializer cannot read entity or DTO properties.
        if (creation.Initializer is null && creation.ArgumentList is not { Arguments.Count: > 0 })
            return false;
        if (creation is not ObjectCreationExpressionSyntax explicitCreation)
            return true;
        if (explicitCreation.Type is PredefinedTypeSyntax)
            return false;
        if (ConventionFacts.RightmostTypeIdentifier(explicitCreation.Type) is { } name &&
            facts.MayBeResponseModel(name))
            return true;
        return hasResponseAlias ??= facts.HasResponseUsingAlias(tree);
    }

    private static void CheckResponseProjectionReads(
        ConventionFacts facts, INamedTypeSymbol ownerType, IOperation projectionRoot,
        ClassificationContract classifications, HashSet<SyntaxNode> walkedCreations,
        Dictionary<(IAnonymousFunctionOperation, SelectorScope?), SelectorFlowResult> selectorScopes,
        Dictionary<IBlockOperation, SelectorFlowResult> bodyFlows)
    {
        var context = new ProjectionCheckContext(facts, ownerType, classifications);
        foreach (var (source, target, locals) in MappedReadsCore(
                     projectionRoot, facts, walkedCreations, matchConstructorArguments: true, selectorScopes, bodyFlows))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!ProjectionOriginAnalyzer.HasEntityOrDtoOrigin(context, source, locals)) continue;
            var sourceReceiver = PropertyAccessFacts.Receiver(source, facts);
            CheckProjectedProperty(context, new(source.Property, sourceReceiver, source.Instance?.Type), target);
        }
    }

    private static IEnumerable<(IPropertyReferenceOperation Source, ProjectionTarget Target, SelectorScope? Locals)> MappedReadsCore(
        IOperation root, ConventionFacts facts, HashSet<SyntaxNode>? walkedCreations,
        bool matchConstructorArguments,
        Dictionary<(IAnonymousFunctionOperation, SelectorScope?), SelectorFlowResult>? selectorScopes,
        Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows)
    {
        var pending = new Stack<ProjectionNode>();
        var childScratch = new List<IOperation>();
        HashSet<LocalExpansionKey>? visitedExpansions = null;
        var initialScope = matchConstructorArguments
            ? SelectorFlowCollector.SeedEnclosingSelectorScopes(root, facts, selectorScopes, bodyFlows)
            : null;
        pending.Push(new(root, null, false, initialScope));
        while (pending.TryPop(out var entry))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (entry.Operation is INameOfOperation) continue;
            if (entry.Target is { } target && entry.Operation is IPropertyReferenceOperation source)
                yield return (source, target, entry.Locals);
            PushProjectionChildren(entry, facts, walkedCreations, matchConstructorArguments,
                pending, childScratch, ref visitedExpansions, selectorScopes, bodyFlows);
        }
    }

    private static void PushProjectionChildren(
        ProjectionNode entry, ConventionFacts facts, HashSet<SyntaxNode>? walkedCreations,
        bool matchConstructorArguments, Stack<ProjectionNode> pending, List<IOperation> childScratch,
        ref HashSet<LocalExpansionKey>? visitedExpansions,
        Dictionary<(IAnonymousFunctionOperation, SelectorScope?), SelectorFlowResult>? selectorScopes,
        Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows)
    {
        if (ProjectionLocalExpander.TryPushSelectorLocal(entry, facts, pending, ref visitedExpansions, selectorScopes))
            return;
        if (TryPushSpecialProjectionNode(entry, facts, walkedCreations, matchConstructorArguments, pending, selectorScopes, bodyFlows))
            return;
        if (IsIgnoredProjectionNode(entry.Operation))
            return;
        if (TryPushStorageAdd(entry, facts, pending))
            return;

        var (written, value) = ReadWrite(entry.Operation);
        if (value is not null)
        {
            PushWrite(entry, written, value, facts, pending);
            return;
        }
        PushRemainingChildren(entry, pending, childScratch);
    }

    private static bool TryPushSpecialProjectionNode(
        ProjectionNode entry, ConventionFacts facts, HashSet<SyntaxNode>? walkedCreations,
        bool matchConstructorArguments, Stack<ProjectionNode> pending,
        Dictionary<(IAnonymousFunctionOperation, SelectorScope?), SelectorFlowResult>? selectorScopes,
        Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows)
    {
        var (operation, _, isInput, _, _, _) = entry;
        if (matchConstructorArguments && !isInput && operation is IInvocationOperation call &&
            PushSequenceProjection(entry, call, facts, pending, selectorScopes, bodyFlows))
        {
            if (LinqSequenceFacts.IsLinqSelect(call.TargetMethod, facts.Symbols))
                walkedCreations?.Add(call.Syntax);
            return true;
        }
        if (matchConstructorArguments && !isInput && operation is IObjectCreationOperation creation)
        {
            walkedCreations?.Add(creation.Syntax);
            PushCreationChildren(entry, creation, facts, pending);
            return true;
        }
        if (!isInput && operation is IWithOperation)
            walkedCreations?.Add(operation.Syntax);
        if (operation is ICollectionExpressionOperation collection)
        {
            PushCollection(entry, collection, facts, pending);
            return true;
        }
        return false;
    }

    private static bool IsIgnoredProjectionNode(IOperation operation)
    {
        var isBindingOnlySubpattern = operation is IPropertySubpatternOperation
            { Pattern: IDeclarationPatternOperation { MatchesNull: true } or IDiscardPatternOperation };
        var isBindingOnlyTypeTest = operation is IIsPatternOperation test &&
            ProjectionOriginAnalyzer.IsBindingOnlyPattern(test.Pattern);
        var isPureOutTarget = operation is IArgumentOperation
            { Parameter.RefKind: RefKind.Out, Value: ILocalReferenceOperation or IDeclarationExpressionOperation or IDiscardOperation };
        return isBindingOnlySubpattern || isBindingOnlyTypeTest || isPureOutTarget;
    }

    private static void PushRemainingChildren(
        ProjectionNode entry, Stack<ProjectionNode> pending, List<IOperation> childScratch)
    {
        var (operation, target, isInput, _, _, suppressed) = entry;
        var skipBindingOnlySwitchValue = operation is ISwitchExpressionOperation selection &&
            ProjectionOriginAnalyzer.AreAllArmsBindingOnly(selection.Arms);
        var skippedSwitchValue = skipBindingOnlySwitchValue ? ((ISwitchExpressionOperation)operation).Value : null;
        var forwardSuppressed = operation is IObjectOrCollectionInitializerOperation && !suppressed.IsDefaultOrEmpty;

        // Only operations that store a constructed value directly into the DTO keep its initializer as the destination.
        RoslynOperationHelpers.CopyChildrenTo(operation, childScratch);
        for (var i = childScratch.Count - 1; i >= 0; i--)
        {
            var child = childScratch[i];
            if (skippedSwitchValue is not null && ReferenceEquals(child, skippedSwitchValue)) continue;
            var childIsInput = isInput || !PreservesDestination(operation, child);
            var next = forwardSuppressed && !childIsInput && child is ISimpleAssignmentOperation
                ? entry.NextWithSuppressed(child, target, false, suppressed)
                : entry.Next(child, target, childIsInput);
            pending.Push(next);
        }
        childScratch.Clear();
    }

    private static bool TryPushStorageAdd(ProjectionNode entry, ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        if (entry.IsInput) return false;
        if (entry.Operation is not IInvocationOperation
            { IsImplicit: true, Parent: IObjectOrCollectionInitializerOperation initializer } add ||
            add.Arguments is not [var argument] ||
            add.Instance?.Type is not INamedTypeSymbol collectionType)
            return false;
        var knownExactDefaultCollection = initializer.Parent is IObjectCreationOperation { Arguments.Length: 0 };
        if (!CollectionMutationFacts.IsStorageAdd(add.TargetMethod, collectionType, facts, knownExactDefaultCollection))
            return false;
        pending.Push(entry.Next(argument.Value, entry.Target, false));
        return true;
    }

    private static void PushCreationChildren(
        ProjectionNode entry, IObjectCreationOperation creation, ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        var outerTarget = entry.Target;
        if (creation.Initializer is { } initializer)
            pending.Push(entry.NextWithSuppressed(initializer, outerTarget, false, entry.SuppressedInitializerProperties));
        var destinationType = creation.Type as INamedTypeSymbol;
        var canMatchProperties = destinationType is { TypeKind: not TypeKind.Error } &&
            (facts.Role(destinationType).Role == ConventionArchitecture.Contract || facts.IsLocal(destinationType));
        for (var i = creation.Arguments.Length - 1; i >= 0; i--)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var argument = creation.Arguments[i];
            if (argument.ArgumentKind == ArgumentKind.DefaultValue) continue;
            var matched = canMatchProperties
                ? facts.ProjectionFacts.FindDestinationProperty(destinationType!, argument.Parameter)
                : null;
            if (matched is not null)
            {
                var argTarget = new ProjectionTarget(matched, destinationType!, destinationType, argument.Syntax.GetLocation());
                if (!entry.ReturnConditions.IsDefaultOrEmpty)
                {
                    for (var j = entry.ReturnConditions.Length - 1; j >= 0; j--)
                        pending.Push(new(entry.ReturnConditions[j], argTarget, true, entry.Locals));
                }
                pending.Push(new(argument.Value, argTarget, false, entry.Locals));
            }
            else if (outerTarget is not null)
            {
                pending.Push(entry.Next(argument.Value, outerTarget, true));
            }
        }
    }

    private static bool PushSequenceProjection(
        ProjectionNode entry, IInvocationOperation call, ConventionFacts facts, Stack<ProjectionNode> pending,
        Dictionary<(IAnonymousFunctionOperation, SelectorScope?), SelectorFlowResult>? selectorScopes,
        Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows)
    {
        var method = call.TargetMethod;
        if (call.Instance is null &&
            LinqSequenceFacts.TryGetPreservedSequenceArgumentCount(method, facts.Symbols, out var preservedCount))
        {
            PushPreservedSequenceArguments(entry, call, preservedCount, pending);
            return true;
        }
        if (!LinqSequenceFacts.IsLinqSelect(method, facts.Symbols)) return false;
        if (LinqSequenceFacts.SelectLambda(call) is not { } lambda ||
            RoslynOperationHelpers.TryGetArgumentByOrdinal(call, 0) is not { } source)
            return false;

        var parentScope = entry.Locals ?? SelectorFlowCollector.ResolveEnclosingBodyScope(lambda, facts, bodyFlows);
        var (returns, returnConditions, scope, _) = SelectorFlowCollector.GetOrCollect(
            lambda, parentScope, facts, selectorScopes);
        if (returnConditions is not null && entry.Target is not null)
        {
            for (var i = returnConditions.Count - 1; i >= 0; i--)
                pending.Push(new(returnConditions[i], entry.Target, true, scope));
        }
        for (var i = returns.Count - 1; i >= 0; i--)
        {
            var returned = returns[i];
            pending.Push(new(returned.Value, entry.Target, false, scope, returned.Conditions));
        }
        pending.Push(entry.Next(source.Value, entry.Target, true));
        return true;
    }

    private static void PushPreservedSequenceArguments(
        ProjectionNode entry, IInvocationOperation call, int preservedCount, Stack<ProjectionNode> pending)
    {
        var sourceTypeParameter = LinqSequenceFacts.FallbackElementTypeParameter(call.TargetMethod);
        for (var i = call.Arguments.Length - 1; i >= 0; i--)
        {
            var argument = call.Arguments[i];
            var preserves = LinqSequenceFacts.PreservesSequenceArgument(argument, preservedCount, sourceTypeParameter);
            pending.Push(entry.Next(argument.Value, entry.Target, !preserves));
        }
    }

    private static void PushWrite(
        ProjectionNode entry, IOperation? written, IOperation value, ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        var (operation, target, isInput, locals, returnConditions, suppressed) = entry;
        if (written is IPropertyReferenceOperation { Property.IsIndexer: true } indexer)
        {
            pending.Push(entry.Next(value, target, true));
            for (var i = indexer.Arguments.Length - 1; i >= 0; i--)
                pending.Push(entry.Next(indexer.Arguments[i], target, true));
            return;
        }
        var initializesProperty = !isInput &&
            operation.Parent is IObjectOrCollectionInitializerOperation &&
            written is IPropertyReferenceOperation;
        if (initializesProperty)
        {
            var property = (IPropertyReferenceOperation)written!;
            if (operation is ISimpleAssignmentOperation &&
                IsSuppressedInitializerProperty(suppressed, property.Property))
                return;
            target = new ProjectionTarget(property.Property, PropertyAccessFacts.Receiver(property, facts),
                property.Instance?.Type, (property.Parent ?? property).Syntax.GetLocation());
            if (!returnConditions.IsDefaultOrEmpty)
            {
                for (var i = returnConditions.Length - 1; i >= 0; i--)
                    pending.Push(new(returnConditions[i], target, true, locals));
            }
        }
        var next = initializesProperty && value is not IObjectOrCollectionInitializerOperation
            ? new ProjectionNode(value, target, isInput, locals)
            : entry.Next(value, target, isInput);
        pending.Push(next);
    }

    private static bool IsSuppressedInitializerProperty(
        ImmutableArray<IPropertySymbol> suppressed, IPropertySymbol written)
    {
        if (suppressed.IsDefaultOrEmpty) return false;
        for (var i = 0; i < suppressed.Length; i++)
        {
            if (PropertyAccessFacts.SameOrMatchingProperty(suppressed[i], written))
                return true;
        }
        return false;
    }

    private static void PushCollection(
        ProjectionNode entry, ICollectionExpressionOperation collection, ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        var (_, target, isInput, _, _, _) = entry;
        var usesConstructor = collection.ConstructMethod?.MethodKind == MethodKind.Constructor;
        if (!isInput && usesConstructor && collection.Type is INamedTypeSymbol { TypeKind: not TypeKind.Interface } addReceiver)
        {
            PushAddElements(entry, collection, addReceiver, facts, pending);
        }
        else
        {
            // Arrays, spans and compiler-provided interface collections store the element directly.
            var storesDirectly = !isInput &&
                ((collection.ConstructMethod is null && collection.Type is IArrayTypeSymbol or INamedTypeSymbol) ||
                 (usesConstructor && collection.Type?.TypeKind == TypeKind.Interface));
            for (var i = collection.Elements.Length - 1; i >= 0; i--)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                pending.Push(entry.Next(collection.Elements[i], target, !storesDirectly));
            }
        }
        for (var i = collection.ConstructArguments.Length - 1; i >= 0; i--)
            pending.Push(entry.Next(collection.ConstructArguments[i], target, true));
    }

    private static void PushAddElements(
        ProjectionNode entry, ICollectionExpressionOperation collection,
        INamedTypeSymbol addReceiver, ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        var target = entry.Target;
        var model = facts.SemanticModel(collection.Syntax.SyntaxTree);
        var knownExactDefaultCollection = collection.ConstructArguments.Length == 0;
        var singleAdd = model.LookupSymbols(collection.Syntax.SpanStart, addReceiver, "Add")
            is [IMethodSymbol { Parameters.Length: 1 } only] ? only : null;
        var singleStores = singleAdd is not null &&
            CollectionMutationFacts.IsStorageAdd(singleAdd, addReceiver, facts, knownExactDefaultCollection);
        for (var i = collection.Elements.Length - 1; i >= 0; i--)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var element = collection.Elements[i];
            var elementType = element is ISpreadOperation spread ? spread.ElementType : element.Type;
            var matchesSingleAdd = singleAdd is not null &&
                SymbolEqualityComparer.Default.Equals(singleAdd.Parameters[0].Type, elementType);
            var stored = matchesSingleAdd
                ? singleStores
                : CollectionMutationFacts.BindsToStorageAdd(model, addReceiver, element, facts, knownExactDefaultCollection);
            pending.Push(entry.Next(element, target, !stored));
        }
    }

    private static bool PreservesDestination(IOperation parent, IOperation child) => parent switch
    {
        IObjectOrCollectionInitializerOperation => true,
        IObjectCreationOperation creation => ReferenceEquals(child, creation.Initializer),
        IWithOperation with => ReferenceEquals(child, with.Initializer) || ReferenceEquals(child, with.Operand),
        IArrayCreationOperation array => ReferenceEquals(child, array.Initializer),
        IArrayInitializerOperation => true,
        ISpreadOperation spread => !spread.ElementConversion.IsUserDefined,
        IParenthesizedOperation => true,
        IConversionOperation { Conversion.IsUserDefined: false } => true,
        IConditionalOperation conditional => ReferenceEquals(child, conditional.WhenTrue) || ReferenceEquals(child, conditional.WhenFalse),
        IConditionalAccessOperation conditionalAccess => ReferenceEquals(child, conditionalAccess.WhenNotNull),
        ICoalesceOperation coalesce => ReferenceEquals(child, coalesce.WhenNull) || !coalesce.ValueConversion.IsUserDefined,
        ISwitchExpressionOperation switchExpression => !ReferenceEquals(child, switchExpression.Value),
        ISwitchExpressionArmOperation arm => ReferenceEquals(child, arm.Value),
        _ => false
    };

    private static void CheckProjectedProperty(
        ProjectionCheckContext context, ProjectionSource source, ProjectionTarget target)
    {
        var facts = context.Facts;
        var classifications = context.Classifications;
        facts.Cancellation.ThrowIfCancellationRequested();
        var (property, contextualKind) = classifications.ProjectionClassification(
            source.Property, source.SourceReceiver, source.View, facts.Cancellation);
        context.Reported.TryGetValue(target.Property, out var previous);
        if (((property | contextualKind) & ~previous) == PropertyClassifications.None) return;

        var onTarget = classifications.SymbolKinds(target.Property, facts.Cancellation);
        var missing = property & ~onTarget;
        var contextual = contextualKind & ~(onTarget | previous | missing);
        if (contextual != PropertyClassifications.None)
        {
            var effective = classifications.ProjectionClassification(
                target.Property, target.TargetReceiver, target.View, facts.Cancellation);
            missing |= contextual & ~(effective.Property | effective.Context);
        }
        missing &= ~previous;
        if (missing == PropertyClassifications.None) return;

        context.Reported[target.Property] = previous | missing;
        if (missing.HasFlag(PropertyClassifications.Personal))
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.DtoClassification, context.OwnerType, target.Location,
                target.Property.Name, "Personal", source.Property.Name);
        if (missing.HasFlag(PropertyClassifications.Sensitive))
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.DtoClassification, context.OwnerType, target.Location,
                target.Property.Name, "Sensitive", source.Property.Name);
    }

    private static HashSet<IPropertySymbol> AssignedProperties(IObjectOrCollectionInitializerOperation? initializer)
    {
        var assigned = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        if (initializer is null) return assigned;
        foreach (var assignment in initializer.Initializers)
        {
            if (ReadWrite(assignment).Target is IPropertyReferenceOperation property)
                assigned.Add(property.Property);
        }
        return assigned;
    }

    private static IEnumerable<(IPropertySymbol Source, IPropertySymbol Target)> ImplicitMappings(
        ConventionFacts facts, INamedTypeSymbol entity, INamedTypeSymbol dto, HashSet<IPropertySymbol> assigned)
    {
        var overridden = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        for (var contract = dto; contract is not null && !ConventionSymbols.Same(contract, facts.Symbols.Dto); contract = contract.BaseType)
        {
            foreach (var member in contract.GetMembers())
            {
                if (member is not IPropertySymbol target || !Eligible(target)) continue;
                if (target.OverriddenProperty is { } parent) overridden.Add(parent);
                if (overridden.Contains(target) || assigned.Contains(target)) continue;
                if (FindSourceProperty(entity, target.Name, facts.Symbols.Entity) is { } source)
                    yield return (source, target);
            }
        }
    }

    private static IPropertySymbol? FindSourceProperty(INamedTypeSymbol entity, string name, INamedTypeSymbol? entityBase)
    {
        for (var domain = entity; domain is not null && !ConventionSymbols.Same(domain, entityBase); domain = domain.BaseType)
        {
            foreach (var member in domain.GetMembers(name))
            {
                if (member is IPropertySymbol source) return source;
            }
        }
        return null;
    }
}
