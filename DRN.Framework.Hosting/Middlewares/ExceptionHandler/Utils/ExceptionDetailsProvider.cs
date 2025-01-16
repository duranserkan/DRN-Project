// This file is licensed to you under the MIT license.

using System.Reflection;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;

[Singleton<ExceptionDetailsProvider>]
public class ExceptionDetailsProvider(IOptions<DeveloperExceptionPageOptions> options, IWebHostEnvironment hostingEnvironment)
{
    private readonly DeveloperExceptionPageOptions _options = options.Value;
    private readonly IFileProvider _fileProvider = options.Value.FileProvider ?? hostingEnvironment.ContentRootFileProvider;

    public IEnumerable<ExceptionDetails> GetDetails(Exception exception)
    {
        var exceptions = FlattenAndReverseExceptionTree(exception);

        return exceptions.Select(ex => new ExceptionDetails(ex, GetStackFrames(ex)));
    }

    private IEnumerable<StackFrameSourceCodeInfo> GetStackFrames(Exception original)
    {
        var stackFrames = StackTraceHelper.GetFrames(original, out var exception)
            .Select(frame => GetStackFrameSourceCodeInfo(
                frame.MethodDisplayInfo?.ToString(),
                frame.FilePath,
                frame.LineNumber));
        _ = exception;

        return stackFrames;
    }

    private static List<Exception> FlattenAndReverseExceptionTree(Exception? ex)
    {
        // ReflectionTypeLoadException is special because the details are in
        // the LoaderExceptions property
        if (ex is ReflectionTypeLoadException typeLoadException)
        {
            var typeLoadExceptions = new List<Exception>();
            foreach (var loadException in typeLoadException.LoaderExceptions)
                if (loadException is not null)
                    typeLoadExceptions.AddRange(FlattenAndReverseExceptionTree(loadException));

            typeLoadExceptions.Add(typeLoadException);
            return typeLoadExceptions;
        }

        var list = new List<Exception>();
        if (ex is AggregateException aggregateException)
        {
            list.Add(ex);
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
                list.Add(innerException);
        }
        else
        {
            while (ex != null)
            {
                list.Add(ex);
                ex = ex.InnerException;
            }

            list.Reverse();
        }

        return list;
    }

    // make it internal to enable unit testing
    internal StackFrameSourceCodeInfo GetStackFrameSourceCodeInfo(string? method, string? filePath, int lineNumber)
    {
        var stackFrame = new StackFrameSourceCodeInfo
        {
            Function = method, File = filePath, Line = lineNumber
        };

        if (string.IsNullOrEmpty(stackFrame.File))
            return stackFrame;

        IEnumerable<string>? lines;
        if (File.Exists(stackFrame.File))
            lines = File.ReadLines(stackFrame.File);
        else
        {
            // Handle relative paths and embedded files
            var fileInfo = _fileProvider.GetFileInfo(stackFrame.File);
            lines = fileInfo.GetLines();
        }

        if (lines != null)
            ReadFrameContent(stackFrame, lines, stackFrame.Line, stackFrame.Line);

        return stackFrame;
    }

    // make it internal to enable unit testing
    internal void ReadFrameContent(
        StackFrameSourceCodeInfo frame,
        IEnumerable<string> allLines,
        int errorStartLineNumberInFile,
        int errorEndLineNumberInFile)
    {
        // Get the line boundaries in the file to be read and read all these lines at once into an array.
        var preErrorLineNumberInFile = Math.Max(errorStartLineNumberInFile - _options.SourceCodeLineCount, 1);
        var postErrorLineNumberInFile = errorEndLineNumberInFile + _options.SourceCodeLineCount;
        var codeBlock = allLines
            .Skip(preErrorLineNumberInFile - 1)
            .Take(postErrorLineNumberInFile - preErrorLineNumberInFile + 1)
            .ToArray();

        var numOfErrorLines = (errorEndLineNumberInFile - errorStartLineNumberInFile) + 1;
        var errorStartLineNumberInArray = errorStartLineNumberInFile - preErrorLineNumberInFile;

        frame.PreContextLine = preErrorLineNumberInFile;
        frame.PreContextCode = codeBlock.Take(errorStartLineNumberInArray).ToArray();
        frame.ContextCode = codeBlock
            .Skip(errorStartLineNumberInArray)
            .Take(numOfErrorLines)
            .ToArray();
        frame.PostContextCode = codeBlock
            .Skip(errorStartLineNumberInArray + numOfErrorLines)
            .ToArray();
    }
}