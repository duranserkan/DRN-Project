using System.Security.Claims;

namespace DRN.Framework.Utils.Auth;

/// <summary>
/// Represents verified proof that an authentication scheme exemption was satisfied for the scoped request.
/// </summary>
public sealed record ExemptionProof(string Scheme, ClaimsPrincipal Principal);
