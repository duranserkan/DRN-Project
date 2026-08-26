using System.Security.Cryptography;
using System.Xml.Linq;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Sample.Infra.DataProtection;

namespace DRN.Test.Unit.Tests.Sample.Infra.DataProtection;

public class SampleXmlEncryptorTests
{
    [Fact]
    public void Encrypt_And_Decrypt_Should_RoundTrip_Xml_Element()
    {
        var features = new DrnAppFeatures { SeedKey = "SampleSecretSeedForTesting1234567890" };
        var securitySettings = new AppSecuritySettings(features);
        var encryptor = new SampleXmlEncryptor(securitySettings);

        var originalXml = new XElement("key",
            new XAttribute("id", Guid.NewGuid().ToString()),
            new XAttribute("version", "1"),
            new XElement("creationDate", DateTime.UtcNow.ToString("O")),
            new XElement("descriptor",
                new XElement("masterKey",
                    new XElement("value", Convert.ToBase64String(new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 })))));

        var encryptedInfo = encryptor.Encrypt(originalXml);

        encryptedInfo.Should().NotBeNull();
        encryptedInfo.DecryptorType.Should().Be(typeof(SampleXmlEncryptor));
        encryptedInfo.EncryptedElement.Should().NotBeNull();
        encryptedInfo.EncryptedElement.Name.LocalName.Should().Be("encryptedKey");
        encryptedInfo.EncryptedElement.Attribute("nonce").Should().NotBeNull();
        encryptedInfo.EncryptedElement.Attribute("tag").Should().NotBeNull();
        encryptedInfo.EncryptedElement.Value.Should().NotBeNullOrWhiteSpace();

        // Plaintext XML content must not appear in the encrypted payload
        encryptedInfo.EncryptedElement.ToString().Should().NotContain("masterKey");
        encryptedInfo.EncryptedElement.ToString().Should().NotContain("creationDate");

        var decryptedXml = encryptor.Decrypt(encryptedInfo.EncryptedElement);

