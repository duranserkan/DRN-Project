using Microsoft.CodeAnalysis.CodeActions;

namespace DRN.Test.Analyzer.Tests;

internal static class CodeActionTestExtensions
{
    internal static async Task<Solution> GetChangedSolutionAsync(this CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        return operations.OfType<ApplyChangesOperation>().Should().ContainSingle().Which.ChangedSolution;
    }
}
