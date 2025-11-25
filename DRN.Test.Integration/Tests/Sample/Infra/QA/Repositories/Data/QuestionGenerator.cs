using Sample.Domain.QA.Categories;
using Sample.Domain.QA.Questions;
using Sample.Domain.Users;

namespace DRN.Test.Integration.Tests.Sample.Infra.QA.Repositories.Data;

public static class QuestionGenerator
{
    public static Question New(string prefix, string suffix, User? user = null, Category? category = null)
    {
        user ??= UserGenerator.New(prefix, suffix);
        category ??= CategoryGenerator.New(prefix, suffix);
        return new Question($"{prefix}_{suffix}_title", $"{prefix}_{suffix}_body", user, category);
    }
}