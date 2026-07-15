namespace DRN.Framework.Utils.Cancellation;

/// <summary>Identifies one named child cancellation scope by its owning type and an optional ordinal name.</summary>
/// <remarks>
/// Names are developer-defined constants for intentionally distinct groups owned by the same type. They must not come from request,
/// user, or operation identifiers. The default value is invalid.
/// </remarks>
public readonly struct CancellationScopeKey : IEquatable<CancellationScopeKey>
{
    private const int MaxNameLength = 128;
    private readonly Type? _ownerType;

    private CancellationScopeKey(Type ownerType, string? name)
    {
        _ownerType = ownerType;
        Name = name;
    }

    /// <summary>Gets the type that owns this cancellation group.</summary>
    /// <exception cref="InvalidOperationException">This key is the invalid default value.</exception>
    public Type OwnerType => _ownerType ?? throw new InvalidOperationException("The default cancellation scope key is invalid.");

    /// <summary>Gets the optional developer-defined group name compared with ordinal, case-sensitive equality.</summary>
    public string? Name { get; }

    /// <summary>Creates a type-only key owned by <typeparamref name="TScope"/>.</summary>
    /// <typeparam name="TScope">The component or workflow type that owns the group.</typeparam>
    /// <returns>A valid type-only key.</returns>
    public static CancellationScopeKey For<TScope>() => For(typeof(TScope));

    /// <summary>Creates a named key owned by <typeparamref name="TScope"/>.</summary>
    /// <typeparam name="TScope">The component or workflow type that owns the group.</typeparam>
    /// <param name="name">A nonblank developer-defined constant of at most 128 characters.</param>
    /// <returns>A valid named key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or longer than 128 characters.</exception>
    public static CancellationScopeKey For<TScope>(string name) => For(typeof(TScope), name);

    /// <summary>Creates a type-only key owned by <paramref name="ownerType"/>.</summary>
    /// <param name="ownerType">The component or workflow type that owns the group.</param>
    /// <returns>A valid type-only key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ownerType"/> is <see langword="null"/>.</exception>
    public static CancellationScopeKey For(Type ownerType)
    {
        ArgumentNullException.ThrowIfNull(ownerType);
        return new CancellationScopeKey(ownerType, null);
    }

    /// <summary>Creates a named key owned by <paramref name="ownerType"/>.</summary>
    /// <param name="ownerType">The component or workflow type that owns the group.</param>
    /// <param name="name">A nonblank developer-defined constant of at most 128 characters.</param>
    /// <returns>A valid named key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ownerType"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or longer than 128 characters.</exception>
    public static CancellationScopeKey For(Type ownerType, string name)
    {
        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Length > MaxNameLength
            ? throw new ArgumentException($"Cancellation scope names cannot exceed {MaxNameLength} characters.", nameof(name))
            : new CancellationScopeKey(ownerType, name);
    }

    /// <inheritdoc />
    public bool Equals(CancellationScopeKey other)
        => _ownerType == other._ownerType && StringComparer.Ordinal.Equals(Name, other.Name);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CancellationScopeKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_ownerType);
        hash.Add(Name, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (_ownerType is null) return "<invalid cancellation scope key>";

        var ownerName = _ownerType.FullName ?? _ownerType.Name;
        return Name is null ? ownerName : $"{ownerName}:{Name}";
    }

    /// <summary>Returns whether two keys have the same owner type and ordinal name.</summary>
    public static bool operator ==(CancellationScopeKey left, CancellationScopeKey right) => left.Equals(right);

    /// <summary>Returns whether two keys differ by owner type or ordinal name.</summary>
    public static bool operator !=(CancellationScopeKey left, CancellationScopeKey right) => !left.Equals(right);

    internal void Validate(string parameterName)
    {
        if (_ownerType is null)
            throw new ArgumentException("The default cancellation scope key is invalid.", parameterName);
    }
}
