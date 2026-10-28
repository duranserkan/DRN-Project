using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Scope;

namespace DRN.Test.Unit.Tests.Framework.Utils.Scope;

public class ScopeProblemTests
{
    [Fact]
    public void Helpers_Should_Preserve_Diagnostics_Public_Defaults_And_Caller_Stack()
    {
        var problem = new ScopeProblem
        {
            Type = "https://example.com/problem", Title = "Public title",
            Detail = "Public detail", Instance = "/public-path"
        };
        problem.Extensions["code"] = "public_code";
        var inner = new InvalidOperationException("Private cause");
        (Action<string, Exception?, string?> Throw, Func<string, Exception?, string?, DrnException> Create)[] cases =
        [
            (problem.Validation, ExceptionFor.Validation),
            (problem.Unauthorized, ExceptionFor.Unauthorized),
            (problem.Forbidden, ExceptionFor.Forbidden),
            (problem.NotFound, ExceptionFor.NotFound),
            (problem.Conflict, ExceptionFor.Conflict),
            (problem.Expired, ExceptionFor.Expired),
            (problem.Configuration, ExceptionFor.Configuration),
            (problem.UnprocessableEntity, ExceptionFor.UnprocessableEntity),
            (problem.MaliciousRequest, ExceptionFor.MaliciousRequest),
            (problem.Jackpot, ExceptionFor.Jackpot)
        ];

        foreach (var (throwException, createException) in cases)
        foreach (var category in new[] { "diagnostic_category", null })
        {
            var expected = createException("Private message", inner, category);
            expected.StackTrace.Should().BeNull();
            Action act = () => throwException("Private message", inner, category);
            var actual = act.Should().Throw<DrnException>().Which;
            actual.GetType().Should().Be(expected.GetType());
            actual.Status.Should().Be(expected.Status);
            actual.Message.Should().Be(expected.Message);
            actual.Category.Should().Be(expected.Category);
            actual.InnerException.Should().BeSameAs(inner);
            actual.StackTrace.Should().Contain(nameof(Helpers_Should_Preserve_Diagnostics_Public_Defaults_And_Caller_Stack))
                .And.NotContain("DRN.Framework.Utils.Scope.ScopeProblem.")
                .And.NotContain("DRN.Framework.SharedKernel.ExceptionFor.");
            problem.Type.Should().Be("https://example.com/problem");
            problem.Title.Should().Be("Public title");
            problem.Detail.Should().Be("Public detail");
            problem.Instance.Should().Be("/public-path");
            problem.Extensions.Should().ContainSingle().Which.Key.Should().Be("code");
            problem.Extensions["code"].Should().Be("public_code");
        }
    }
}
