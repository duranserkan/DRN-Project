// This file is licensed to you under the MIT license.

#nullable enable

using System.Diagnostics;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;

internal sealed class StackFrameInfo
{
    public StackFrameInfo(int lineNumber, string? filePath, StackFrame? stackFrame, MethodDisplayInfo? methodDisplayInfo)
    {
        LineNumber = lineNumber;
        FilePath = filePath;
        StackFrame = stackFrame;
        MethodDisplayInfo = methodDisplayInfo;
    }

    public int LineNumber { get; }

    public string? FilePath { get; }

    public StackFrame? StackFrame { get; }

    public MethodDisplayInfo? MethodDisplayInfo { get; }
}