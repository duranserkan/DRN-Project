using Microsoft.EntityFrameworkCore;
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
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.StartPostgresAndApplyMigrationsAsync();
        var qaContext = context.GetRequiredService<QAContext>();
        _ = context.GetRequiredService<QBContext>(); //to make sure multiple contexts can run side by side

        var category = new Category("dotnet");
        qaContext.Categories.Add(category);
        await qaContext.SaveChangesAsync();

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
        question.Id.Should().BePositive();

        qaContext.Questions.Remove(question);
        await qaContext.SaveChangesAsync();

        var retrievedQuestion = await qaContext.Questions.FindAsync(question.Id);
        retrievedQuestion.Should().BeNull();
    }
}