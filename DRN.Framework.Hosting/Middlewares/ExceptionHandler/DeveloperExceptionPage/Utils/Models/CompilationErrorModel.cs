// This file is licensed to you under the MIT license.

using Microsoft.AspNetCore.Builder;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;

/// <summary>
/// Holds data to be displayed on the compilation error page.
/// </summary>
public class CompilationErrorModel(DeveloperExceptionPageOptions options)
{
    /// <summary>
    /// Options for what output to display.
    /// </summary>
    public DeveloperExceptionPageOptions Options { get; } = options;

    /// <summary>
    /// Detailed information about each parse or compilation error.
    /// </summary>
    public IList<ExceptionDetails> ErrorDetails { get; } = new List<ExceptionDetails>();

    /// <summary>
    /// Gets the generated content that produced the corresponding <see cref="ErrorDetails"/>.
    /// </summary>
    public IList<string?> CompiledContent { get; } = new List<string?>();

    public string Title { get; set; } = "Error Page";
}