using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static DRN.Framework.SharedKernel.Analyzers.ClassificationContract;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionDtoAnalysis
{
    private readonly record struct DtoPlacement(string Namespace, string? Directory);

    internal static void Analyze(ConventionFacts facts)
    {
        var classifications = new Lazy<ClassificationContract?>(() =>
        {
            var sensitive = facts.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SensitiveAttribute");
            var personal = facts.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.PersonalAttribute");
            return sensitive is null || personal is null ? null : new ClassificationContract(sensitive, personal, facts.Compilation);
        }, isThreadSafe: false);
        var dtoStems = new Dictionary<(string Root, string Subdomain, string Stem), DtoPlacement?>();
        var messages = new List<INamedTypeSymbol>();
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var role = facts.Role(type);
            if (role.Role == ConventionArchitecture.Contract && MessageStemLength(type.Name) > 0 &&
                ConventionBoundaryAnalysis.IsPublic(type) && facts.Location(type) != Location.None)
                messages.Add(type);
            if (type.IsAbstract) continue;
            if (role.Role == ConventionArchitecture.Domain && facts.Symbols.IsKnown(type, facts.Symbols.Entity, facts.Cancellation) &&
                !facts.Symbols.IsOpen(type, facts.Cancellation) && facts.Location(type) != Location.None)
                CheckMapper(facts, type, classifications);
            if (role.Role == ConventionArchitecture.Contract && type.Name.EndsWith("Dto", StringComparison.Ordinal) &&
                type.Name.Length > 3 && facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation))
            {
                var stem = type.Name[..^3];
                var folder = ConventionNames.DtoFolder(stem);
                var subdomain = facts.Owner(type)?.Subdomain;
                if (facts.HasInvalidOwner(type)) continue;
                if (subdomain is not null && ConventionBoundaryAnalysis.IsPublic(type) && facts.Location(type).SourceTree is { } tree)
                {
                    var key = (role.Namespace, subdomain, stem);
                    var placement = new DtoPlacement(type.ContainingNamespace.ToDisplayString(),
                        facts.HasInvalidPath(tree) ? null : facts.LogicalDirectory(tree)?.Directory);
                    // A shared stem can identify more than one DTO. Only report a unique destination.
                    if (!dtoStems.TryAdd(key, placement) && dtoStems[key] != placement) dtoStems[key] = null;
                }
                if (facts.Location(type) != Location.None && !InDtoNamespace(facts, type, subdomain, folder))
                    Report(facts, ConventionDiagnosticDescriptors.DtoFolder, type, facts.Location(type), type.Name, folder);
                // DRNSTRUCTURE001 independently verifies each declaration's logical folder against its namespace.
            }
        }
        foreach (var message in messages)
            CheckMessagePlacement(facts, message, dtoStems);
    }

    private static int MessageStemLength(string name)
    {
        if (name.EndsWith("Request", StringComparison.Ordinal)) return name.Length - "Request".Length;
        if (name.EndsWith("Response", StringComparison.Ordinal)) return name.Length - "Response".Length;
        return 0;
    }

    private static void CheckMessagePlacement(ConventionFacts facts, INamedTypeSymbol message,
        Dictionary<(string Root, string Subdomain, string Stem), DtoPlacement?> dtoStems)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        if (facts.Owner(message) is not { Subdomain: var subdomain }) return;
        var role = facts.Role(message);
        var name = message.Name;
        var stemLength = MessageStemLength(name);
        for (var end = stemLength; end > 0; end--)
        {
            if (end != stemLength && !IsWordBoundary(name, end)) continue;
            var stem = name[..end];
            if (!dtoStems.TryGetValue((role.Namespace, subdomain, stem), out var placement)) continue;
            if (placement is { } dto)
                ConventionOwnershipAnalysis.AnalyzeTypePlacement(facts, message, facts.Location(message),
                    message.ContainingNamespace.ToDisplayString(), dto.Namespace, dto.Directory, ConventionDiagnosticDescriptors.DtoMessagePlacement);
            return;
        }
    }

    private static bool IsWordBoundary(string name, int index) => char.IsUpper(name[index]) &&
        (!char.IsUpper(name[index - 1]) || char.IsLower(name[index + 1]));

    private static bool InDtoNamespace(ConventionFacts facts, INamedTypeSymbol dto, string? subdomain, string folder) =>
        subdomain is not null && facts.Role(dto).Role == ConventionArchitecture.Contract &&
        dto.ContainingNamespace.ToDisplayString() == facts.Role(dto).Namespace + "." + subdomain + "." + folder;

    private static void CheckMapper(ConventionFacts facts, INamedTypeSymbol entity, Lazy<ClassificationContract?> classifications)
    {
        var owner = facts.Owner(entity);
        if (facts.HasInvalidOwner(entity)) return;
        var stem = entity.Name.EndsWith("Entity", StringComparison.Ordinal) && entity.Name.Length > 6 ? entity.Name[..^6] : entity.Name;
        var method = entity.GetMembers("ToDto").OfType<IMethodSymbol>().FirstOrDefault(candidate =>
            candidate is { IsStatic: false, Arity: 0, Parameters.Length: 0, DeclaredAccessibility: Accessibility.Public });
        var creation = method?.ReturnType is INamedTypeSymbol result && facts.Symbols.IsKnown(result, facts.Symbols.Dto, facts.Cancellation)
            ? GetConstruction(facts, method) : null;
        if (creation?.Type is not INamedTypeSymbol dto || !SymbolEqualityComparer.Default.Equals(dto, method?.ReturnType) ||
            !ConstructsWithEntity(facts, creation) || dto.Name != stem + "Dto" || !IsPublicDtoType(dto) ||
            !InDtoNamespace(facts, dto, owner?.Subdomain, ConventionNames.DtoFolder(stem)))
            Report(facts, ConventionDiagnosticDescriptors.EntityDto, entity, facts.Location(entity), entity.Name, stem + "Dto");
        if (creation?.Type is INamedTypeSymbol projected && facts.Symbols.IsKnown(projected, facts.Symbols.Dto, facts.Cancellation) &&
            classifications.Value is { } contract)
            CheckProjection(facts, entity, projected, creation, contract);
    }

    private static bool IsPublicDtoType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsPublicDtoType(array.ElementType),
        INamedTypeSymbol named => named.DeclaredAccessibility == Accessibility.Public &&
            named.TypeArguments.All(IsPublicDtoType) && (named.ContainingType is null || IsPublicDtoType(named.ContainingType)),
        IDynamicTypeSymbol => true,
        _ => false
    };

    private static IObjectCreationOperation? GetConstruction(ConventionFacts facts, IMethodSymbol method)
    {
        foreach (var reference in (method.PartialImplementationPart ?? method).DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(facts.Cancellation) is not MethodDeclarationSyntax syntax) continue;
            var expression = syntax.ExpressionBody?.Expression ??
                (syntax.Body?.Statements is [ReturnStatementSyntax statement] ? statement.Expression : null);
            if (expression is null || facts.SemanticModel(syntax.SyntaxTree).GetOperation(expression, facts.Cancellation)
                is not IObjectCreationOperation creation) continue;
            return creation;
        }
        return null;
    }

    private static bool ConstructsWithEntity(ConventionFacts facts, IObjectCreationOperation creation)
    {
        // The operation also contains compiler-supplied arguments for omitted optional parameters.
        if (creation.Arguments is not [{ ArgumentKind: ArgumentKind.Explicit } argument, ..] ||
            creation.Arguments.Skip(1).Any(arg => arg.ArgumentKind != ArgumentKind.DefaultValue)) return false;
        var value = argument.Value;
        while (value is IConversionOperation { Conversion.IsReference: true } conversion) value = conversion.Operand;
        return value is IInstanceReferenceOperation { Syntax: ThisExpressionSyntax, ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } &&
            ConventionSymbols.Same(argument.Parameter?.Type, facts.Symbols.Entity);
    }

    private static IEnumerable<(IPropertyReferenceOperation Source, IPropertyReferenceOperation Target)> MappedReads(
        IObjectOrCollectionInitializerOperation initializer, ConventionFacts facts)
    {
        var pending = new Stack<(IOperation Operation, IPropertyReferenceOperation? Target, bool IsInput)>();
        pending.Push((initializer, null, false));
        while (pending.TryPop(out var entry))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var (operation, target, isInput) = entry;
            if (operation is INameOfOperation) continue;
            if (operation is ICollectionExpressionOperation collection)
            {
                PushCollection(collection, target, isInput);
                continue;
            }
            if (!isInput && operation is IInvocationOperation { IsImplicit: true, Parent: IObjectOrCollectionInitializerOperation } add &&
                add.Arguments is [var argument] && add.Instance?.Type is INamedTypeSymbol collectionType &&
                IsStorageAdd(add.TargetMethod, collectionType, facts))
            {
                pending.Push((argument.Value, target, false));
                continue;
            }
            (IOperation? written, IOperation? value) = operation switch
            {
                ISimpleAssignmentOperation assignment => (assignment.Target, assignment.Value),
                IMemberInitializerOperation member => (member.InitializedMember, member.Initializer),
                _ => (null, null)
            };
            if (value is not null)
            {
                if (written is IPropertyReferenceOperation { Property.IsIndexer: true } indexer)
                {
                    pending.Push((value, target, true));
                    foreach (var key in indexer.Arguments.Reverse()) pending.Push((key, target, true));
                    continue;
                }
                if (!isInput && operation.Parent is IObjectOrCollectionInitializerOperation &&
                    written is IPropertyReferenceOperation property)
                    target = property;
                pending.Push((value, target, isInput));
                continue;
            }
            if (target is not null && operation is IPropertyReferenceOperation source) yield return (source, target);
            // Only operations that store a constructed value directly into the DTO keep its initializer as the destination.
            foreach (var child in operation.ChildOperations.Reverse())
                pending.Push((child, target, isInput || !PreservesDestination(operation, child)));
        }

        void PushCollection(ICollectionExpressionOperation collection, IPropertyReferenceOperation? target, bool isInput)
        {
            var constructor = collection.ConstructMethod?.MethodKind == MethodKind.Constructor;
            var addReceiver = constructor && collection.Type is INamedTypeSymbol { TypeKind: not TypeKind.Interface } type ? type : null;
            var model = !isInput && addReceiver is not null ? facts.SemanticModel(collection.Syntax.SyntaxTree) : null;
            var singleAdd = model is not null && model.LookupSymbols(collection.Syntax.SpanStart, addReceiver, "Add")
                is [IMethodSymbol { Parameters.Length: 1 } only]
                ? only : null;
            var singleStores = singleAdd is not null && addReceiver is not null && IsStorageAdd(singleAdd, addReceiver, facts);
            // Arrays, spans and compiler-provided interface collections store the element directly.
            var direct = !isInput && (collection.ConstructMethod is null && collection.Type is IArrayTypeSymbol or INamedTypeSymbol ||
                constructor && collection.Type?.TypeKind == TypeKind.Interface);

            for (var i = collection.Elements.Length - 1; i >= 0; i--)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                var element = collection.Elements[i];
                var stored = direct;
                if (model is not null && addReceiver is not null)
                {
                    var elementType = element is ISpreadOperation spread ? spread.ElementType : element.Type;
                    stored = singleAdd is not null && SymbolEqualityComparer.Default.Equals(singleAdd.Parameters[0].Type, elementType)
                        ? singleStores : BindsToStorageAdd(model, addReceiver, element, facts);
                }
                pending.Push((element, target, !stored));
            }
            for (var i = collection.ConstructArguments.Length - 1; i >= 0; i--)
                pending.Push((collection.ConstructArguments[i], target, true));
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
        ICoalesceOperation coalesce => ReferenceEquals(child, coalesce.WhenNull) || !coalesce.ValueConversion.IsUserDefined,
        ISwitchExpressionOperation switchExpression => !ReferenceEquals(child, switchExpression.Value),
        ISwitchExpressionArmOperation arm => ReferenceEquals(child, arm.Value),
        _ => false
    };

    private static bool BindsToStorageAdd(SemanticModel model, INamedTypeSymbol collection, IOperation element, ConventionFacts facts)
    {
        // Bracket expressions expose the argument but omit the implicit Add invocation.
        var argument = element.Syntax as ExpressionSyntax;
        if (element is ISpreadOperation { ElementType: { } itemType }) argument = DefaultValue(itemType);
        if (argument is null) return false;
        var add = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, DefaultValue(collection),
            SyntaxFactory.IdentifierName("Add"));
        var invocation = SyntaxFactory.InvocationExpression(add,
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(argument))));
        return model.GetSpeculativeSymbolInfo(element.Syntax.SpanStart, invocation, SpeculativeBindingOption.BindAsExpression).Symbol
            is IMethodSymbol method && IsStorageAdd(method, collection, facts);

        static ExpressionSyntax DefaultValue(ITypeSymbol type) => SyntaxFactory.DefaultExpression(
            SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    }

    private static bool IsStorageAdd(IMethodSymbol method, INamedTypeSymbol collection, ConventionFacts facts)
    {
        if (method.Name != "Add" || method.DeclaringSyntaxReferences.Length != 0) return false;
        // Collection contracts identify a stored element. Other Add methods can consume arbitrary helper inputs.
        foreach (var contract in collection.AllInterfaces.Prepend(collection))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var elementContract = contract.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T ||
                contract.MetadataName == "ISet`1" && ConventionSymbols.Same(contract, facts.Compilation.GetTypeByMetadataName("System.Collections.Generic.ISet`1")) ||
                contract.MetadataName == "IList" && ConventionSymbols.Same(contract, facts.Compilation.GetTypeByMetadataName("System.Collections.IList"));
            if (!elementContract) continue;
            // A derived Add can reimplement the storage contract even when the DTO assembly is metadata-only.
            if (method.ContainingType.BaseType?.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default) == true) continue;
            foreach (var member in contract.GetMembers("Add"))
                if (SymbolEqualityComparer.Default.Equals(method, member) ||
                    SymbolEqualityComparer.Default.Equals(method, collection.FindImplementationForInterfaceMember(member)))
                    return true;
        }
        return false;
    }

    private static void CheckProjection(ConventionFacts facts, INamedTypeSymbol entity, INamedTypeSymbol dto,
        IObjectCreationOperation creation, ClassificationContract classifications)
    {
        var reported = new Dictionary<IPropertySymbol, PropertyClassification>(SymbolEqualityComparer.Default);
        var assigned = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        if (creation.Initializer is { } initializer)
        {
            foreach (var assignment in initializer.Initializers)
            {
                var destination = assignment switch
                {
                    ISimpleAssignmentOperation simple => simple.Target,
                    IMemberInitializerOperation member => member.InitializedMember,
                    _ => null
                };
                if (destination is IPropertyReferenceOperation property) assigned.Add(property.Property);
            }
            foreach (var (source, target) in MappedReads(initializer, facts))
                Check(source.Property, Receiver(source), target.Property, Receiver(target), (target.Parent ?? target).Syntax.GetLocation(),
                    source.Instance?.Type, target.Instance?.Type);
        }
        var overridden = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        for (var contract = dto; contract is not null && !ConventionSymbols.Same(contract, facts.Symbols.Dto); contract = contract.BaseType)
        foreach (var target in contract.GetMembers().OfType<IPropertySymbol>().Where(Eligible))
        {
            if (target.OverriddenProperty is { } parent) overridden.Add(parent);
            if (overridden.Contains(target) || assigned.Contains(target)) continue;
            for (var domain = entity; domain is not null && !ConventionSymbols.Same(domain, facts.Symbols.Entity); domain = domain.BaseType)
            {
                if (domain.GetMembers(target.Name).OfType<IPropertySymbol>().FirstOrDefault() is not { } source) continue;
                Check(source, entity, target, dto, creation.Syntax.GetLocation());
                break;
            }
        }

        INamedTypeSymbol Receiver(IPropertyReferenceOperation property)
        {
            var instance = property.Instance;
            while (instance is not null)
            {
                var operand = instance switch
                {
                    IConditionalAccessInstanceOperation { Type.IsReferenceType: true } placeholder => ConditionalReceiver(placeholder, facts.Cancellation),
                    IConversionOperation { Conversion.IsUserDefined: false, Operand.Type: INamedTypeSymbol from, Type: { } to } conversion
                        when IsImplicitUpcastOrBoxing(facts.Compilation, from, to) => conversion.Operand,
                    _ => null
                };
                if (operand is null) break;
                instance = operand;
            }
            var receiver = instance?.Type;
            if (receiver is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable &&
                !SymbolEqualityComparer.Default.Equals(nullable, property.Property.ContainingType))
                receiver = nullable.TypeArguments[0];
            return receiver as INamedTypeSymbol ?? property.Property.ContainingType;
        }

        void Check(IPropertySymbol source, INamedTypeSymbol receiver, IPropertySymbol target, INamedTypeSymbol targetReceiver,
            Location location, ITypeSymbol? sourceView = null, ITypeSymbol? targetView = null)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var (property, context) = classifications.ProjectionClassification(source, receiver, sourceView, facts.Cancellation);
            reported.TryGetValue(target, out var previous);
            if (((property | context) & ~previous) == PropertyClassification.None) return;
            var onTarget = classifications.SymbolKinds(target, facts.Cancellation);
            var missing = property & ~onTarget;
            var contextual = context & ~(onTarget | previous | missing);
            if (contextual != PropertyClassification.None)
            {
                var effective = classifications.ProjectionClassification(target, targetReceiver, targetView, facts.Cancellation);
                missing |= contextual & ~(effective.Property | effective.Context);
            }
            missing &= ~previous;
            if (missing == PropertyClassification.None) return;
            reported[target] = previous | missing;
            if (missing.HasFlag(PropertyClassification.Personal)) Missing("Personal");
            if (missing.HasFlag(PropertyClassification.Sensitive)) Missing("Sensitive");
            void Missing(string kind) => Report(facts, ConventionDiagnosticDescriptors.DtoClassification, entity,
                location, target.Name, kind, source.Name);
        }
    }

    private static bool IsImplicitUpcastOrBoxing(Compilation compilation, ITypeSymbol from, ITypeSymbol to) =>
        compilation is CSharpCompilation csharp
            ? csharp.ClassifyConversion(from, to) is { IsImplicit: true } conversion &&
              (conversion.IsReference || conversion.IsBoxing || conversion.IsIdentity)
            : compilation.ClassifyCommonConversion(from, to) is { IsImplicit: true } common &&
              (common.IsReference || common.IsIdentity);

    private static IOperation? ConditionalReceiver(IConditionalAccessInstanceOperation placeholder, CancellationToken cancellation)
    {
        // In a?.B?.C the placeholder for B is in the receiver of the inner conditional and the non-null branch of the outer one.
        IOperation child = placeholder;
        while (child.Parent is { } parent)
        {
            cancellation.ThrowIfCancellationRequested();
            if (parent is IConditionalAccessOperation access && ReferenceEquals(access.WhenNotNull, child))
                return access.Operation;
            child = parent;
        }
        return null;
    }

    private static void Report(ConventionFacts facts, DiagnosticDescriptor descriptor, INamedTypeSymbol target,
        Location location, params object[] args) => facts.Diagnostics.Add(Diagnostic.Create(descriptor, location,
        ImmutableDictionary<string, string?>.Empty.Add("Target", target.GetDocumentationCommentId()), args));
}
