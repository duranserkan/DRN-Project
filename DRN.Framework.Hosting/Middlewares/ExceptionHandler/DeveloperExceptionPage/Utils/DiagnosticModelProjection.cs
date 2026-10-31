using System.Reflection;
using System.Text.Json;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.Logging.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;

internal sealed class DiagnosticModelProjection(IDiagnosticSanitizer sanitizer)
{
    private int _remainingFrames = sanitizer.Options.MaxCollectionEntries;
    private int _remainingHeaderValues = sanitizer.Options.MaxNodes;
    public string Text(string? value) => sanitizer.SanitizeText(value);

    public string Format(Exception exception) =>
        Text(exception.GetType().FullName) + ": " + Text(exception.Message) + Environment.NewLine + Text(exception.StackTrace);

    public ExceptionDetails Exception(ExceptionDetails source)
    {
        var frames = source.StackFrames.Take(_remainingFrames).Select(Frame).ToArray();
        _remainingFrames -= frames.Length;
        if (_remainingFrames == 0)
            frames = [.. frames, new StackFrameSourceCodeInfo { ErrorDetails = DiagnosticSanitizer.Limited }];
        return source.Error is not { } error
            ? new ExceptionDetails(Text(source.ExceptionMessage), frames)
            : new ExceptionDetails(error, frames)
            {
                ExceptionType = Text(source.ExceptionType),
                ExceptionMessage = Text(source.ExceptionMessage),
                InnerExceptionType = Text(source.InnerExceptionType),
                InnerExceptionMessage = Text(source.InnerExceptionMessage),
                FormattedException = Format(error),
                LoaderExceptionMessages = error is ReflectionTypeLoadException loader
                    ? loader.LoaderExceptions.Take(sanitizer.Options.MaxCollectionEntries).Select(item => Text(item?.Message)).ToArray() : []
            };
    }

    private StackFrameSourceCodeInfo Frame(StackFrameSourceCodeInfo frame)
    {
        var pre = frame.PreContextCode.Take(sanitizer.Options.MaxCollectionEntries).ToArray();
        var current = frame.ContextCode.Take(sanitizer.Options.MaxCollectionEntries).ToArray();
        var post = frame.PostContextCode.Take(sanitizer.Options.MaxCollectionEntries).ToArray();
        // Bound the entire captured excerpt before dividing its display sections.
        var lines = Lines(pre.Concat(current).Concat(post));
        return new StackFrameSourceCodeInfo
        {
            File = Text(frame.File), Function = Text(frame.Function), ErrorDetails = Text(frame.ErrorDetails),
            Line = frame.Line, PreContextLine = frame.PreContextLine,
            PreContextCode = lines.Take(pre.Length).ToArray(), ContextCode = lines.Skip(pre.Length).Take(current.Length).ToArray(),
            PostContextCode = lines.Skip(pre.Length + current.Length).ToArray()
        };
    }

    private string[] Lines(IEnumerable<string> lines)
    {
        var captured = new List<string>();
        var length = 0;
        foreach (var line in lines)
        {
            if (captured.Count == sanitizer.Options.MaxCollectionEntries || line.Length > sanitizer.Options.MaxFieldBytes - length)
                return [DiagnosticSanitizer.Limited];
            captured.Add(line);
            length += line.Length + 1;
        }
        return Text(string.Join('\n', captured)).Split('\n');
    }

    public Dictionary<string, string?[]> Strings(IEnumerable<KeyValuePair<string, string?[]>> source, bool allSensitive = false)
    {
        var result = new Dictionary<string, string?[]>(StringComparer.Ordinal);
        foreach (var pair in source.Take(sanitizer.Options.MaxCollectionEntries))
        {
            if (_remainingHeaderValues <= 0) { result["_diagnostic_limit"] = [DiagnosticSanitizer.Limited]; break; }
            var key = sanitizer.SanitizeKey(pair.Key);
            var sensitive = allSensitive && sanitizer is not DiagnosticSanitizer { RedactionEnabled: false } || sanitizer.IsSensitive(pair.Key);
            result[key] = sensitive ? [DiagnosticSanitizer.Redacted] :
                pair.Value.Take(Math.Min(sanitizer.Options.MaxCollectionEntries, _remainingHeaderValues)).Select(Text).ToArray();
            if (!sensitive && result[key].Length < pair.Value.Length)
                result[key] = [.. result[key], DiagnosticSanitizer.Limited];
            _remainingHeaderValues -= Math.Max(1, result[key].Length);
        }
        return result;
    }

    public EndpointModel? Endpoint(Endpoint? endpoint)
    {
        if (endpoint == null) return null;
        var route = endpoint as RouteEndpoint;
        return new EndpointModel
        {
            DisplayName = Text(endpoint.DisplayName), RoutePattern = Text(route?.RoutePattern.RawText), Order = route?.Order,
            HttpMethods = Text(string.Join(", ", endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Take(32) ?? [])),
            MetadataCollection = new EndpointMetadataCollectionModel
            {
                Values = endpoint.Metadata.Take(sanitizer.Options.MaxCollectionEntries).Select(metadata =>
                    new MetadataModel(Text(metadata.GetType().FullName), JsonSerializer.Serialize(sanitizer.SanitizeValue("Endpoint.Metadata", metadata)))).ToArray()
            }
        };
    }
}
