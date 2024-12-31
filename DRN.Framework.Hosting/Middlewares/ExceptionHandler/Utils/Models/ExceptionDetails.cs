// This file is licensed to you under the MIT license.

#nullable enable

using System.Text.Json.Serialization;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;

/// <summary>
/// Contains details for individual exception messages.
/// </summary>
public class ExceptionDetails
{
    [JsonConstructor]
    private ExceptionDetails()
    {
    }

    public ExceptionDetails(Exception error, IEnumerable<StackFrameSourceCodeInfo> stackFrames)
    {
        Error = error;
        StackFrames = stackFrames;
        ExceptionType = error.GetType().FullName ?? error.GetType().Name;
        ExceptionMessage = error.Message;
        InnerExceptionType = error.InnerException?.GetType().FullName;
        InnerExceptionMessage = error.InnerException?.Message;
    }

    public ExceptionDetails(string exceptionMessage, IEnumerable<StackFrameSourceCodeInfo> stackFrames)
    {
        StackFrames = stackFrames;
        ExceptionType = "Compilation";
        ExceptionMessage = exceptionMessage;
    }

    /// <summary>
    /// An individual exception
    /// </summary>
    [JsonIgnore]
    public Exception? Error { get; }

    public string ExceptionType { get; init; } = string.Empty;
    public string ExceptionMessage { get; init; } = string.Empty;
    public string? InnerExceptionType { get; init; }
    public string? InnerExceptionMessage { get; init; }

    /// <summary>
    /// The generated stack frames
    /// </summary>
    public IEnumerable<StackFrameSourceCodeInfo> StackFrames { get; init; } = Array.Empty<StackFrameSourceCodeInfo>();
}