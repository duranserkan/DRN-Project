using DRN.Framework.Hosting.Auth;
using DRN.Framework.Utils.Time;
using Sample.Domain.Identity.ProfilePictures;

namespace Sample.Hosted.Controllers.User.Profile;

[ApiController]
[Route(UserApiFor.ControllerRouteTemplate)]
[Authorize(AuthPolicy.MfaExempt)]
public class ProfilePictureController(IProfilePictureRepository ppRepository, IWebHostEnvironment hostingEnvironment) : ControllerBase
{
    private const string JpegContentType = "image/jpeg";
    
    [HttpGet("{userId:required}")]
    [Produces(JpegContentType)] // Adjust MIME type if needed
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<FileStreamResult> Get(string userId)
    {
        //todo evaluate ppID usage instead of userId
        var ppData = await ppRepository.GetProfilePictureAsync(userId);
        Stream stream = ppData != null
            ? new MemoryStream(ppData.ImageData)
            : System.IO.File.OpenRead(Path.Combine(hostingEnvironment.WebRootPath, "images", "mountain.jpg")); //from Lorem Picsum
        
        // Set caching headers
        Response.Headers.CacheControl = "private, max-age=31536000"; // Cache for 1 year
        Response.Headers.Expires = DateTimeProvider.UtcNow.AddYears(1).ToString("R");

        return new FileStreamResult(stream, JpegContentType);
    }
}