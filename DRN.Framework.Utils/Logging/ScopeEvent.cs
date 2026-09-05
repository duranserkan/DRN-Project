using Microsoft.Extensions.Logging;

namespace DRN.Framework.Utils.Logging;

/// <summary>A structured event within a logging scope. EventId is provided by Microsoft.Extensions.Logging.</summary>
public sealed record ScopeEvent(EventId Id, string? Outcome = null, string? Reason = null);
