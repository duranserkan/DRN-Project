using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed record ConventionInput(string Kind, string Identity, ImmutableDictionary<string, string> Fields, Location Location)
{
    internal string Get(string name) => Fields.TryGetValue(name, out var value) ? value : "";
}

internal sealed record ConventionInputs(ImmutableArray<ConventionInput> Records, ImmutableArray<Diagnostic> Diagnostics)
{
    internal const string FileName = "DrnConventionInputs.g.cs";

    internal static ConventionInputs Read(AnalyzerOptions options, CancellationToken cancellation)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var files = options.AdditionalFiles.Where(file =>
            ConventionProject.NormalizeSeparators(file.Path).Split('/')[^1] == FileName).Take(2).ToArray();

        if (files.Length != 1)
        {
            diagnostics.Add(ConventionProject.InputIssue(FileName, files.Length == 0 ? "missing" : "duplicated",
                "build-generated convention input file"));
            return new([], diagnostics.ToImmutable());
        }

        var text = files[0].GetText(cancellation);
        if (text is null || text.Length > ConventionInputLimits.MaxCharacters)
        {
            diagnostics.Add(ConventionProject.InputIssue(FileName, text is null ? "unreadable" : "over the input size limit",
                "readable bounded convention input"));
            return new([], diagnostics.ToImmutable());
        }

        var root = ConventionInputSyntax.Parse(text, files[0].Path, cancellation, out var problem);
        if (root is null)
        {
            diagnostics.Add(ConventionProject.InputIssue(FileName, problem, "build-generated convention input"));
            return new([], diagnostics.ToImmutable());
        }

        return new(CollectRecords(root, files[0].Path, text, cancellation), diagnostics.ToImmutable());
    }

    private static ImmutableArray<ConventionInput> CollectRecords(CompilationUnitSyntax root, string path,
        SourceText text, CancellationToken cancellation)
    {
        var records = ImmutableArray.CreateBuilder<ConventionInput>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var list in root.AttributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                cancellation.ThrowIfCancellationRequested();
                var record = ReadAttributeRecord(attribute, path, text, cancellation, out var key);
                if (seen.Add(key)) records.Add(record);
            }
        }
        return records.ToImmutable();
    }

    private static ConventionInput ReadAttributeRecord(AttributeSyntax attribute, string path, SourceText text,
        CancellationToken cancellation, out string key)
    {
        var kind = ConventionInputSyntax.Kind(attribute);
        var arguments = attribute.ArgumentList!.Arguments;
        var identity = ConventionInputSyntax.String(arguments[0].Expression, cancellation)!;
        var fields = arguments.Skip(1).ToImmutableDictionary(argument =>
        {
            var name = argument.NameEquals!.Name.Identifier.ValueText;
            return kind == ConventionInputSyntax.ExceptionKind && name == ConventionInputSyntax.SymbolTargetField
                ? ConventionInputSyntax.TargetField
                : name;
        }, argument => ConventionInputSyntax.String(argument.Expression, cancellation)!, StringComparer.Ordinal);
        var location = Location.Create(path, attribute.Span, text.Lines.GetLinePositionSpan(attribute.Span));
        key = kind.Length + ":" + kind + identity.Length + ":" + identity + string.Concat(
            fields.OrderBy(field => field.Key, StringComparer.Ordinal)
                .Select(field => field.Key.Length + ":" + field.Key + field.Value.Length + ":" + field.Value));
        return new(kind, identity, fields, location);
    }
}
