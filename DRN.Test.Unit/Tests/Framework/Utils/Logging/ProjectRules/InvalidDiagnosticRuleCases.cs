namespace DRN.Test.Shared;

internal static class InvalidDiagnosticRuleCases
{
    internal static string[] Create() =>
    [
        "{", "[]", "{\"personalFields\":null}", "{\"personalFields\":[1]}",
        "{\"personalFields\":[\"---\"]}", "{\"unknown\":[]}",
        "{\"personalFields\":[],\"personalFields\":[]}",
        "{\"personalFields\":[\"" + new string('x', 513) + "\"]}",
        "{\"personalFields\":[" + string.Join(",", Enumerable.Repeat("\"value\"", 257)) + "]}",
        "{}" + new string(' ', 65536)
    ];
}
