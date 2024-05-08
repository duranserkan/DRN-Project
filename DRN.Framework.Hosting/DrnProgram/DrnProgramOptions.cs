namespace DRN.Framework.Hosting.DrnProgram;

public class DrnProgramOptions
{
    public bool UseHttpRequestLogger { get; set; }
    public DrnAppBuilderType AppBuilderType { get; set; } = DrnAppBuilderType.DrnDefaults;
}