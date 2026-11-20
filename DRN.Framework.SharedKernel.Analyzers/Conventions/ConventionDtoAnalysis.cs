using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static DRN.Framework.SharedKernel.Analyzers.ClassificationContract;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionDtoAnalysis
{
    private readonly record struct DtoPlacement(string Namespace, string? Directory);
    private readonly record struct ProjectionNode(IOperation Operation, IPropertyReferenceOperation? Target, bool IsInput);

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
                CheckDtoPlacement(facts, type, role.Namespace, dtoStems);
        }
        foreach (var message in messages)
            CheckMessagePlacement(facts, message, dtoStems);
    }

    private static void CheckDtoPlacement(ConventionFacts facts, INamedTypeSymbol dto, string root,
        Dictionary<(string Root, string Subdomain, string Stem), DtoPlacement?> dtoStems)
    {
        var subdomain = facts.Owner(dto)?.Subdomain;
        if (facts.HasInvalidOwner(dto)) return;
        var stem = dto.Name[..^3];
        var folder = ConventionNames.DtoFolder(stem);
        var location = facts.Location(dto);
        if (subdomain is not null && ConventionBoundaryAnalysis.IsPublic(dto) && location.SourceTree is { } tree)
        {
            var key = (root, subdomain, stem);
            var placement = new DtoPlacement(dto.ContainingNamespace.ToDisplayString(),
                facts.HasInvalidPath(tree) ? null : facts.LogicalDirectory(tree)?.Directory);
            // A shared stem can identify more than one DTO. Only report a unique destination.
            if (!dtoStems.TryAdd(key, placement) && dtoStems[key] != placement) dtoStems[key] = null;
        }
        // DRNSTRUCTURE001 independently verifies each declaration's logical folder against its namespace.
        if (location != Location.None && !InDtoNamespace(facts, dto, subdomain, folder))
            Report(facts, ConventionDiagnosticDescriptors.DtoFolder, dto, location, dto.Name, folder);
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
        var returnType = method?.ReturnType;
        var creation = method is not null && returnType is INamedTypeSymbol result &&
            facts.Symbols.IsKnown(result, facts.Symbols.Dto, facts.Cancellation) ? GetConstruction(facts, method) : null;
        if (creation?.Type is not INamedTypeSymbol dto || !SymbolEqualityComparer.Default.Equals(dto, returnType) ||
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
        var pending = new Stack<ProjectionNode>();
        pending.Push(new(initializer, null, false));
        while (pending.TryPop(out var entry))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (entry.Operation is INameOfOperation) continue;
            if (entry.Target is { } target && entry.Operation is IPropertyReferenceOperation source)
                yield return (source, target);
            PushProjectionChildren(entry, facts, pending);
        }
    }

    private static void PushProjectionChildren(ProjectionNode entry, ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        var (operation, target, isInput) = entry;
        if (operation is ICollectionExpressionOperation collection)
        {
            PushCollection(collection, target, isInput, facts, pending);
            return;
        }
        if (!isInput && operation is IInvocationOperation { IsImplicit: true, Parent: IObjectOrCollectionInitializerOperation } add &&
            add.Arguments is [var argument] && add.Instance?.Type is INamedTypeSymbol collectionType &&
            IsStorageAdd(add.TargetMethod, collectionType, facts))
        {
            pending.Push(new(argument.Value, target, false));
            return;
        }
        var (written, value) = ReadWrite(operation);
        if (value is not null)
        {
            PushWrite(entry, written, value, pending);
            return;
        }
        // Only operations that store a constructed value directly into the DTO keep its initializer as the destination.
        foreach (var child in operation.ChildOperations.Reverse())
            pending.Push(new(child, target, isInput || !PreservesDestination(operation, child)));
    }

    private static (IOperation? Target, IOperation? Value) ReadWrite(IOperation operation) => operation switch
    {
        ISimpleAssignmentOperation assignment => (assignment.Target, assignment.Value),
        IMemberInitializerOperation member => (member.InitializedMember, member.Initializer),
        _ => (null, null)
    };

    private static void PushWrite(ProjectionNode entry, IOperation? written, IOperation value, Stack<ProjectionNode> pending)
    {
        var (operation, target, isInput) = entry;
        if (written is IPropertyReferenceOperation { Property.IsIndexer: true } indexer)
        {
            pending.Push(new(value, target, true));
            foreach (var key in indexer.Arguments.Reverse()) pending.Push(new(key, target, true));
            return;
        }
        if (!isInput && operation.Parent is IObjectOrCollectionInitializerOperation && written is IPropertyReferenceOperation property)
            target = property;
        pending.Push(new(value, target, isInput));
    }

    private static void PushCollection(ICollectionExpressionOperation collection, IPropertyReferenceOperation? target,
        bool isInput, ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        var constructor = collection.ConstructMethod?.MethodKind == MethodKind.Constructor;
        if (!isInput && constructor && collection.Type is INamedTypeSymbol { TypeKind: not TypeKind.Interface } addReceiver)
        {
            PushAddElements(collection, target, addReceiver, facts, pending);
        }
        else
        {
            // Arrays, spans and compiler-provided interface collections store the element directly.
            var direct = !isInput && (collection.ConstructMethod is null && collection.Type is IArrayTypeSymbol or INamedTypeSymbol ||
                constructor && collection.Type?.TypeKind == TypeKind.Interface);
            for (var i = collection.Elements.Length - 1; i >= 0; i--)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                pending.Push(new(collection.Elements[i], target, !direct));
            }
        }
        for (var i = collection.ConstructArguments.Length - 1; i >= 0; i--)
            pending.Push(new(collection.ConstructArguments[i], target, true));
    }

    private static void PushAddElements(ICollectionExpressionOperation collection, IPropertyReferenceOperation? target,
        INamedTypeSymbol addReceiver, ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        var model = facts.SemanticModel(collection.Syntax.SyntaxTree);
        var singleAdd = model.LookupSymbols(collection.Syntax.SpanStart, addReceiver, "Add")
            is [IMethodSymbol { Parameters.Length: 1 } only] ? only : null;
        var singleStores = singleAdd is not null && IsStorageAdd(singleAdd, addReceiver, facts);
        for (var i = collection.Elements.Length - 1; i >= 0; i--)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var element = collection.Elements[i];
            var elementType = element is ISpreadOperation spread ? spread.ElementType : element.Type;
            var stored = singleAdd is not null && SymbolEqualityComparer.Default.Equals(singleAdd.Parameters[0].Type, elementType)
                ? singleStores : BindsToStorageAdd(model, addReceiver, element, facts);
            pending.Push(new(element, target, !stored));
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
            if (!IsElementContract(contract, facts.Compilation)) continue;
            // A derived Add can reimplement the storage contract even when the DTO assembly is metadata-only.
            if (method.ContainingType.BaseType?.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default) == true) continue;
            if (contract.GetMembers("Add").Any(member => SymbolEqualityComparer.Default.Equals(method, member) ||
                SymbolEqualityComparer.Default.Equals(method, collection.FindImplementationForInterfaceMember(member))))
                return true;
        }
        return false;
    }

    private static bool IsElementContract(INamedTypeSymbol contract, Compilation compilation) =>
        contract.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T ||
        contract.MetadataName == "ISet`1" && ConventionSymbols.Same(contract, compilation.GetTypeByMetadataName("System.Collections.Generic.ISet`1")) ||
        contract.MetadataName == "IList" && ConventionSymbols.Same(contract, compilation.GetTypeByMetadataName("System.Collections.IList"));

    private static void CheckProjection(ConventionFacts facts, INamedTypeSymbol entity, INamedTypeSymbol dto,
        IObjectCreationOperation creation, ClassificationContract classifications)
    {
        var reported = new Dictionary<IPropertySymbol, PropertyClassifications>(SymbolEqualityComparer.Default);
        var assigned = AssignedProperties(creation.Initializer);
        if (creation.Initializer is { } initializer)
        {
            foreach (var (source, target) in MappedReads(initializer, facts))
                Check(source.Property, Receiver(source, facts), target.Property, Receiver(target, facts), (target.Parent ?? target).Syntax.GetLocation(),
                    source.Instance?.Type, target.Instance?.Type);
        }
        foreach (var (source, target) in ImplicitMappings(facts, entity, dto, assigned))
            Check(source, entity, target, dto, creation.Syntax.GetLocation());

        void Check(IPropertySymbol source, INamedTypeSymbol receiver, IPropertySymbol target, INamedTypeSymbol targetReceiver,
            Location location, ITypeSymbol? sourceView = null, ITypeSymbol? targetView = null)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var (property, context) = classifications.ProjectionClassification(source, receiver, sourceView, facts.Cancellation);
            reported.TryGetValue(target, out var previous);
            if (((property | context) & ~previous) == PropertyClassifications.None) return;
            var onTarget = classifications.SymbolKinds(target, facts.Cancellation);
            var missing = property & ~onTarget;
            var contextual = context & ~(onTarget | previous | missing);
            if (contextual != PropertyClassifications.None)
            {
                var effective = classifications.ProjectionClassification(target, targetReceiver, targetView, facts.Cancellation);
                missing |= contextual & ~(effective.Property | effective.Context);
            }
            missing &= ~previous;
            if (missing == PropertyClassifications.None) return;
            reported[target] = previous | missing;
            if (missing.HasFlag(PropertyClassifications.Personal)) Missing("Personal");
            if (missing.HasFlag(PropertyClassifications.Sensitive)) Missing("Sensitive");
            void Missing(string kind) => Report(facts, ConventionDiagnosticDescriptors.DtoClassification, entity,
                location, target.Name, kind, source.Name);
        }
    }

    private static HashSet<IPropertySymbol> AssignedProperties(IObjectOrCollectionInitializerOperation? initializer)
    {
        var assigned = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        if (initializer is null) return assigned;
        foreach (var assignment in initializer.Initializers)
            if (ReadWrite(assignment).Target is IPropertyReferenceOperation property)
                assigned.Add(property.Property);
        return assigned;
    }

    private static IEnumerable<(IPropertySymbol Source, IPropertySymbol Target)> ImplicitMappings(
        ConventionFacts facts, INamedTypeSymbol entity, INamedTypeSymbol dto, HashSet<IPropertySymbol> assigned)
    {
        var overridden = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        for (var contract = dto; contract is not null && !ConventionSymbols.Same(contract, facts.Symbols.Dto); contract = contract.BaseType)
        {
            foreach (var target in contract.GetMembers().OfType<IPropertySymbol>().Where(Eligible))
            {
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
            if (domain.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault() is { } source)
                return source;
        }
        return null;
    }

    private static INamedTypeSymbol Receiver(IPropertyReferenceOperation property, ConventionFacts facts)
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
