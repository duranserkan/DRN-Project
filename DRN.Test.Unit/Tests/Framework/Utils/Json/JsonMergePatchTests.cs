using System.Text.Json.Nodes;
using AwesomeAssertions;
using DRN.Framework.Utils.Data.Json;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Json;

public class JsonMergePatchTests
{
    // Exact examples from RFC 7396 Appendix A, in published order.
    public static IReadOnlyList<object[]> Rfc7396AppendixACases { get; } =
    [
        ["""{"a":"b"}""", """{"a":"c"}""", """{"a":"c"}"""],
        ["""{"a":"b"}""", """{"b":"c"}""", """{"a":"b","b":"c"}"""],
        ["""{"a":"b"}""", """{"a":null}""", "{}"],
        ["""{"a":"b","b":"c"}""", """{"a":null}""", """{"b":"c"}"""],
        ["""{"a":["b"]}""", """{"a":"c"}""", """{"a":"c"}"""],
        ["""{"a":"c"}""", """{"a":["b"]}""", """{"a":["b"]}"""],
        ["""{"a":{"b":"c"}}""", """{"a":{"b":"d","c":null}}""", """{"a":{"b":"d"}}"""],
        ["""{"a":[{"b":"c"}]}""", """{"a":[1]}""", """{"a":[1]}"""],
        ["""["a","b"]""", """["c","d"]""", """["c","d"]"""],
        ["""{"a":"b"}""", """["c"]""", """["c"]"""],
        ["""{"a":"foo"}""", "null", "null"],
        ["""{"a":"foo"}""", "\"bar\"", "\"bar\""],
        ["""{"e":null}""", """{"a":1}""", """{"e":null,"a":1}"""],
        ["[1,2]", """{"a":"b","c":null}""", """{"a":"b"}"""],
        ["{}", """{"a":{"bb":{"ccc":null}}}""", """{"a":{"bb":{}}}"""]
    ];

    [Fact]
    public void Rfc7396_Appendix_A_Matrix_Should_Contain_All_Published_Cases()
    {
        Rfc7396AppendixACases.Should().HaveCount(15);
    }

    [Theory]
    [DataMemberUnit(nameof(Rfc7396AppendixACases))]
    public void Safe_Merge_Should_Produce_Rfc7396_Appendix_A_Result(
        string targetJson, string patchJson, string expectedJson)
    {
        var target = JsonNode.Parse(targetJson);
        var patch = JsonNode.Parse(patchJson);
        var targetSnapshot = target?.DeepClone();
        var patchSnapshot = patch?.DeepClone();
        var expected = JsonNode.Parse(expectedJson);

        var result = JsonMergePatch.SafeApplyMergePatch(target, patch);

        result.Changed.Should().BeTrue();
        JsonNode.DeepEquals(result.Json, expected).Should().BeTrue();
        JsonNode.DeepEquals(target, targetSnapshot).Should().BeTrue();
        JsonNode.DeepEquals(patch, patchSnapshot).Should().BeTrue();

        if (result.Json is not null && target is not null)
            result.Json.Should().NotBeSameAs(target);
        if (result.Json is not null && patch is not null)
            result.Json.Should().NotBeSameAs(patch);
    }

    [Fact]
    public void Safe_Merge_Should_Reuse_Target_For_Semantic_No_Ops()
    {
        var objectTarget = JsonNode.Parse("""{"value":{"nested":1}}""")!;
        var objectPatch = JsonNode.Parse("""{"value":{"nested":1,"absent":null}}""")!;
        var arrayTarget = JsonNode.Parse("""[1,{"value":true}]""")!;
        var arrayPatch = arrayTarget.DeepClone();
        var primitiveTarget = JsonValue.Create("same")!;
        var primitivePatch = JsonValue.Create("same")!;

        var objectResult = JsonMergePatch.SafeApplyMergePatch(objectTarget, objectPatch);
        var arrayResult = JsonMergePatch.SafeApplyMergePatch(arrayTarget, arrayPatch);
        var primitiveResult = JsonMergePatch.SafeApplyMergePatch(primitiveTarget, primitivePatch);
        var nullResult = JsonMergePatch.SafeApplyMergePatch(null, null);

        objectResult.Changed.Should().BeFalse();
        objectResult.Json.Should().BeSameAs(objectTarget);
        arrayResult.Changed.Should().BeFalse();
        arrayResult.Json.Should().BeSameAs(arrayTarget);
        primitiveResult.Changed.Should().BeFalse();
        primitiveResult.Json.Should().BeSameAs(primitiveTarget);
        nullResult.Changed.Should().BeFalse();
        nullResult.Json.Should().BeNull();
    }

    [Fact]
    public void Safe_Merge_Should_Return_Detached_Result_And_Leave_Inputs_Unchanged()
    {
        var target = JsonNode.Parse("""{"stable":{"value":1},"changed":{"value":1}}""")!;
        var patch = JsonNode.Parse("""{"changed":{"value":2},"added":[1,2]}""")!;
        var targetSnapshot = target.DeepClone();
        var patchSnapshot = patch.DeepClone();
        var targetStable = target["stable"];
        var patchAdded = patch["added"];

        var result = JsonMergePatch.SafeApplyMergePatch(target, patch);

        result.Changed.Should().BeTrue();
        result.Json.Should().NotBeSameAs(target);
        result.Json!["stable"].Should().NotBeSameAs(targetStable);
        result.Json!["added"].Should().NotBeSameAs(patchAdded);
        result.Json!["changed"]!["value"]!.GetValue<int>().Should().Be(2);
        JsonNode.DeepEquals(target, targetSnapshot).Should().BeTrue();
        JsonNode.DeepEquals(patch, patchSnapshot).Should().BeTrue();
    }

