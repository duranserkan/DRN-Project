namespace DRN.Framework.Hosting.DrnProgram;

public class DrnProgramOptions
{
    public bool UseHttpRequestLogger { get; init; } = true;
    public DrnAppBuilderType AppBuilderType { get; init; } = DrnAppBuilderType.DrnDefaults;
}