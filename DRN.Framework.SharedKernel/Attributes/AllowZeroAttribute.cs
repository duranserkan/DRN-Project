namespace DRN.Framework.SharedKernel.Attributes;

/// <summary>
/// Opts a Contract or Domain enum into defining a named zero member under DRN's compile-time enum conventions.
/// Individual operations still decide whether that zero value is permitted for a given request or command.
/// </summary>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class AllowZeroAttribute : Attribute;
