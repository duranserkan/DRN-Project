using Sample.Application;
using Sample.Utils.Image;

namespace DRN.Test.Tests.Sample.Utils;

public class JpegUtilsTests
{
    [Theory]
    [DataInline]
    public void JpegUtils_Should_Resize_Image(TestContext context)
    {
        context.ServiceCollection.AddSampleApplicationServices();
        var utils = context.GetRequiredService<IJpegUtils>();


        // var image = context.GetData("");
        // utils.ResizeImage(null, 100);
    }
}