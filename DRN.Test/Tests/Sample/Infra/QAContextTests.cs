using Sample.Domain.QA.Questions;
using Sample.Infra;
using Sample.Infra.Repositories.QA;
using Sample.Infra.Repositories.QB;

namespace DRN.Test.Tests.Sample.Infra;

public class QAContextTests
{
    [Theory]
    [DataInline]
    public async Task QAContext_Should_Add_And_Remove_Question(TestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.StartPostgresAsync();
        var qaContext = context.GetRequiredService<QAContext>();
        _ = context.GetRequiredService<QBContext>(); //to make sure multiple contexts can run side by side

        var title = "Is this magic?";
        var body = "Yes, it is.";
        var question = new Question(title, body);

        qaContext.Questions.Add(question);
        await qaContext.SaveChangesAsync();
        question.Id.Should().BePositive();

        qaContext.Questions.Remove(question);
        await qaContext.SaveChangesAsync();

        var retrievedQuestion = await qaContext.Questions.FindAsync(question.Id);
        retrievedQuestion.Should().BeNull();
    }
}