        decryptedXml.Should().NotBeNull();
        decryptedXml.Name.LocalName.Should().Be("key");
        decryptedXml.ToString().Should().Be(originalXml.ToString());
    }

    [Fact]
    public void Constructor_With_IServiceProvider_Should_Activate_And_Decrypt_Xml_Element()
    {
        var features = new DrnAppFeatures { SeedKey = "SampleSecretSeedForTesting1234567890" };
        var securitySettings = new AppSecuritySettings(features);
        var services = new ServiceCollection();
        services.AddSingleton<IAppSecuritySettings>(securitySettings);
        var serviceProvider = services.BuildServiceProvider();

        // Simulate ASP.NET Core DataProtection reflection activation of IXmlDecryptor (.ctor(IServiceProvider))
        var decryptorInstance = Activator.CreateInstance(typeof(SampleXmlEncryptor), serviceProvider) as IXmlDecryptor;
        decryptorInstance.Should().NotBeNull();

        var encryptor = new SampleXmlEncryptor(securitySettings);
        var originalXml = new XElement("keyPayload", "SecretActivationData");
        var encryptedInfo = encryptor.Encrypt(originalXml);

        var decryptedXml = decryptorInstance!.Decrypt(encryptedInfo.EncryptedElement);
        decryptedXml.Should().NotBeNull();
        decryptedXml.ToString().Should().Be(originalXml.ToString());
    }

    [Fact]
    public void Constructor_With_Null_IServiceProvider_Should_Throw()
    {
        var act = () => new SampleXmlEncryptor((IServiceProvider)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_With_IServiceProvider_Missing_IAppSecuritySettings_Should_Throw()
    {
        var emptyServices = new ServiceCollection().BuildServiceProvider();
        var act = () => new SampleXmlEncryptor(emptyServices);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DataProtection_PersistAndReload_Should_Decrypt_Using_IServiceProvider_Activation()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "drn-dataprotection-test-" + Guid.NewGuid());
        var directoryInfo = new DirectoryInfo(tempDirectory);
        try
        {
            var features = new DrnAppFeatures { SeedKey = "SampleSecretSeedForTesting1234567890" };
            var securitySettings = new AppSecuritySettings(features);

            // 1. Initial container: creates key, encrypts via SampleXmlEncryptor, persists XML to directory
            var services1 = new ServiceCollection();
            services1.AddSingleton<IAppSecuritySettings>(securitySettings);
            services1.AddDataProtection()
                .PersistKeysToFileSystem(directoryInfo)
                .SetApplicationName("DataProtectionTestApp")
                .AddKeyManagementOptions(options => options.XmlEncryptor = new SampleXmlEncryptor(securitySettings));

            var provider1 = services1.BuildServiceProvider();
            var dataProtector1 = provider1.GetRequiredService<IDataProtectionProvider>().CreateProtector("TestPurpose");
            var protectedPayload = dataProtector1.Protect("SensitiveUserData");

            // Verify persisted XML contains decryptorType pointing to SampleXmlEncryptor
            var persistedFiles = directoryInfo.GetFiles("*.xml");
            persistedFiles.Should().NotBeEmpty();
            var persistedXml = XDocument.Load(persistedFiles[0].FullName);
            persistedXml.ToString().Should().Contain(typeof(SampleXmlEncryptor).FullName!);

            // 2. Fresh container (simulating application restart): reloads persisted key from storage and activates SampleXmlEncryptor(IServiceProvider)
            var services2 = new ServiceCollection();
            services2.AddSingleton<IAppSecuritySettings>(securitySettings);
            services2.AddDataProtection()
                .PersistKeysToFileSystem(directoryInfo)
                .SetApplicationName("DataProtectionTestApp")
                .AddKeyManagementOptions(options => options.XmlEncryptor = new SampleXmlEncryptor(securitySettings));

            var provider2 = services2.BuildServiceProvider();
            var dataProtector2 = provider2.GetRequiredService<IDataProtectionProvider>().CreateProtector("TestPurpose");
            var unprotectedPayload = dataProtector2.Unprotect(protectedPayload);

            unprotectedPayload.Should().Be("SensitiveUserData");
        }
        finally
        {
            if (directoryInfo.Exists)
            {
                directoryInfo.Delete(recursive: true);
            }
        }
    }

    [Fact]
    public void Decrypt_Should_Throw_When_Ciphertext_Is_Tampered()
    {
        var features = new DrnAppFeatures { SeedKey = "SampleSecretSeedForTesting1234567890" };
        var securitySettings = new AppSecuritySettings(features);
        var encryptor = new SampleXmlEncryptor(securitySettings);

        var originalXml = new XElement("secretData", "Confidential Payload");
        var encryptedInfo = encryptor.Encrypt(originalXml);

        var corruptedCiphertext = Convert.ToBase64String(new byte[] { 0, 1, 2, 3, 4, 5 });
        var tamperedElement = new XElement("encryptedKey",
            encryptedInfo.EncryptedElement.Attribute("nonce"),
            encryptedInfo.EncryptedElement.Attribute("tag"),
            new XCData(corruptedCiphertext));

        var act = () => encryptor.Decrypt(tamperedElement);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_Should_Throw_When_Decrypted_With_Different_Key()
    {
        var features1 = new DrnAppFeatures { SeedKey = "SeedKeyOne_1234567890" };
        var encryptor1 = new SampleXmlEncryptor(new AppSecuritySettings(features1));

        var features2 = new DrnAppFeatures { SeedKey = "SeedKeyTwo_1234567890" };
        var decryptor2 = new SampleXmlEncryptor(new AppSecuritySettings(features2));

        var originalXml = new XElement("sensitive", "TopSecret");
        var encryptedInfo = encryptor1.Encrypt(originalXml);

        var act = () => decryptor2.Decrypt(encryptedInfo.EncryptedElement);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Encrypt_And_Decrypt_Should_Throw_On_Null()
    {
        var features = new DrnAppFeatures();
        var encryptor = new SampleXmlEncryptor(new AppSecuritySettings(features));

        var encryptNull = () => encryptor.Encrypt(null!);
        encryptNull.Should().Throw<ArgumentNullException>();

        var decryptNull = () => encryptor.Decrypt(null!);
        decryptNull.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Reusable_Instance_Should_Support_Multiple_Operations()
    {
        var features = new DrnAppFeatures { SeedKey = "MultiOperationTestSeed_1234567890" };
        using var encryptor = new SampleXmlEncryptor(new AppSecuritySettings(features));

        for (var i = 0; i < 10; i++)
        {
            var element = new XElement("item", new XAttribute("index", i), $"payload-{i}");
            var encrypted = encryptor.Encrypt(element);
            var decrypted = encryptor.Decrypt(encrypted.EncryptedElement);

            decrypted.ToString().Should().Be(element.ToString());
        }
    }
}
