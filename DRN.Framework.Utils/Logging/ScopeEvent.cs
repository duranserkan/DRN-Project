using Microsoft.Extensions.Logging;

namespace DRN.Framework.Utils.Logging;

/// <summary>A structured event within a logging scope. EventId is provided by Microsoft.Extensions.Logging.</summary>
/// <remarks>WithEvent retains one primary and up to eight additional events. Retained contents bypass sanitization
/// and byte limits. Further events are omitted and reported by OmittedEventCount only when nonzero.</remarks>
public sealed record ScopeEvent(EventId Id, string? Outcome = null, string? Reason = null);
