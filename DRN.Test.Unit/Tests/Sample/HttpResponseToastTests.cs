using System.Text.Json;
using Sample.Hosted.Extensions;

namespace DRN.Test.Unit.Tests.Sample;

public class HttpResponseToastTests
{
    private const string TriggerHeader = "HX-Trigger";

    [Fact]
    public void SendToast_serializes_the_event_without_an_optional_duration()
    {
        var response = new DefaultHttpContext().Response;

        response.SendToastSuccess("Saved");

        using var json = JsonDocument.Parse(response.Headers[TriggerHeader].ToString());
        var toast = json.RootElement.GetProperty("showToast");
        toast.GetProperty("type").GetString().Should().Be("success");
        toast.GetProperty("message").GetString().Should().Be("Saved");
        toast.TryGetProperty("duration", out _).Should().BeFalse();
    }

    [Fact]
    public void SendToast_preserves_plain_event_names()
    {
        var response = new DefaultHttpContext().Response;
        response.Headers[TriggerHeader] = "profileUpdated, refreshGrid";

        response.SendToastInfo("Saved");

        using var json = JsonDocument.Parse(response.Headers[TriggerHeader].ToString());
        json.RootElement.GetProperty("profileUpdated").ValueKind.Should().Be(JsonValueKind.Object);
        json.RootElement.GetProperty("refreshGrid").ValueKind.Should().Be(JsonValueKind.Object);
        json.RootElement.GetProperty("showToast").GetProperty("message").GetString().Should().Be("Saved");
    }

    [Fact]
    public void SendToast_replaces_the_toast_and_preserves_existing_json_event_details()
    {
        var response = new DefaultHttpContext().Response;
        response.Headers[TriggerHeader] = """{"profileUpdated":{"target":"#profile","version":3},"showToast":{"message":"Old"}}""";

        response.SendToastWarning("New", 9000);

        using var json = JsonDocument.Parse(response.Headers[TriggerHeader].ToString());
        var events = json.RootElement;
        events.EnumerateObject().Select(entry => entry.Name).Should().Equal("showToast", "profileUpdated");
        events.GetProperty("profileUpdated").GetProperty("target").GetString().Should().Be("#profile");
        events.GetProperty("profileUpdated").GetProperty("version").GetInt32().Should().Be(3);
        var toast = events.GetProperty("showToast");
        toast.GetProperty("type").GetString().Should().Be("warning");
        toast.GetProperty("message").GetString().Should().Be("New");
        toast.GetProperty("duration").GetInt32().Should().Be(9000);
    }

    [Fact]
    public void SendToast_preserves_the_target_inherited_from_the_old_toast()
    {
        var response = new DefaultHttpContext().Response;
        response.Headers[TriggerHeader] = """
            {"before":{"target":"#before"},"showToast":{"target":"#profile"},"refreshGrid":{"version":3},"after":{"target":"#after"}}
            """;

        response.SendToastSuccess("Saved");

        using var json = JsonDocument.Parse(response.Headers[TriggerHeader].ToString());
        var events = json.RootElement;
        events.EnumerateObject().Select(entry => entry.Name).Should().Equal("showToast", "before", "refreshGrid", "after");
        events.GetProperty("showToast").TryGetProperty("target", out _).Should().BeFalse();
        events.GetProperty("before").GetProperty("target").GetString().Should().Be("#before");
        events.GetProperty("refreshGrid").GetProperty("target").GetString().Should().Be("#profile");
        events.GetProperty("refreshGrid").GetProperty("version").GetInt32().Should().Be(3);
        events.GetProperty("after").GetProperty("target").GetString().Should().Be("#after");
    }

    [Fact]
    public void SendToast_preserves_the_inherited_target_and_value_for_non_object_details()
    {
        var response = new DefaultHttpContext().Response;
        response.Headers[TriggerHeader] = """{"showToast":{"target":"#profile"},"refreshGrid":3}""";

        response.SendToastSuccess("Saved");

        using var json = JsonDocument.Parse(response.Headers[TriggerHeader].ToString());
        var refreshGrid = json.RootElement.GetProperty("refreshGrid");
        refreshGrid.GetProperty("target").GetString().Should().Be("#profile");
        refreshGrid.GetProperty("value").GetInt32().Should().Be(3);
    }

    [Fact]
    public void SendToast_replaces_malformed_trigger_json_with_a_toast_only_header()
    {
        var response = new DefaultHttpContext().Response;
        response.Headers[TriggerHeader] = """{"refreshGrid":{},"unfinished":""";

        response.SendToastSuccess("Saved");

        using var json = JsonDocument.Parse(response.Headers[TriggerHeader].ToString());
        var events = json.RootElement;
        events.EnumerateObject().Select(entry => entry.Name).Should().Equal("showToast");
        var toast = events.GetProperty("showToast");
        toast.GetProperty("type").GetString().Should().Be("success");
        toast.GetProperty("message").GetString().Should().Be("Saved");
    }
}
