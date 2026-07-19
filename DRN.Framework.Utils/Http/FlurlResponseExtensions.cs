using Flurl.Http;

namespace DRN.Framework.Utils.Http;

public static class FlurlResponseExtensions
{
    public static async Task<HttpResponse<string>> ToStringAsync(this Task<IFlurlResponse> responseTask)
    {
        var response = await responseTask;
        return await response.ToStringAsync();
    }

    public static async Task<HttpResponse<string>> ToStringAsync(this IFlurlResponse response)
        => await FlurlResponseConverter.ToStringAsync(response);

    public static async Task<HttpResponse<byte[]>> ToBytesAsync(this Task<IFlurlResponse> responseTask)
    {
        var response = await responseTask;
        return await response.ToBytesAsync();
    }

    public static async Task<HttpResponse<byte[]>> ToBytesAsync(this IFlurlResponse response)
        => await FlurlResponseConverter.ToBytesAsync(response);

    public static async Task<HttpResponse<Stream>> ToStreamAsync(this Task<IFlurlResponse> responseTask)
    {
        var response = await responseTask;
        return await response.ToStreamAsync();
    }

    public static async Task<HttpResponse<Stream>> ToStreamAsync(this IFlurlResponse response)
        => await FlurlResponseConverter.ToStreamAsync(response);

    public static async Task<HttpResponse<TResponse>> FromJsonAsync<TResponse>(this Task<IFlurlResponse> responseTask)
    {
        var response = await responseTask;
        return await response.FromJsonAsync<TResponse>();
    }

    public static async Task<HttpResponse<TResponse>> FromJsonAsync<TResponse>(this IFlurlResponse response)
        => await FlurlResponseConverter.FromJsonAsync<TResponse>(response);

    public static async Task<HttpCallResult<string>> TryToStringAsync(this Task<IFlurlResponse> responseTask)
        => await FlurlResponseConverter.TryToStringAsync(responseTask);

    public static async Task<HttpCallResult<string>> TryToStringAsync(this IFlurlResponse response)
        => await FlurlResponseConverter.TryToStringAsync(response);

    public static async Task<HttpCallResult<byte[]>> TryToBytesAsync(this Task<IFlurlResponse> responseTask)
        => await FlurlResponseConverter.TryToBytesAsync(responseTask);

    public static async Task<HttpCallResult<byte[]>> TryToBytesAsync(this IFlurlResponse response)
        => await FlurlResponseConverter.TryToBytesAsync(response);

    public static async Task<HttpCallResult<Stream>> TryToStreamAsync(this Task<IFlurlResponse> responseTask)
        => await FlurlResponseConverter.TryToStreamAsync(responseTask);

    public static async Task<HttpCallResult<Stream>> TryToStreamAsync(this IFlurlResponse response)
        => await FlurlResponseConverter.TryToStreamAsync(response);

    public static async Task<HttpCallResult<TResponse>> TryFromJsonAsync<TResponse>(this Task<IFlurlResponse> responseTask)
        => await FlurlResponseConverter.TryFromJsonAsync<TResponse>(responseTask);

    public static async Task<HttpCallResult<TResponse>> TryFromJsonAsync<TResponse>(this IFlurlResponse response)
        => await FlurlResponseConverter.TryFromJsonAsync<TResponse>(response);
}
