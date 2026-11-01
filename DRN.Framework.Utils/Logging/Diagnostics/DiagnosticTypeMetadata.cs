using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.CompilerServices;
using DRN.Framework.SharedKernel.Attributes;
using Microsoft.Extensions.Compliance.Classification;

namespace DRN.Framework.Utils.Logging.Diagnostics;

internal static class DiagnosticTypeMetadata
{
    internal sealed record Member(PropertyInfo Property, bool Ignored, bool Sensitive, bool Exempt)
    {
        internal string Name => Property.Name;
        internal object? Read(object value) => Property.GetValue(value);
    }

    internal sealed class TypePlan(Type type)
    {
        internal bool Classified { get; } = HasClassification(type);
        private readonly Lazy<Member[]> _members = new(() => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0)
            .Select(property => ForProperty(property, type)).ToArray());

        internal Member[] Members => _members.Value;
    }

    private static readonly ConditionalWeakTable<Assembly, FrozenDictionary<Type, Lazy<TypePlan>>> Registered = new();
    private static readonly ConditionalWeakTable<Type, Lazy<TypePlan>> Runtime = new();

    // Reuse DI's existing discovery. Registration never constructs attributes or invokes model getters.
    internal static void Register(Assembly assembly, Type[] types) => Registered.GetValue(assembly, _ =>
        types.Where(type => !type.ContainsGenericParameters).ToFrozenDictionary(type => type,
            type => Runtime.TryGetValue(type, out var existing) ? existing : Create(type)));

    internal static TypePlan For(Type type) =>
        Registered.TryGetValue(type.Assembly, out var known) && known.TryGetValue(type, out var plan)
            ? plan.Value : Runtime.GetValue(type, static value => Create(value)).Value;

    private static Lazy<TypePlan> Create(Type type) => new(() => new TypePlan(type));

    internal static bool IsClassified(MemberInfo member) => member switch
    {
        Type type => For(Nullable.GetUnderlyingType(type) ?? type).Classified,
        PropertyInfo property => HasClassification(property) || IsClassified(property.PropertyType),
        _ => HasClassification(member)
    };

    internal static Member ForProperty(PropertyInfo property, Type runtimeType)
    {
        var implementation = ResolveOverride(property, runtimeType);
        var declaration = property.DeclaringType!.IsInterface ? property : implementation;
        var ignored = declaration.IgnoredLog() || implementation.IgnoredLog();
        var classified = IsClassified(declaration) || IsClassified(implementation);
        var getter = implementation.GetMethod!;
        if (!runtimeType.IsInterface)
            foreach (var contract in runtimeType.GetInterfaces())
            {
                // Arrays expose generic interfaces, but reflection cannot map their implementations.
                if (runtimeType.IsArray && contract.IsGenericType) continue;
                var map = runtimeType.GetInterfaceMap(contract);
                foreach (var candidate in contract.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (candidate.GetMethod is not { } contractGetter) continue;
                    var index = Array.IndexOf(map.InterfaceMethods, contractGetter);
                    if (index < 0 || map.TargetMethods[index].GetBaseDefinition() != getter.GetBaseDefinition()) continue;
                    ignored |= candidate.IgnoredLog();
                    classified |= IsClassified(candidate);
                }
            }

        // An interface exemption cannot waive a concrete declaration's name rules.
        var exempt = implementation.GetCustomAttributes<SensitiveAttribute>(false).Any(attribute =>
            attribute.GetType() == typeof(SensitiveAttribute) && !attribute.IsSensitive);
        return new Member(property, ignored, classified, exempt);
    }

    private static PropertyInfo ResolveOverride(PropertyInfo property, Type runtimeType)
    {
        if (property.DeclaringType is { IsInterface: true } contract && !runtimeType.IsInterface)
        {
            var map = runtimeType.GetInterfaceMap(contract);
            var index = Array.IndexOf(map.InterfaceMethods, property.GetMethod);
            var target = map.TargetMethods[index];
            var implementation = target.DeclaringType!
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Single(candidate => candidate.GetMethod?.MethodHandle == target.MethodHandle);
            return implementation.DeclaringType!.IsInterface ? implementation : ResolveOverride(implementation, runtimeType);
        }
        if (property.GetMethod is not { IsVirtual: true } getter) return property;
        var definition = getter.GetBaseDefinition();
        for (var type = runtimeType; type != null && type != property.DeclaringType; type = type.BaseType)
            foreach (var candidate in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (candidate.GetMethod?.GetBaseDefinition() == definition) return candidate;
        return property;
    }

    // Inspect ancestry explicitly so Sensitive(false) cannot hide a positive base annotation.
    private static bool HasClassification(MemberInfo member, bool inherited = false)
    {
        if (member.GetCustomAttributes<DataClassificationAttribute>(false).Any(attribute =>
                (!inherited || attribute.GetType().GetCustomAttribute<AttributeUsageAttribute>(true)?.Inherited != false) &&
                (attribute.Classification != DataClassification.None ||
                 member is not PropertyInfo && attribute is SensitiveAttribute { IsSensitive: false }))) return true;
        if (member is Type type) return type.BaseType != null && HasClassification(type.BaseType, true);
        if (member is not PropertyInfo property || property.GetMethod is not { IsVirtual: true } getter) return false;
        var definition = getter.GetBaseDefinition();
        for (var parent = property.DeclaringType?.BaseType; parent != null; parent = parent.BaseType)
            foreach (var candidate in parent.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (candidate.GetMethod?.GetBaseDefinition() == definition && HasClassification(candidate, true)) return true;
        return false;
    }
}
