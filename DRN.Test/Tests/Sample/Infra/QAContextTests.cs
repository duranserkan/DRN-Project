using DRN.Framework.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Sample.Domain;
using Sample.Domain.QA.Categories;
using Sample.Domain.QA.Questions;
using Sample.Domain.Users;
using Sample.Infra;
using Sample.Infra.QA;
using Sample.Infra.QB;

namespace DRN.Test.Tests.Sample.Infra;

public class QAContextTests
{
    [Theory]
    [DataInline]
    public async Task QAContext_Should_Add_And_Remove_Question(TestContext context)
    {
        byte heroCount = 33;
        byte destinationId = 12;
        context.AddToConfiguration(new { NexusAppSettings = new NexusAppSettings { AppId = heroCount, AppInstanceId = destinationId } });
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();

        var appSettings = context.GetRequiredService<IAppSettings>();
        var qaContext = context.GetRequiredService<QAContext>();
        _ = context.GetRequiredService<QBContext>(); //to make sure multiple contexts can run side by side

        var category = new Category("dotnet");
        category.SetExtendedProperties(new CustomProperties(true));

        var beforeCategoryCreation = DateTimeOffset.UtcNow;
        await Task.Delay(TimeSpan.FromSeconds(1.2));

        qaContext.Categories.Add(category);
        await qaContext.SaveChangesAsync();

        category.Id.Should().BeNegative();
        category.EntityId.Should().NotBe(Guid.Empty);
        category.EntityIdSource.Valid.Should().BeTrue();
        category.EntityIdSource.EntityTypeId.Should().Be((byte)SampleEntityTypeIds.Category);
        category.EntityIdSource.Source.Id.Should().Be(category.Id);
        category.EntityIdSource.Source.AppId.Should().Be(appSettings.NexusAppSettings.AppId);
        category.EntityIdSource.Source.AppInstanceId.Should().Be(appSettings.NexusAppSettings.AppInstanceId);
        category.CreatedAt.Should().BeAfter(beforeCategoryCreation);
        category.ModifiedAt.Should().BeAfter(beforeCategoryCreation);
        category.CreatedAt.Should().Be(category.ModifiedAt);

        var categoryInitialCreatedAt = category.CreatedAt;
        var categoryInitialModifiedAt = category.ModifiedAt;


        var address = new Address("Victory street", "Ankara", "Türkiye", "001");
        var contact = new ContactDetail("drn@framework.com");
        var user = new User("Duran Serkan", "KILIÇ", "gg", contact, address);
        qaContext.Users.Add(user);
        await qaContext.SaveChangesAsync();

        var title = "Is this magic?";
        var body = "Yes, it is.";
        var question = new Question(title, body, user, category);

        qaContext.Questions.Add(question);
        await qaContext.SaveChangesAsync();
        question.Id.Should().BeNegative();

        qaContext.Questions.Remove(question);
        await qaContext.SaveChangesAsync();

        var retrievedQuestion = await qaContext.Questions.FindAsync(question.Id);
        retrievedQuestion.Should().BeNull();

        category = await qaContext.Categories.FindAsync(category.Id);
        var custom = category!.GetExtendedProperties<CustomProperties>();
        custom.LifeIsGood.Should().BeTrue();

        category.SetExtendedProperties(new NewProperties("Good"));
        await qaContext.SaveChangesAsync();

        category = await qaContext.Categories.FindAsync(category.Id);
        category!.GetExtendedProperties<NewProperties>().LifeIs.Should().Be("Good");

        category.CreatedAt.Should().Be(categoryInitialCreatedAt);
        category.ModifiedAt.Should().BeAfter(categoryInitialModifiedAt);

        var now= DateTimeOffset.UtcNow;
        categoryInitialModifiedAt.Should().BeBefore(now);

        var task = async () =>
        {
            category.ExtendedProperties = "not a json";
            await qaContext.SaveChangesAsync();
        };

        await task.Should().ThrowAsync<DbUpdateException>();
    }

    public record struct CustomProperties(bool LifeIsGood);

    public record struct NewProperties(string LifeIs);
}