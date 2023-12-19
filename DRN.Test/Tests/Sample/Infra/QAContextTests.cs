using Sample.Domain.QA.Questions;
using Sample.Infra;
using Sample.Infra.Repositories.QA;

namespace DRN.Test.Tests.Sample.Infra;

public class QAContextTests
{
    [Theory]
    [DataInline]
    public async Task QAContext_Should_Add_And_Remove_Question(TestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        context.StartPostgreSQL();
        var qaContext = context.GetRequiredService<QAContext>();

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