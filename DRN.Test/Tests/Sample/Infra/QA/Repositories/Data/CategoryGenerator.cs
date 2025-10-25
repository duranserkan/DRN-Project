using Sample.Domain.QA.Categories;

namespace DRN.Test.Tests.Sample.Infra.QA.Repositories.Data;

public class CategoryGenerator
{
    public static Category New(string prefix, string suffix) => new($"{prefix}_{suffix}_name");
}