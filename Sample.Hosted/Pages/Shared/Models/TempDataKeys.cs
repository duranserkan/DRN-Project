namespace Sample.Hosted.Pages.Shared.Models;

public class TempDataKeys
{
    public string StatusMessage { get; } = nameof(StatusMessage);
    public string AccessDeniedRequestId { get; } = nameof(AccessDeniedRequestId);
}
