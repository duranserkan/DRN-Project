namespace DRN.Framework.Hosting.DrnProgram;

public class DrnProgramOptions
{
    //todo: improve usage 
    public bool UseHttpRequestLogger { get; set; }
    public DrnAppBuilderType AppBuilderType { get; set; } = DrnAppBuilderType.DrnDefaults;
}