using System.Text.Json.Nodes;
using AwesomeAssertions;
using DRN.Framework.Utils.Data.Json;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Json;

public class JsonMergePatchTests
{
    [Fact]
    public void Json_Should_Be_Merged_With_Patch_Without_New_Copy()
    {
        var original = JsonNode.Parse("""{"a": 1, "b": {"c": 2}}""")!;
        var patch = JsonNode.Parse("""{"b": {"c": 3}}""")!;

        var result = JsonMergePatch.SafeApplyMergePatch(original, patch, true);
        result.Changed.Should().BeTrue();
        
        original["b"]!["c"]!.GetValue<int>().Should().Be(3);
        original.Should().BeEquivalentTo(result.Json);
    }

    [Fact]
    public void Json_Should_Be_Merged_With_Patch_With_New_Copy()
    {
        var original = JsonNode.Parse("""{"a": 1, "b": {"c": 2}}""")!;
        var patch = JsonNode.Parse("""{"b": {"c": 3}}""")!;

        var result = JsonMergePatch.SafeApplyMergePatch(original, patch, false);
        result.Changed.Should().BeTrue();

        original["b"]!["c"]!.GetValue<int>().Should().Be(2);
        original["a"]!.GetValue<int>().Should().Be(1);

        result.Json["b"]!["c"]!.GetValue<int>().Should().Be(3);
        result.Json["a"]!.GetValue<int>().Should().Be(1);

        original.Should().NotBeEquivalentTo(result);
    }

    [Fact]
    public void Json_Should_Be_Merged_With_Array_Patch_Without_New_Copy()
    {
        var original = JsonNode.Parse("""{"x": [1, 2, 3]}""")!;
        var patch = JsonNode.Parse("""{"x": [4, 5]}""")!;

        var result = JsonMergePatch.SafeApplyMergePatch(original, patch, true);
        result.Changed.Should().BeTrue();

        original["x"]!.AsArray().GetValues<int>().Should().BeEquivalentTo([4, 5]);
        original.Should().BeEquivalentTo(result.Json);
    }


    [Fact]
    public void Json_Should_Be_Merged_With_Array_Patch_With_New_Copy()
    {
        var original = JsonNode.Parse("""{"x": [1, 2, 3]}""")!;
        var patch = JsonNode.Parse("""{"x": [4, 5]}""")!;

        var result = JsonMergePatch.SafeApplyMergePatch(original, patch, false);
        result.Changed.Should().BeTrue();

        original["x"]!.AsArray().GetValues<int>().Should().BeEquivalentTo([1, 2, 3]);
        result.Json["x"]!.AsArray().GetValues<int>().Should().BeEquivalentTo([4, 5]);

        original.Should().NotBeEquivalentTo(result.Json);
    }

    [Fact]
    public void Json_Should_Be_Merged_With_Patch_That_Changes_Type()
    {
        var newValue = 42;
        var original = new JsonObject { ["value"] = "text" };
        var patch = new JsonObject { ["value"] = newValue };

        var result = JsonMergePatch.SafeApplyMergePatch(original, patch, changeOriginal: true);
        result.Changed.Should().BeTrue();

        original["value"]!.GetValue<int>().Should().Be(newValue);
        original.Should().BeEquivalentTo(result.Json);
    }

    [Fact]
    public void Json_Should_Be_Merged_With_Patch_That_Has_Null_Value()
    {
        var original = new JsonObject
        {
            ["a"] = new JsonObject
            {
                ["b"] = new JsonObject
                {
                    ["c"] = "value"
                }
            }
        };

        var patch = new JsonObject { ["a"] = new JsonObject { ["b"] = null } };
        var result = JsonMergePatch.SafeApplyMergePatch(original, patch, changeOriginal: false);
        result.Changed.Should().BeTrue();

        result.Json["a"].Should().NotBeNull();
        result.Json["a"]!["b"].Should().BeNull();
    }

    [Fact]
    public void Patch_Should_Replace_Non_Object()
    {
        var original = JsonValue.Create("simple string");
        var patch = new JsonObject { ["new"] = "value" };

        var result = JsonMergePatch.SafeApplyMergePatch(original, patch, changeOriginal: false);
        result.Changed.Should().BeTrue();

        result.Json["new"]!.ToString().Should().BeEquivalentTo(patch["new"]!.ToString());
    }

    //todo: add aditional non changing results
    [Fact]
    public void Empty_Patch_Should_Not_Change_Original()
    {
        var original = new JsonObject { ["data"] = "original" };
        var patch = new JsonObject();

        var result = JsonMergePatch.SafeApplyMergePatch(original, patch, changeOriginal: true);
        result.Changed.Should().BeFalse();

        result.Json["data"]!.ToString().Should().Be("original");
        original.Should().BeEquivalentTo(result.Json);
    }

    [Fact]
    public void Patch_Should_Support_Multiple_Null_Operations()
    {
        var original = new JsonObject
        {
            ["keep"] = "value",
            ["remove1"] = "exists",
            ["remove2"] = "exists"
        };

        var patch = new JsonObject
        {
            ["remove1"] = null,
            ["remove2"] = null,
            ["nonexistent"] = null
        };

        var result = JsonMergePatch.SafeApplyMergePatch(original, patch, changeOriginal: false);
        result.Changed.Should().BeTrue();
        
        result.Json["keep"]!.ToString().Should().Be("value");
        result.Json["remove1"].Should().BeNull();
        result.Json["remove2"].Should().BeNull();
        result.Json["nonexistent"].Should().BeNull();
    }

    [Fact]
    public void Patch_Replace_Root_Level_Primitive()
    {
        var original = JsonValue.Create(42);
        var patch = JsonValue.Create("forty-two");

        var result = JsonMergePatch.SafeApplyMergePatch(original, patch, changeOriginal: false);
        result.Changed.Should().BeTrue();

        result.Json.ToString().Should().Be("forty-two");
    }

    [Fact]
    public void Patch_Should_Support_Max_Depth_Enforcement()
    {
        var original = CreateDeepObject(50); // 50-level nested object
        var patch = CreateDeepObject(70); // Exceeds maxDepth

        var operation = () => JsonMergePatch.SafeApplyMergePatch(original, patch, false, maxDepth: 40);
        operation.Should().Throw<InvalidOperationException>();

        JsonNode CreateDeepObject(int depth)
        {
            var obj = new JsonObject();
            var current = obj;
            for (var i = 0; i < depth; i++)
            {
                current["child"] = new JsonObject();
                current = current["child"]!.AsObject();
            }

            return obj;
        }
    }
}