namespace DRN.Framework.Hosting.DrnProgram;

public class DrnProgramOptions
{
    public bool UseHttpRequestLogger { get; set; } = false;
    public DrnAppBuilderType AppBuilderType { get; set; } = DrnAppBuilderType.DrnDefaults;
}