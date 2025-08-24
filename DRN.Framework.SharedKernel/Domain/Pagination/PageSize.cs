using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

public readonly struct PageSize
{
    public const int SizeDefault = 10;
    public const int MaxSizeDefault = 100;

    public const int MaxSizeThreshold = 1000;

    public static PageSize Default => new(SizeDefault);

    [JsonConstructor]
    public PageSize(int size, int maxSize = MaxSizeDefault) : this(size, maxSize, false)
    {
    }

    /// <summary>
    /// overrideMaxsizeThreshold only can be used for in process requests. Intentionally made non-serializable.
    /// </summary>
    public PageSize(int size, int maxSize, bool overrideMaxsizeThreshold = false)
    {
        if (overrideMaxsizeThreshold)
            MaxSize = maxSize;
        else
            MaxSize = maxSize < MaxSizeThreshold ? maxSize : MaxSizeThreshold;

        if (MaxSize < 1)
            MaxSize = 1;

        Size = size > MaxSize ? MaxSize : size;
        if (Size < 1)
            Size = 1;
    }

    public int Size { get; init; }

    //Required to preserve MaxSizeDefault override up to MaxSizeThreshold for additional requests
    public int MaxSize { get; init; }

    public bool Valid() => MaxSize >= Size && Size > 0;
}