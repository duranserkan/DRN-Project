using Sample.Domain.Identity.ProfilePictures;
using Sample.Hosted.Auth;

namespace Sample.Hosted.Controllers.Account;

[ApiController]
[Route("[controller]")]
[Authorize(AuthPolicy.MFAExempt)]
public class ProfilePictureController(IProfilePictureRepository ppRepository, IWebHostEnvironment hostingEnvironment) : ControllerBase
{
    [HttpGet("{userId:required}")]
    [ProducesResponseType(200)]
    public async Task<ActionResult<string>> Get(string userId)
    {
        var ppData = await ppRepository.GetProfilePictureAsync(userId);

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
        Response.Headers.Expires = DateTime.UtcNow.AddYears(1).ToString("R");

        return new FileStreamResult(stream, "image/jpeg");
    }
}