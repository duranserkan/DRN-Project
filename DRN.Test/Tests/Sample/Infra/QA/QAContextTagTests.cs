using Microsoft.EntityFrameworkCore;
using Sample.Domain.QA.Tags;
using Sample.Infra;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Sample.Infra.QA;

public class QAContextTagTests
{
    [Theory]
    [DataInline]
    public async Task QAContext_Should_Have_Tag(TestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();
        var qaContext = context.GetRequiredService<QAContext>();

        var firstTag = new Tag("firstTag")
        {
            Model = new TagValueModel { BoolValue = true, StringValue = "firstTagValue" }
        };
        
        var secondTag = new Tag("secondTag")
        {
            Model = new TagValueModel { BoolValue = false, StringValue = "secondTagValue" }
        };

        qaContext.Tags.Add(firstTag);
        qaContext.Tags.Add(secondTag);
        await qaContext.SaveChangesAsync();

        var tagFromDb = await qaContext.Tags.FindAsync(firstTag.Id);
        tagFromDb.Should().NotBeNull();
        tagFromDb.Name.Should().Be(firstTag.Name);
        tagFromDb.Model.Should().BeEquivalentTo(firstTag.Model);
        
        var tagFromDb2 = await qaContext.Tags.FindAsync(secondTag.Id);
        tagFromDb2.Should().NotBeNull();
        tagFromDb2.Name.Should().Be(secondTag.Name);
        tagFromDb2.Model.Should().BeEquivalentTo(secondTag.Model);

        var modelBool1Query = qaContext.Tags.Where(t => t.Model.BoolValue == true);
        var modelBool2Query = qaContext.Tags.Where(t => t.Model.BoolValue == false);
        var modelString1Query = qaContext.Tags.Where(t => t.Model.StringValue == firstTag.Model.StringValue);
        var modelString2Query = qaContext.Tags.Where(t => t.Model.StringValue == secondTag.Model.StringValue);
        
        var sqlQueries = new[] { modelBool1Query.ToQueryString(), modelBool2Query.ToQueryString(), modelString1Query.ToQueryString(), modelString2Query.ToQueryString() };
        sqlQueries.Distinct().Count().Should().Be(4);
        
        var tagFromBool1Query = await modelBool1Query.SingleAsync();
        tagFromBool1Query.Model.Should().BeEquivalentTo(firstTag.Model);

        var tagFromBool2Query = await modelBool2Query.SingleAsync();
        tagFromBool2Query.Model.Should().BeEquivalentTo(secondTag.Model);
        
        var tagFromString1Query = await modelString1Query.SingleAsync();
        tagFromString1Query.Model.Should().BeEquivalentTo(firstTag.Model);

        var tagFromString2Query = await modelString2Query.SingleAsync();
        tagFromString2Query.Model.Should().BeEquivalentTo(secondTag.Model);
    }
}