using DRN.Framework.Utils.Entity;
using Microsoft.EntityFrameworkCore;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Questions;
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
        var dateTimeUtils = context.GetRequiredService<IEntityDateTimeUtils>();
        var tagPrefix = $"{nameof(QAContext_Should_Have_Tag)}_{Guid.NewGuid():N}";
        var tagQuery = qaContext.Tags.Where(t => t.Name.StartsWith(tagPrefix));

        var firstTag = new Tag($"{tagPrefix}_firstTag")
        {
            Model = new TagValueModel
            {
                BoolValue = true,
                StringValue = "firstTagValue",
                Max = long.MaxValue,
                Min = long.MinValue,
                Other = 0,
                Type = TagType.System
            }
        };

        var secondTag = new Tag($"{tagPrefix}_secondTag")
        {
            Model = new TagValueModel
            {
                BoolValue = false,
                StringValue = "secondTagValue",
                Max = int.MaxValue,
                Min = int.MinValue,
                Other = uint.MaxValue,
                Type = TagType.User
            }
        };

        var beforeTagCreation = DateTimeOffset.UtcNow;
        await Task.Delay(TimeSpan.FromSeconds(1.2));

        qaContext.Tags.Add(firstTag);
        qaContext.Tags.Add(secondTag);
        await qaContext.SaveChangesAsync();

        await Task.Delay(TimeSpan.FromSeconds(1.2));
        var afterTagCreation = DateTimeOffset.UtcNow;

        firstTag.EntityIdSource.HasSameEntityType<Tag>().Should().BeTrue();
        firstTag.EntityIdSource.HasSameEntityType<Question>().Should().BeFalse();

        var tagFromDb = await qaContext.Tags.FindAsync(firstTag.Id);
        tagFromDb.Should().NotBeNull();
        tagFromDb.Name.Should().Be(firstTag.Name);
        tagFromDb.Model.Should().BeEquivalentTo(firstTag.Model);

        var tagFromDb2 = await qaContext.Tags.FindAsync(secondTag.Id);
        tagFromDb2.Should().NotBeNull();
        tagFromDb2.Name.Should().Be(secondTag.Name);
        tagFromDb2.Model.Should().BeEquivalentTo(secondTag.Model);

        var modelBool1Query = tagQuery.Where(t => t.Model.BoolValue == true);
        var modelBool2Query = tagQuery.Where(t => t.Model.BoolValue == false);
        var modelString1Query = tagQuery.Where(t => t.Model.StringValue == firstTag.Model.StringValue);
        var modelString2Query = tagQuery.Where(t => t.Model.StringValue == secondTag.Model.StringValue);

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


        var tagFromBeforeFilter = await dateTimeUtils.CreatedBefore(tagQuery, afterTagCreation).ToArrayAsync();
        var tagFromAfterFilter = await dateTimeUtils.CreatedAfter(tagQuery, beforeTagCreation).ToArrayAsync();
        var tagFromBetweenFilter = await dateTimeUtils.CreatedBetween(tagQuery, beforeTagCreation, afterTagCreation).ToArrayAsync();
        //utils should correct order when order is incorrect
        var tagFromBetweenFilter2 = await dateTimeUtils.CreatedBetween(tagQuery, afterTagCreation, beforeTagCreation).ToArrayAsync();
        var tagFromOutsideFilter = await dateTimeUtils.CreatedOutside(tagQuery, beforeTagCreation, afterTagCreation).ToArrayAsync();

        tagFromBeforeFilter.Length.Should().Be(2);
        tagFromAfterFilter.Length.Should().Be(2);
        tagFromBetweenFilter.Length.Should().Be(2);
        tagFromBetweenFilter2.Length.Should().Be(2);
        tagFromOutsideFilter.Length.Should().Be(0);

        tagFromBeforeFilter = await dateTimeUtils.CreatedBefore(tagQuery, beforeTagCreation).ToArrayAsync();
        tagFromAfterFilter = await dateTimeUtils.CreatedAfter(tagQuery, afterTagCreation).ToArrayAsync();
        tagFromBetweenFilter = await dateTimeUtils.CreatedBetween(tagQuery, beforeTagCreation, beforeTagCreation).ToArrayAsync();
        tagFromBetweenFilter2 = await dateTimeUtils.CreatedBetween(tagQuery, afterTagCreation, afterTagCreation).ToArrayAsync();
        tagFromOutsideFilter = await dateTimeUtils.CreatedOutside(tagQuery, beforeTagCreation, beforeTagCreation).ToArrayAsync();
        var tagFromOutsideFilter2 = await dateTimeUtils.CreatedOutside(tagQuery, afterTagCreation, afterTagCreation).ToArrayAsync();

        tagFromBeforeFilter.Length.Should().Be(0);
        tagFromAfterFilter.Length.Should().Be(0);
        tagFromBetweenFilter.Length.Should().Be(0);
        tagFromBetweenFilter2.Length.Should().Be(0);
        tagFromOutsideFilter.Length.Should().Be(2);
        tagFromOutsideFilter2.Length.Should().Be(2);
    }
}