    [Fact]
    public void InPlace_Object_Merge_Should_Preserve_Existing_Nested_References()
    {
        var target = JsonNode.Parse("""{"nested":{"value":1},"stable":true}""")!.AsObject();
        var patch = JsonNode.Parse("""{"nested":{"value":2,"added":true}}""")!.AsObject();
        var patchSnapshot = patch.DeepClone();
        var nested = target["nested"]!.AsObject();

        var changed = JsonMergePatch.ApplyObjectMergePatchInPlace(target, patch);

        changed.Should().BeTrue();
        target["nested"].Should().BeSameAs(nested);
        nested["value"]!.GetValue<int>().Should().Be(2);
        nested["added"]!.GetValue<bool>().Should().BeTrue();
        target["stable"]!.GetValue<bool>().Should().BeTrue();
        JsonNode.DeepEquals(patch, patchSnapshot).Should().BeTrue();
    }

    [Fact]
    public void InPlace_Object_Merge_Should_Preserve_References_For_No_Op_Subtrees()
    {
        var target = JsonNode.Parse("""{"nested":{"value":1}}""")!.AsObject();
        var patch = JsonNode.Parse("""{"nested":{"value":1,"absent":null}}""")!.AsObject();
        var nested = target["nested"];

        var changed = JsonMergePatch.ApplyObjectMergePatchInPlace(target, patch);

        changed.Should().BeFalse();
        target["nested"].Should().BeSameAs(nested);
    }

    [Fact]
    public void InPlace_Object_Merge_Should_Handle_Patch_From_Target_Tree()
    {
        var target = JsonNode.Parse("""{"value":1,"remove":null}""")!.AsObject();

        var changed = JsonMergePatch.ApplyObjectMergePatchInPlace(target, target);

        changed.Should().BeTrue();
        target.ContainsKey("remove").Should().BeFalse();
        target["value"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public void InPlace_Object_Merge_Should_Reuse_Same_Tree_For_Semantic_No_Op()
    {
        var target = JsonNode.Parse("""{"value":1,"nested":{"stable":true}}""")!.AsObject();
        var nested = target["nested"];

        var changed = JsonMergePatch.ApplyObjectMergePatchInPlace(target, target);

        changed.Should().BeFalse();
        target["nested"].Should().BeSameAs(nested);
    }

    [Fact]
    public void InPlace_Object_Merge_Should_Merge_New_Objects_Against_Empty_Objects()
    {
        var target = new JsonObject();
        var patch = JsonNode.Parse("""{"new":{"remove":null,"keep":true}}""")!.AsObject();
        var expected = JsonNode.Parse("""{"new":{"keep":true}}""");

        var changed = JsonMergePatch.ApplyObjectMergePatchInPlace(target, patch);

        changed.Should().BeTrue();
        JsonNode.DeepEquals(target, expected).Should().BeTrue();
    }

    [Fact]
    public void Safe_Merge_Should_Enforce_Array_Depth()
    {
        var patch = CreateDeepArray(5);

        var operation = () => JsonMergePatch.SafeApplyMergePatch(new JsonObject(), patch, maxDepth: 4);

        operation.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InPlace_Object_Merge_Should_Prevalidate_Depth_Before_Mutation()
    {
        var target = new JsonObject { ["existing"] = true };
        var patch = new JsonObject
        {
            ["shallow"] = "change",
            ["deep"] = CreateDeepObject(5)
        };

        var operation = () => JsonMergePatch.ApplyObjectMergePatchInPlace(target, patch, maxDepth: 4);

        operation.Should().Throw<InvalidOperationException>();
        target.ContainsKey("shallow").Should().BeFalse();
        target.ContainsKey("deep").Should().BeFalse();
    }

    [Fact]
    public void Safe_Merge_Should_Allow_Depth_At_Maximum()
    {
        var patch = CreateDeepObject(3);

        var result = JsonMergePatch.SafeApplyMergePatch(new JsonObject(), patch, maxDepth: 4);

        result.Changed.Should().BeTrue();
        JsonNode.DeepEquals(result.Json, patch).Should().BeTrue();
    }

    [Fact]
    public void Merge_Methods_Should_Reject_Non_Positive_Max_Depth()
    {
        var target = new JsonObject();
        var patch = new JsonObject();

        var safeOperation = () => JsonMergePatch.SafeApplyMergePatch(target, patch, maxDepth: 0);
        var inPlaceOperation = () => JsonMergePatch.ApplyObjectMergePatchInPlace(target, patch, maxDepth: 0);

        safeOperation.Should().Throw<ArgumentException>();
        inPlaceOperation.Should().Throw<ArgumentException>();
    }

    private static JsonObject CreateDeepObject(int depth)
    {
        var root = new JsonObject();
        var current = root;
        for (var i = 0; i < depth; i++)
        {
            current["child"] = new JsonObject();
            current = current["child"]!.AsObject();
        }

        return root;
    }

    private static JsonArray CreateDeepArray(int depth)
    {
        var root = new JsonArray();
        var current = root;
        for (var i = 0; i < depth; i++)
        {
            var child = new JsonArray();
            current.Add(child);
            current = child;
        }

        return root;
    }
}
