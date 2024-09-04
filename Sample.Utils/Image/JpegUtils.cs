using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace Sample.Utils.Image;

public interface IJpegUtils
{
    byte[] ResizeImage(Stream pictureStream, int width, int height = 0);
}

[Transient<IJpegUtils>]
public class JpegUtils : IJpegUtils
{
    public byte[] ResizeImage(Stream pictureStream, int width, int height = 0)
    {
        throw new NotImplementedException();
    }
}