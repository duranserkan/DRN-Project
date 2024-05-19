using Flurl.Http;

namespace DRN.Framework.Utils.Http;

public static class FlurlResponseExtensions
{
    public static async Task<HttpResponse<string>> ToStringAsync(this Task<IFlurlResponse> responseTask)
    {
        var response = await responseTask;
        return await response.ToStringAsync();
    }

    public static async Task<HttpResponse<byte[]>> ToBytesAsync(this Task<IFlurlResponse> responseTask)
    {
        var response = await responseTask;
        return await response.ToBytesAsync();
    }

    public static async Task<HttpResponse<Stream>> ToStreamAsync(this Task<IFlurlResponse> responseTask)
    {
        var response = await responseTask;
        return await response.ToStreamAsync();
    }

    public static async Task<HttpResponse<TResponse>> ToJsonAsync<TResponse>(this Task<IFlurlResponse> responseTask)
    {
        var response = await responseTask;
        return await response.ToJsonAsync<TResponse>();
    }

    public static async Task<HttpResponse<string>> ToStringAsync(this IFlurlResponse response)
        => await HttpResponse.ToStringAsync(response);

    public static async Task<HttpResponse<byte[]>> ToBytesAsync(this IFlurlResponse response)
        => await HttpResponse.ToBytesAsync(response);

    public static async Task<HttpResponse<Stream>> ToStreamAsync(this IFlurlResponse response)
        => await HttpResponse.ToStreamAsync(response);

    public static async Task<HttpResponse<TResponse>> ToJsonAsync<TResponse>(this IFlurlResponse response)
        => await HttpResponse.ToJsonAsync<TResponse>(response);
}

public class HttpResponse(IFlurlResponse response)
{
    public static async Task<HttpResponse<string>> ToStringAsync(IFlurlResponse response)
    {
        var result = await response.GetStringAsync();
        response.Dispose();

        return new HttpResponse<string>(response, result);
    }

    public static async Task<HttpResponse<byte[]>> ToBytesAsync(IFlurlResponse response)
    {
        var result = await response.GetBytesAsync();
        response.Dispose();

        return new HttpResponse<byte[]>(response, result);
    }

    public static async Task<HttpResponse<Stream>> ToStreamAsync(IFlurlResponse response)
    {
        var result = await response.GetStreamAsync();

        return new HttpResponse<Stream>(response, result);
    }

    public static async Task<HttpResponse<TResponse>> ToJsonAsync<TResponse>(IFlurlResponse response)
    {
        var result = await response.GetJsonAsync<TResponse>();
        response.Dispose();

        return new HttpResponse<TResponse>(response, result);
    }

    public IFlurlResponse Response { get; } = response;
    public int HttpStatus { get; } = response.StatusCode;
}

public class HttpResponse<TResult>(IFlurlResponse response, TResult? result) : HttpResponse(response)
{
    public TResult? Result { get; } = result;
}