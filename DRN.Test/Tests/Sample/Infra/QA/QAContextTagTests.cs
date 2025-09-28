using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Framework.Utils.Entity;
using DRN.Test.Tests.Sample.Infra.QA.Repositories;
using Microsoft.EntityFrameworkCore;
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

        var (firstTag, secondTag, _) = TagGenerator.GetTags(tagPrefix);

        var beforeTagCreation = DateTimeOffset.UtcNow;
        await Task.Delay(TimeSpan.FromSeconds(1.2));

        qaContext.Tags.Add(firstTag);
        qaContext.Tags.Add(secondTag);
        await qaContext.SaveChangesAsync();

        await Task.Delay(TimeSpan.FromSeconds(1.2));
        var afterTagCreation = DateTimeOffset.UtcNow;

        firstTag.EntityIdSource.HasSameEntityType<Tag>().Should().BeTrue();
        firstTag.EntityIdSource.HasSameEntityType<Question>().Should().BeFalse();
        
        //generator set by DrnSaveChangesInterceptor
        var foreignId = firstTag.GetForeignId(1, -1);
        foreignId.EntityType.Should().Be(1);
        foreignId.EntityId.Should().NotBeEmpty();
        foreignId.Valid.Should().BeTrue();
        
        foreignId = firstTag.GetForeignId(2, -2);
        foreignId.EntityType.Should().Be(2);
        foreignId.EntityId.Should().NotBeEmpty();
        foreignId.Valid.Should().BeTrue();

        var tagFromDb = await qaContext.Tags.FindAsync(firstTag.Id);
        tagFromDb.Should().Be(firstTag);
        tagFromDb.Name.Should().Be(firstTag.Name);
        tagFromDb.Model.Should().BeEquivalentTo(firstTag.Model);

        var tagFromDb2 = await qaContext.Tags.FindAsync(secondTag.Id);
        tagFromDb2.Should().Be(secondTag);
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
        var tagFromBeforeFilter2 = await dateTimeUtils.Apply(tagQuery, EntityCreatedFilter.Before(afterTagCreation)).ToArrayAsync();
        tagFromBeforeFilter.Length.Should().Be(2);
        tagFromBeforeFilter2.Length.Should().Be(2);

        tagFromBeforeFilter = await dateTimeUtils.CreatedBefore(tagQuery, beforeTagCreation).ToArrayAsync();
        tagFromBeforeFilter2 = await dateTimeUtils.Apply(tagQuery, EntityCreatedFilter.Before(beforeTagCreation)).ToArrayAsync();
        tagFromBeforeFilter.Length.Should().Be(0);
        tagFromBeforeFilter2.Length.Should().Be(0);


        var tagFromAfterFilter = await dateTimeUtils.CreatedAfter(tagQuery, beforeTagCreation).ToArrayAsync();
        var tagFromAfterFilter2 = await dateTimeUtils.Apply(tagQuery, EntityCreatedFilter.After(beforeTagCreation)).ToArrayAsync();
        tagFromAfterFilter.Length.Should().Be(2);
        tagFromAfterFilter2.Length.Should().Be(2);

        tagFromAfterFilter = await dateTimeUtils.CreatedAfter(tagQuery, afterTagCreation).ToArrayAsync();
        tagFromAfterFilter2 = await dateTimeUtils.Apply(tagQuery, EntityCreatedFilter.After(afterTagCreation)).ToArrayAsync();
        tagFromAfterFilter.Length.Should().Be(0);
        tagFromAfterFilter2.Length.Should().Be(0);


        var tagFromBetweenFilter = await dateTimeUtils.CreatedBetween(tagQuery, beforeTagCreation, afterTagCreation).ToArrayAsync();
        //utils should correct order when order is incorrect
        var tagFromBetweenFilter2 = await dateTimeUtils.CreatedBetween(tagQuery, afterTagCreation, beforeTagCreation).ToArrayAsync();
        var tagFromBetweenFilter3 = await dateTimeUtils.Apply(tagQuery, EntityCreatedFilter.Between(beforeTagCreation, afterTagCreation)).ToArrayAsync();
        tagFromBetweenFilter.Length.Should().Be(2);
        tagFromBetweenFilter2.Length.Should().Be(2);
        tagFromBetweenFilter3.Length.Should().Be(2);

        tagFromBetweenFilter = await dateTimeUtils.CreatedBetween(tagQuery, beforeTagCreation, beforeTagCreation).ToArrayAsync();
        tagFromBetweenFilter2 = await dateTimeUtils.CreatedBetween(tagQuery, afterTagCreation, afterTagCreation).ToArrayAsync();
        tagFromBetweenFilter3 = await dateTimeUtils.Apply(tagQuery, EntityCreatedFilter.Between(afterTagCreation, afterTagCreation)).ToArrayAsync();
        tagFromBetweenFilter.Length.Should().Be(0);
        tagFromBetweenFilter2.Length.Should().Be(0);
        tagFromBetweenFilter3.Length.Should().Be(0);


        var tagFromOutsideFilter = await dateTimeUtils.CreatedOutside(tagQuery, beforeTagCreation, afterTagCreation).ToArrayAsync();
        var tagFromOutsideFilter2 = await dateTimeUtils.CreatedOutside(tagQuery, afterTagCreation, beforeTagCreation).ToArrayAsync();
        var tagFromOutsideFilter3 = await dateTimeUtils.Apply(tagQuery, EntityCreatedFilter.Outside(beforeTagCreation, afterTagCreation)).ToArrayAsync();

        tagFromOutsideFilter.Length.Should().Be(0);
        tagFromOutsideFilter2.Length.Should().Be(0);
        tagFromOutsideFilter3.Length.Should().Be(0);

        tagFromOutsideFilter = await dateTimeUtils.CreatedOutside(tagQuery, beforeTagCreation, beforeTagCreation).ToArrayAsync();
        tagFromOutsideFilter2 = await dateTimeUtils.CreatedOutside(tagQuery, afterTagCreation, afterTagCreation).ToArrayAsync();
        tagFromOutsideFilter3 = await dateTimeUtils.Apply(tagQuery, EntityCreatedFilter.Outside(beforeTagCreation, beforeTagCreation)).ToArrayAsync();

        tagFromOutsideFilter.Length.Should().Be(2);
        tagFromOutsideFilter2.Length.Should().Be(2);
        tagFromOutsideFilter3.Length.Should().Be(2);
        
        var serviceProvider = context.GetRequiredService<IServiceProvider>();
        var newScope = serviceProvider.CreateScope();
        var qaContext2 = newScope.ServiceProvider.GetRequiredService<QAContext>();
        var firstTagFromDb = await qaContext2.Tags.FindAsync(firstTag.Id);
        
        //generator set by DrnMaterializationInterceptor
        foreignId = firstTagFromDb!.GetForeignId(3, -3);
        foreignId.EntityType.Should().Be(3);
        foreignId.EntityId.Should().NotBeEmpty();
        foreignId.Valid.Should().BeTrue();
        
        foreignId = firstTagFromDb.GetForeignId(3, -4);
        foreignId.EntityType.Should().Be(3);
        foreignId.EntityId.Should().NotBeEmpty();
        foreignId.Valid.Should().BeTrue();
        
        foreignId = firstTagFromDb.GetForeignId(3, -5);
        foreignId.EntityType.Should().Be(3);
        foreignId.EntityId.Should().NotBeEmpty();
        foreignId.Valid.Should().BeTrue();
        
        //test cache hit
        foreignId = firstTagFromDb.GetForeignId(3, -5);
        foreignId.EntityType.Should().Be(3);
        foreignId.EntityId.Should().NotBeEmpty();
        foreignId.Valid.Should().BeTrue();
    }
}