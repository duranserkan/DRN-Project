using DRN.Framework.Utils.Scope;

namespace DRN.Framework.Hosting.Consent;

public static class ConsentContext
{
    public static ConsentCookie ConsentCookie =>
        ScopeContext.Data.GetParameter<ConsentCookie>(nameof(Consent.ConsentCookie)) ?? new ConsentCookie(string.Empty, null);

    public static string CookieName => ConsentCookie.Name;
}