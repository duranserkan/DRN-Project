using System.Runtime.CompilerServices;

namespace DRN.Framework.Utils.Logging;

public readonly struct ScopeName(string name)
{
    public string Name { get; } = name;

    public string GetKey([CallerMemberName] string? caller = null) => string.IsNullOrEmpty(caller) ? Name : $"{Name}.{caller}";
}