using Sample.Infra.Repositories.QA;

namespace DRN.Test.Tests.Sample.Infra;

public class QAContextFactoryTest
{
    [Theory]
    [DataInline]
    public void QAContext_Factory_Should_Create_QAContext()
    {
        var factory=new QAContextFactory();
        var qaContext = factory.CreateDbContext(Array.Empty<string>());
        qaContext.Should().NotBeNull();
    }
}