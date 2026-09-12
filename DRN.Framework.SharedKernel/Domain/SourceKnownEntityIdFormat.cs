namespace DRN.Framework.SharedKernel.Domain;

/// <summary>Selects the accepted representation when parsing or validating a GUID entity ID.</summary>
public enum SourceKnownEntityIdFormat
{
    /// <summary>Use the immutable secure/plain default configured in ID operations.</summary>
    ConfiguredDefault = 0,

    /// <summary>Decrypt and verify; do not attempt plain parsing.</summary>
    Secure = 1,

    /// <summary>Verify plain input; do not attempt decryption.</summary>
    Plain = 2,

    /// <summary>Try plain verification, then decryption, for each configured key.</summary>
    Auto = 3
}
