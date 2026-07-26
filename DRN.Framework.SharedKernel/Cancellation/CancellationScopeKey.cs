namespace DRN.Framework.SharedKernel.Cancellation;

/// <summary>Identifies one child cancellation scope by a non-null ordinal name and an optional owning type.</summary>
/// <remarks>
/// Names are non-null developer-defined constants; empty and whitespace names are permitted. They must not come from request, user,
/// instance, or operation identifiers. Ownerless keys share one ordinal-name namespace within an <c>ICancellationUtils</c> service
/// scope, so use them only for intentional cross-type groups and qualify their names to prevent accidental collisions. The default
/// value is invalid.
/// </remarks>
public readonly struct CancellationScopeKey : IEquatable<CancellationScopeKey>
{
    private const int MaxNameLength = 128;
    private readonly Type? _ownerType;
    private readonly string _name;

    private CancellationScopeKey(Type? ownerType, string name)
    {
        _ownerType = ownerType;
        _name = name;
    }

    /// <summary>Creates an ownerless key for an intentional cross-type group with the specified ordinal name.</summary>
    /// <param name="name">A non-null developer-defined constant of at most 128 characters, preferably qualified to avoid collisions.</param>
    /// <returns>A valid ownerless key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is longer than 128 characters.</exception>
    public static CancellationScopeKey For(string name) => For(null, name);

    /// <summary>Creates an empty-name key owned by <typeparamref name="TScope"/>.</summary>
    /// <typeparam name="TScope">The component or workflow type that owns the group.</typeparam>
    /// <returns>A valid type-owned key.</returns>
    public static CancellationScopeKey For<TScope>() => For(typeof(TScope));

    /// <summary>Creates a named key owned by <typeparamref name="TScope"/>.</summary>
    /// <typeparam name="TScope">The component or workflow type that owns the group.</typeparam>
    /// <param name="name">A non-null developer-defined constant of at most 128 characters.</param>
    /// <returns>A valid named key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is longer than 128 characters.</exception>
    public static CancellationScopeKey For<TScope>(string name) => For(typeof(TScope), name);

    /// <summary>Creates an empty-name key owned by <paramref name="ownerType"/>.</summary>
    /// <param name="ownerType">The component or workflow type that owns the group.</param>
    /// <returns>A valid type-owned key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ownerType"/> is <see langword="null"/>.</exception>
    public static CancellationScopeKey For(Type ownerType)
    {
        ArgumentNullException.ThrowIfNull(ownerType);
        return new CancellationScopeKey(ownerType, string.Empty);
    }

    /// <summary>Creates a named key owned by <paramref name="ownerType"/>.</summary>
    /// <param name="ownerType">The component or workflow type that owns the group, or <see langword="null"/>.</param>
    /// <param name="name">A developer-defined constant of at most 128 characters.</param>
    /// <returns>A valid named key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is longer than 128 characters.</exception>
    public static CancellationScopeKey For(Type? ownerType, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Length > MaxNameLength
            ? throw new ArgumentException($"Cancellation scope names cannot exceed {MaxNameLength} characters.", nameof(name))
            : new CancellationScopeKey(ownerType, name);
    }

    /// <inheritdoc />
    public bool Equals(CancellationScopeKey other)
        => _ownerType == other._ownerType && StringComparer.Ordinal.Equals(_name, other._name);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CancellationScopeKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_ownerType);
        hash.Add(_name, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    /// <summary>Returns whether two keys have the same owner type and ordinal name.</summary>
    public static bool operator ==(CancellationScopeKey left, CancellationScopeKey right) => left.Equals(right);

    /// <summary>Returns whether two keys differ by owner type or ordinal name.</summary>
    public static bool operator !=(CancellationScopeKey left, CancellationScopeKey right) => !left.Equals(right);

    /// <summary>Gets a value indicating whether this key was created via a factory method and is not the invalid default value.</summary>
    public bool IsValid => _name is not null;
}
