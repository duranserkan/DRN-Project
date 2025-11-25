using Sample.Infra.QA;

namespace DRN.Test.Integration.Tests.Sample.Infra.QA;

public class QAContextFactoryTest
{
    [Fact]
    public void QAContext_Factory_Should_Create_QAContext()
    {
        var factory = new QAContext();
        var qaContext = factory.CreateDbContext(Array.Empty<string>());
        qaContext.Should().NotBeNull();
    }
}