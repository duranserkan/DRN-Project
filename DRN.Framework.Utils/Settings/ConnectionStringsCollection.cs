namespace DRN.Framework.Utils.Settings;

public class ConnectionStringsCollection
{
    public ConnectionStringsCollection() { }
    public ConnectionStringsCollection(string key, string value) => Upsert(key, value);
    public Dictionary<string, string> ConnectionStrings { get; init; } = new();
    public void Upsert(string key, string value) => ConnectionStrings[key] = value;
}