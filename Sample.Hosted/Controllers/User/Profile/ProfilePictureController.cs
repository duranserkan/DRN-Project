using DRN.Framework.Hosting.Auth;
using DRN.Framework.Utils.Scope;
using DRN.Framework.Utils.Time;
using Sample.Domain.Identity.ProfilePictures;

namespace Sample.Hosted.Controllers.User.Profile;

[ApiController]
[Route(UserApiFor.ControllerRouteTemplate)]
[Authorize(AuthPolicy.MfaExempt)]
public class ProfilePictureController(IProfilePictureRepository ppRepository, IWebHostEnvironment hostingEnvironment) : ControllerBase
{
    [HttpGet]
    [Produces("image/jpeg")] // Adjust MIME type if needed
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get()
    {
        var currentUserId = ScopeContext.UserId;
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Forbid();

        var ppData = await ppRepository.GetProfilePictureAsync(currentUserId);

        Stream stream;
        if (ppData == null)
        {
            //from Lorem Picsum
            var path = Path.Combine(hostingEnvironment.WebRootPath, "images", "mountain.jpg");
            stream = System.IO.File.OpenRead(path);
        }
        else
            stream = new MemoryStream(ppData.ImageData);

        // Set caching headers
        Response.Headers.CacheControl = "private, max-age=31536000"; // Cache for 1 year
        Response.Headers.Expires = DateTimeProvider.UtcNow.AddYears(1).ToString("R");

        return new FileStreamResult(stream, "image/jpeg");
    }
}
