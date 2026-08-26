using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using DRN.Framework.Utils.Data.Encryption;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.Utils.DataProtection;

// https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/extensibility/key-management#ixmldecryptor
// ASP.NET Core Data Protection requires IXmlDecryptor implementations to provide either a public .ctor(IServiceProvider)
// or a public parameterless .ctor() so that persisted XML keys can be activated dynamically across application restarts.
public sealed class SampleXmlEncryptor : AesGcmEncryptorBase, IXmlEncryptor, IXmlDecryptor
{
    internal SampleXmlEncryptor(IAppSecuritySettings securitySettings)
        : base(securitySettings)
    {
    }

    public SampleXmlEncryptor(IServiceProvider services)
        : this((services ?? throw new ArgumentNullException(nameof(services))).GetRequiredService<IAppSecuritySettings>())
    {
    }

    protected override string Context => "Sample.Utils DataProtection SampleXmlEncryptor 2026-08-26 v1";

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        try
        {
            var encrypted = Encrypt(plaintextBytes);

            var encryptedElement = new XElement("encryptedKey",
                new XAttribute("nonce", Convert.ToBase64String(encrypted.Nonce)),
                new XAttribute("tag", Convert.ToBase64String(encrypted.Tag)),
                new XCData(Convert.ToBase64String(encrypted.Ciphertext)));

            return new EncryptedXmlInfo(encryptedElement, typeof(SampleXmlEncryptor));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        var nonceAttribute = encryptedElement.Attribute("nonce")?.Value
            ?? throw new InvalidOperationException("Encrypted DataProtection key element is missing the required 'nonce' attribute.");
        var tagAttribute = encryptedElement.Attribute("tag")?.Value
            ?? throw new InvalidOperationException("Encrypted DataProtection key element is missing the required 'tag' attribute.");

        var nonce = Convert.FromBase64String(nonceAttribute);
        var tag = Convert.FromBase64String(tagAttribute);
        var ciphertext = Convert.FromBase64String(encryptedElement.Value);

        var plaintextBytes = Decrypt(nonce, ciphertext, tag);
        try
        {
            return XElement.Parse(Encoding.UTF8.GetString(plaintextBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }
}
