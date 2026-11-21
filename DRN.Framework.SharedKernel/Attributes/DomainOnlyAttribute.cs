namespace DRN.Framework.SharedKernel.Attributes;

/// <summary>
/// Marks an enum declared in a Domain project as intentionally private to the domain boundary.
/// Domain-only enums must not be exposed through public DTOs, requests, responses, or HTTP endpoints.
/// </summary>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class DomainOnlyAttribute : Attribute;
