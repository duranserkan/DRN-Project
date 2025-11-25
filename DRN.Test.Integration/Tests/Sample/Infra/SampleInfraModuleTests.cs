using Sample.Infra;

namespace DRN.Test.Integration.Tests.Sample.Infra;

public class SampleInfraModuleTests
{
    [Theory]
    [DataInline]
    public async Task AddSampleInfraServices_ShouldRegisterRequiredServices(DrnTestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.BindExternalDependenciesAsync();
        await context.ValidateServicesAsync();
    }
}