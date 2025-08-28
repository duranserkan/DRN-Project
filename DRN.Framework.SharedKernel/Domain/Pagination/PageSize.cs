using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

public class PageSize
{
    public const int SizeDefault = 10;
    public const int MaxSizeDefault = 100;
    public const int MaxSizeThreshold = 1000;

    public static PageSize Default => new(SizeDefault);

    private readonly int _size = SizeDefault;
    private readonly int _maxSize = MaxSizeDefault;

    /// <summary>
    /// Required for ASP.NET Core model binding from query strings and form data.
    /// The framework needs a parameterless constructor to instantiate the object
    /// before setting properties during binding with application/x-www-form-urlencoded format.
    /// </summary>
    public PageSize()
    {
    }

    [JsonConstructor]
    [SetsRequiredMembers]
    public PageSize(int size, int maxSize = MaxSizeDefault) : this(size, maxSize, false)
    {
    }

    /// <summary>
    /// overrideMaxsizeThreshold only can be used for in process requests. Intentionally made non-serializable.
    /// </summary>
    [SetsRequiredMembers]
    public PageSize(int size, int maxSize, bool overrideMaxsizeThreshold = false)
    {
        if (overrideMaxsizeThreshold)
            _maxSize = maxSize;
        else
            MaxSize = maxSize;

        Size = size;
    }

    /// <summary>
    /// Required to preserve MaxSizeDefault override up to MaxSizeThreshold for additional requests
    /// </summary>
    public required int MaxSize
    {
        get => _maxSize < 1 ? MaxSizeDefault : _maxSize;
        init => _maxSize = value > MaxSizeThreshold ? MaxSizeThreshold : value;
    }

    public required int Size
    {
        get => _size > MaxSize ? MaxSize : _size;
        init => _size = value < 1 ? 1 : value;
    }

    public bool Valid() => MaxSize >= Size && Size > 0;
}