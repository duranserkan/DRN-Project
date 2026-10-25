// This file is licensed to you under the MIT license.

#nullable enable

using System.Text;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;

internal sealed class ParameterDisplayInfo
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public string? Prefix { get; set; }

    public override string ToString()
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(Prefix))
        {
            builder
                .Append(Prefix)
                .Append(' ');
        }

        builder.Append(Type);
        builder.Append(' ');
        builder.Append(Name);

        return builder.ToString();
    }
}