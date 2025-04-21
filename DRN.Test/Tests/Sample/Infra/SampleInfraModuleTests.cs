using Sample.Infra;

namespace DRN.Test.Tests.Sample.Infra;

public class SampleInfraModuleTests
{
    [Theory]
    [DataInline]
    public async Task AddSampleInfraServices_ShouldRegisterRequiredServices(TestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.BindExternalDependenciesAsync();
        context.ValidateServices();
    }
}