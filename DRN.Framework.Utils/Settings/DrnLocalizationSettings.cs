using System.ComponentModel.DataAnnotations;
using System.Globalization;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Settings;

[Config(validateAnnotations: true, errorOnUnknownConfiguration: false)]
public class DrnLocalizationSettings: IValidatableObject
{
    /// <summary>
    /// As a best practice, always define language only cultures so that Regions specific cultures can fall back to neutral region when not available.
    /// </summary>
    public string[] SupportedCultures { get; private set; } = [];

    public string DefaultCulture { get; private set; } = "en";

    /// <summary>
    /// Generates base language codes from regional culture codes (e.g., "de" from "de-CH")
    /// </summary>
    public bool EnsureBaseLanguage { get; init; } = true;
    
    public void ValidateCultures()
    {
        var validCultures = new List<string>();
        foreach (var cultureName in SupportedCultures)
            if (IsValidCulture(cultureName, out _))
                validCultures.Add(cultureName);

        DefaultCulture = !IsValidCulture(DefaultCulture, out _) ? "en" : DefaultCulture;
        SupportedCultures = EnsureBaseLanguage
            ? EnsureBaseLanguages(validCultures).Distinct().ToArray()
            : validCultures.Distinct().ToArray();
    }

    private static bool IsValidCulture(string cultureName, out CultureInfo? cultureInfo)
    {
        cultureInfo = null;

        if (string.IsNullOrWhiteSpace(cultureName))
            return false;

        try
        {
            cultureInfo = CultureInfo.GetCultureInfo(cultureName);
            return cultureInfo.Name.Length > 0;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static string[] EnsureBaseLanguages(IReadOnlyList<string> cultures)
    {
        var cultureSet = new HashSet<string>(cultures);
        var result = new HashSet<string>(cultures);

        foreach (var culture in cultures)
        {
            if (!culture.Contains('-')) continue;

            var baseLanguage = culture.Split('-')[0];
            if (IsValidCulture(baseLanguage, out _))
                result.Add(baseLanguage);
        }

        return cultureSet.Union(result).ToArray();
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ValidateCultures();
        return [];
    }
}