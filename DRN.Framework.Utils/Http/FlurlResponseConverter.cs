using Flurl.Http;

namespace DRN.Framework.Utils.Http;

internal static class FlurlResponseConverter
{
    internal static async Task<HttpResponse<string>> ToStringAsync(IFlurlResponse response)
        => await ToBufferedAsync(response, static value => value.GetStringAsync());

    internal static async Task<HttpResponse<byte[]>> ToBytesAsync(IFlurlResponse response)
        => await ToBufferedAsync(response, static value => value.GetBytesAsync());

    internal static async Task<HttpResponse<Stream>> ToStreamAsync(IFlurlResponse response)
    {
        try
        {
            var httpStatus = response.StatusCode;
            var result = await response.GetStreamAsync();

            return new HttpResponse<Stream>(httpStatus, result, response);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    internal static async Task<HttpResponse<TResponse>> FromJsonAsync<TResponse>(IFlurlResponse response)
        => await ToBufferedAsync(response, static value => value.GetJsonAsync<TResponse>());

    internal static async Task<HttpCallResult<string>> TryToStringAsync(Task<IFlurlResponse> responseTask)
        => await TryResponseAsync(responseTask, TryToStringAsync);

    internal static async Task<HttpCallResult<string>> TryToStringAsync(IFlurlResponse response)
        => await TryToBufferedAsync(response, static value => value.GetStringAsync(), HttpFailureKind.ResponseRead);

    internal static async Task<HttpCallResult<byte[]>> TryToBytesAsync(Task<IFlurlResponse> responseTask)
        => await TryResponseAsync(responseTask, TryToBytesAsync);

    internal static async Task<HttpCallResult<byte[]>> TryToBytesAsync(IFlurlResponse response)
        => await TryToBufferedAsync(response, static value => value.GetBytesAsync(), HttpFailureKind.ResponseRead);

    internal static async Task<HttpCallResult<Stream>> TryToStreamAsync(Task<IFlurlResponse> responseTask)
        => await TryResponseAsync(responseTask, TryToStreamAsync);

    internal static async Task<HttpCallResult<Stream>> TryToStreamAsync(IFlurlResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        int? httpStatus = null;
        try
        {
            httpStatus = response.StatusCode;
            var result = await response.GetStreamAsync();

            return HttpCallResult<Stream>.FromResponse(new HttpResponse<Stream>(httpStatus.Value, result, response));
        }
        catch (OperationCanceledException)
        {
            response.Dispose();
            throw;
        }
        catch (Exception exception) when (!IsCriticalException(exception))
        {
            response.Dispose();
            return HttpCallResult<Stream>.FromFailure(httpStatus, HttpFailureKind.ResponseRead, exception);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    internal static async Task<HttpCallResult<TResponse>> TryFromJsonAsync<TResponse>(
        Task<IFlurlResponse> responseTask)
        => await TryResponseAsync(responseTask, TryFromJsonAsync<TResponse>);

    internal static async Task<HttpCallResult<TResponse>> TryFromJsonAsync<TResponse>(IFlurlResponse response)
        => await TryToBufferedAsync(response, static value => value.GetJsonAsync<TResponse>(),
            HttpFailureKind.Deserialization);

    private static async Task<HttpResponse<TResult>> ToBufferedAsync<TResult>(
        IFlurlResponse response, Func<IFlurlResponse, Task<TResult>> readPayload)
    {
        try
        {
            var httpStatus = response.StatusCode;
            var result = await readPayload(response);

            return new HttpResponse<TResult>(httpStatus, result);
        }
        finally
        {
            response.Dispose();
        }
    }

    private static async Task<HttpCallResult<TResult>> TryResponseAsync<TResult>(
        Task<IFlurlResponse> responseTask,
        Func<IFlurlResponse, Task<HttpCallResult<TResult>>> convertResponse)
    {
        ArgumentNullException.ThrowIfNull(responseTask);

        try
        {
            var response = await responseTask;
            return await convertResponse(response);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FlurlHttpTimeoutException exception)
        {
            return HttpCallResult<TResult>.FromFailure(null, HttpFailureKind.Timeout, exception);
        }
        catch (FlurlHttpException exception) when (exception.Call.Response is { } response)
        {
            return await convertResponse(response);
        }
        catch (Exception exception) when (!IsCriticalException(exception))
        {
            return HttpCallResult<TResult>.FromFailure(null, HttpFailureKind.Transport, exception);
        }
    }

    private static async Task<HttpCallResult<TResult>> TryToBufferedAsync<TResult>(
        IFlurlResponse response,
        Func<IFlurlResponse, Task<TResult>> readPayload,
        HttpFailureKind failureKind)
    {
        ArgumentNullException.ThrowIfNull(response);

        int? httpStatus = null;
        try
        {
            httpStatus = response.StatusCode;
            var result = await readPayload(response);

            return HttpCallResult<TResult>.FromResponse(new HttpResponse<TResult>(httpStatus.Value, result));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (!IsCriticalException(exception))
        {
            return HttpCallResult<TResult>.FromFailure(httpStatus, failureKind, exception);
        }
        finally
        {
            response.Dispose();
        }
    }

    private static bool IsCriticalException(Exception exception) =>
        exception is OutOfMemoryException or AccessViolationException;
}
