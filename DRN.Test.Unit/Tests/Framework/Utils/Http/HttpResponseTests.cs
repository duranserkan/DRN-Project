using System.Text.Json;
using DRN.Framework.Utils.Http;
using Flurl.Http;

namespace DRN.Test.Unit.Tests.Framework.Utils.Http;

public class HttpResponseTests
{
    [Fact]
    public void HttpResponse_Should_Classify_Status_Codes()
    {
        new HttpResponse<string>(101, null).StatusClass.Should().Be(HttpStatusClass.Informational);
        new HttpResponse<string>(204, null).StatusClass.Should().Be(HttpStatusClass.Success);
        new HttpResponse<string>(302, null).StatusClass.Should().Be(HttpStatusClass.Redirection);
        new HttpResponse<string>(404, null).StatusClass.Should().Be(HttpStatusClass.ClientError);
        new HttpResponse<string>(503, null).StatusClass.Should().Be(HttpStatusClass.ServerError);
        new HttpResponse<string>(700, null).StatusClass.Should().Be(HttpStatusClass.Unknown);

        new HttpResponse<string>(200, null).IsSuccessStatusCode.Should().BeTrue();
        new HttpResponse<string>(300, null).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task ToStringAsync_Should_Dispose_Response_When_Read_Fails()
    {
        var response = Substitute.For<IFlurlResponse>();
        response.GetStringAsync().Returns(Task.FromException<string>(CreateReadException()));

        var read = async () => await response.ToStringAsync();

        await read.Should().ThrowExactlyAsync<InvalidOperationException>();
        response.Received(1).Dispose();
    }

    [Fact]
    public async Task ToBytesAsync_Should_Dispose_Response_When_Read_Fails()
    {
        var response = Substitute.For<IFlurlResponse>();
        response.GetBytesAsync().Returns(Task.FromException<byte[]>(CreateReadException()));

        var read = async () => await response.ToBytesAsync();

        await read.Should().ThrowExactlyAsync<InvalidOperationException>();
        response.Received(1).Dispose();
    }

    [Fact]
    public async Task FromJsonAsync_Should_Dispose_Response_When_Read_Fails()
    {
        var response = Substitute.For<IFlurlResponse>();
        response.GetJsonAsync<TestPayload>().Returns(Task.FromException<TestPayload>(CreateReadException()));

        var read = async () => await response.FromJsonAsync<TestPayload>();

        await read.Should().ThrowExactlyAsync<InvalidOperationException>();
        response.Received(1).Dispose();
    }

    [Fact]
    public async Task TryFromJsonAsync_Should_Return_Deserialization_Failure_With_Response_Status()
    {
        var response = Substitute.For<IFlurlResponse>();
        var exception = new JsonException("Response JSON is malformed.");
        response.StatusCode.Returns(200);
        response.GetJsonAsync<TestPayload>().Returns(Task.FromException<TestPayload>(exception));

        var result = await response.TryFromJsonAsync<TestPayload>();

        result.ResponseReceived.Should().BeTrue();
        result.HttpStatus.Should().Be(200);
        result.StatusClass.Should().Be(HttpStatusClass.Success);
        result.IsSuccessStatusCode.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Payload.Should().BeNull();
        result.Failure.Should().NotBeNull();
        result.Failure!.Kind.Should().Be(HttpFailureKind.Deserialization);
        result.Failure.Exception.Should().BeSameAs(exception);
        var serializedResult = JsonSerializer.Serialize(result);
        serializedResult.Should().NotContain("\"Exception\":");
        serializedResult.Should().NotContain("\"Message\":");
        response.Received(1).Dispose();
    }

    [Fact]
    public async Task TryToStringAsync_Should_Preserve_And_Rethrow_Transport_Failure_When_No_Response_Is_Received()
    {
        var exception = new InvalidOperationException("Connection failed.");
        var responseTask = Task.FromException<IFlurlResponse>(exception);

        var result = await responseTask.TryToStringAsync();

        result.ResponseReceived.Should().BeFalse();
        result.HttpStatus.Should().BeNull();
        result.StatusClass.Should().Be(HttpStatusClass.Unknown);
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Kind.Should().Be(HttpFailureKind.Transport);
        result.Failure.Exception.Should().BeSameAs(exception);

        Action throwFailure = result.ThrowIfFailure;

        throwFailure.Should().ThrowExactly<InvalidOperationException>().Which.Should().BeSameAs(exception);
    }

    [Fact]
    public async Task ThrowIfFailure_Should_Not_Throw_For_Http_Error_Status()
    {
        var response = Substitute.For<IFlurlResponse>();
        response.StatusCode.Returns(404);
        response.GetStringAsync().Returns(Task.FromResult("Not found."));
        var result = await response.TryToStringAsync();

        Action throwFailure = result.ThrowIfFailure;

        throwFailure.Should().NotThrow();
        result.StatusClass.Should().Be(HttpStatusClass.ClientError);
    }

    [Fact]
    public async Task TryToStringAsync_Should_Return_Read_Failure_With_Server_Error_Status()
    {
        var response = Substitute.For<IFlurlResponse>();
        var exception = CreateReadException();
        response.StatusCode.Returns(503);
        response.GetStringAsync().Returns(Task.FromException<string>(exception));

        var result = await response.TryToStringAsync();

        result.ResponseReceived.Should().BeTrue();
        result.HttpStatus.Should().Be(503);
        result.StatusClass.Should().Be(HttpStatusClass.ServerError);
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Kind.Should().Be(HttpFailureKind.ResponseRead);
        result.Failure.Exception.Should().BeSameAs(exception);
        response.Received(1).Dispose();
    }

    [Fact]
    public async Task TryToStringAsync_Should_Propagate_Cancellation()
    {
        var responseTask = Task.FromException<IFlurlResponse>(new OperationCanceledException());

        var read = async () => await responseTask.TryToStringAsync();

        await read.Should().ThrowExactlyAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ToStreamAsync_Should_Dispose_Response_And_Stream_Once_When_Wrapper_Is_Disposed_Repeatedly()
    {
        var response = Substitute.For<IFlurlResponse>();
        var stream = new TrackingStream();
        response.StatusCode.Returns(200);
        response.GetStreamAsync().Returns(Task.FromResult<Stream>(stream));

        var result = await response.ToStreamAsync();

        result.Dispose();
        stream.DisposeCount.Should().Be(1);
        response.Received(1).Dispose();

        result.Dispose();

        stream.DisposeCount.Should().Be(1);
        response.Received(1).Dispose();
    }

    [Fact]
    public async Task ToStreamAsync_Should_Dispose_Response_When_GetStream_Fails()
    {
        var response = Substitute.For<IFlurlResponse>();
        response.GetStreamAsync().Returns(Task.FromException<Stream>(CreateReadException()));

        var read = async () => await response.ToStreamAsync();

        await read.Should().ThrowExactlyAsync<InvalidOperationException>();
        response.Received(1).Dispose();
    }

    [Fact]
    public async Task TryToStreamAsync_Should_Transfer_Response_And_Stream_Ownership_To_Result()
    {
        var response = Substitute.For<IFlurlResponse>();
        var stream = new TrackingStream();
        response.StatusCode.Returns(200);
        response.GetStreamAsync().Returns(Task.FromResult<Stream>(stream));

        var result = await response.TryToStreamAsync();

        result.IsSuccess.Should().BeTrue();
        result.Dispose();
        result.Dispose();

        stream.DisposeCount.Should().Be(1);
        response.Received(1).Dispose();
    }

    private static InvalidOperationException CreateReadException() => new("Response body read failed.");

    private sealed record TestPayload;

    private sealed class TrackingStream : MemoryStream
    {
        public int DisposeCount { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;

            base.Dispose(disposing);
        }
    }
}
