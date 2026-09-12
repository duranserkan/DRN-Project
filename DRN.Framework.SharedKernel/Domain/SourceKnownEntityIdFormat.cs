namespace DRN.Framework.SharedKernel.Domain;

/// <summary>Selects the accepted representation when parsing or validating an entity ID.</summary>
public enum SourceKnownEntityIdFormat
{
    /// <summary>Decrypt and verify; do not attempt plain parsing.</summary>
    Secure = 0,

    /// <summary>Verify plain input; do not attempt decryption.</summary>
    Plain = 1,

    /// <summary>Try plain verification, then decryption, for each configured key.</summary>
    Auto = 2
}
