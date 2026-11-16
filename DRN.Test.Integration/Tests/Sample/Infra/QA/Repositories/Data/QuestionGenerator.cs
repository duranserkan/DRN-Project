using Category = Sample.Domain.QA.Category.CategoryEntity;
using Question = Sample.Domain.QA.Question.QuestionEntity;
using User = Sample.Domain.QA.User.UserEntity;

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
