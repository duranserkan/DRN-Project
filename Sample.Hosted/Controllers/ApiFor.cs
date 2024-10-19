namespace Sample.Hosted.Controllers;

public abstract class ApiFor
{
    public static UserApiFor User { get; } = new();
}

public class UserApiFor
{
    public const string Prefix = "/Api/User";

    /// <summary>
    /// Profile Picture Controller
    /// </summary>
    public ProfilePictureFor PP { get; } = new();
}

public class ProfilePictureFor
{
    private const string Prefix = $"{UserApiFor.Prefix}/ProfilePicture";

    public string Get { get; } = Prefix;
}