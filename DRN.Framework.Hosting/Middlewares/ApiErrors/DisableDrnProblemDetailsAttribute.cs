namespace DRN.Framework.Hosting.Middlewares.ApiErrors;

/// <summary>
/// Disables DRN Problem Details formatting and automatic Swagger error metadata for a controller or action.
/// ASP.NET Core and application-owned error responses remain available.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class DisableDrnProblemDetailsAttribute : Attribute;
