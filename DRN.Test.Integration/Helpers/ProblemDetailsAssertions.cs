using System.Text.Json;

namespace DRN.Test.Integration.Helpers;

internal static class ProblemDetailsAssertions
{
    // The caller owns the returned document. The default instance excludes request queries.
    internal static async Task<JsonDocument> ReadProblemAsync(HttpResponseMessage response, int status, string? expectedInstance = null)
    {
        ((int)response.StatusCode).Should().Be(status);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/problem+json");
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        try
        {
            var root = problem.RootElement;
            root.GetProperty("status").GetInt32().Should().Be(status);
            root.GetProperty("type").GetString().Should().NotBeNullOrWhiteSpace();
            root.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
            root.GetProperty("requestId").GetString().Should().NotBeNullOrWhiteSpace();
            expectedInstance ??= response.RequestMessage!.RequestUri!.AbsolutePath;
            root.GetProperty("instance").GetString().Should().Be(expectedInstance);
            return problem;
        }
        catch
        {
            problem.Dispose();
            throw;
        }
    }
}
