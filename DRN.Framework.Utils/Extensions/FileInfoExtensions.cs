// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.FileProviders;

namespace DRN.Framework.Utils.Extensions;

public static class FileInfoExtensions
{
    public static IEnumerable<string>? GetLines(this IFileInfo fileInfo)
    {
        if (!fileInfo.Exists) return null;

        // ReadLines doesn't accept a stream. Use ReadLines as its more efficient relative to reading lines via stream reader
        return !string.IsNullOrEmpty(fileInfo.PhysicalPath)
            ? File.ReadLines(fileInfo.PhysicalPath)
            : ReadLines(fileInfo);
    }

    private static IEnumerable<string> ReadLines(IFileInfo fileInfo)
    {
        using var reader = new StreamReader(fileInfo.CreateReadStream());
        while (reader.ReadLine() is { } line)
            yield return line;
    }
}