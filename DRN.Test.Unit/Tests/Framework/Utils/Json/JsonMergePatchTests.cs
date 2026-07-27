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

        var isSemanticallyEqual = JsonNode.DeepEquals(targetSnapshot, expected);
        result.Changed.Should().Be(!isSemanticallyEqual);
        JsonNode.DeepEquals(result.Json, expected).Should().BeTrue();
        JsonNode.DeepEquals(target, targetSnapshot).Should().BeTrue();
        JsonNode.DeepEquals(patch, patchSnapshot).Should().BeTrue();

        if (result.Json is not null && target is not null && !isSemanticallyEqual)
            result.Json.Should().NotBeSameAs(target);
        if (result.Json is not null && patch is not null)
            result.Json.Should().NotBeSameAs(patch);
    }

    [Theory]
    [DataMemberUnit(nameof(Rfc7396AppendixACases))]
    public void ApplyMergePatchInPlace_Ref_Should_Produce_Rfc7396_Appendix_A_Result(
        string targetJson, string patchJson, string expectedJson)
    {
        var target = JsonNode.Parse(targetJson);
        var patch = JsonNode.Parse(patchJson);
        var targetSnapshot = target?.DeepClone();
        var patchSnapshot = patch?.DeepClone();
        var expected = JsonNode.Parse(expectedJson);

        var changed = JsonMergePatch.ApplyMergePatchInPlace(ref target, patch);

        var isSemanticallyEqual = JsonNode.DeepEquals(targetSnapshot, expected);
        changed.Should().Be(!isSemanticallyEqual);
        JsonNode.DeepEquals(target, expected).Should().BeTrue();
        JsonNode.DeepEquals(patch, patchSnapshot).Should().BeTrue();

        if (target is not null && patch is not null && !isSemanticallyEqual)
        {
            target.Should().NotBeSameAs(patch);
        }
    }

    [Fact]
    public void All_Merge_Methods_Should_Reuse_Target_For_Semantic_No_Ops()
    {
        var objectTarget = JsonNode.Parse("""{"value":{"nested":1}}""")!;
        var objectPatch = JsonNode.Parse("""{"value":{"nested":1,"absent":null}}""")!;
        var arrayTarget = JsonNode.Parse("""[1,{"value":true}]""")!;
        var arrayPatch = arrayTarget.DeepClone();
        var primitiveTarget = JsonValue.Create("same")!;
        var primitivePatch = JsonValue.Create("same")!;

        // 1. SafeApplyMergePatch
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

        // 2. ApplyMergePatchInPlace (ref JsonNode?)
        JsonNode? refObject = objectTarget.DeepClone();
        JsonNode? refArray = arrayTarget.DeepClone();
        JsonNode? refPrimitive = primitiveTarget.DeepClone();
        JsonNode? refNull = null;

        JsonMergePatch.ApplyMergePatchInPlace(ref refObject, objectPatch).Should().BeFalse();
        JsonMergePatch.ApplyMergePatchInPlace(ref refArray, arrayPatch).Should().BeFalse();
        JsonMergePatch.ApplyMergePatchInPlace(ref refPrimitive, primitivePatch).Should().BeFalse();
        JsonMergePatch.ApplyMergePatchInPlace(ref refNull, null).Should().BeFalse();

        // 3. ApplyMergePatchInPlace (JsonObject)
        var objTargetInPlace = objectTarget.DeepClone().AsObject();
        var nestedRef = objTargetInPlace["value"];
        JsonMergePatch.ApplyMergePatchInPlace(objTargetInPlace, objectPatch.AsObject()).Should().BeFalse();
        objTargetInPlace["value"].Should().BeSameAs(nestedRef);
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

        var changed = JsonMergePatch.ApplyMergePatchInPlace(target, patch);

        changed.Should().BeTrue();
        target["nested"].Should().BeSameAs(nested);
        nested["value"]!.GetValue<int>().Should().Be(2);
        nested["added"]!.GetValue<bool>().Should().BeTrue();
        target["stable"]!.GetValue<bool>().Should().BeTrue();
        JsonNode.DeepEquals(patch, patchSnapshot).Should().BeTrue();
    }

    [Fact]
    public void All_Merge_Methods_Should_Handle_Same_Tree_Aliasing()
    {
        var targetJson = """{"value":1,"nested":{"stable":true},"remove":null}""";

        // 1. SafeApplyMergePatch
        var safeTarget = JsonNode.Parse(targetJson)!;
        var safeResult = JsonMergePatch.SafeApplyMergePatch(safeTarget, safeTarget);
        safeResult.Changed.Should().BeTrue();
        safeResult.Json.Should().NotBeSameAs(safeTarget);
        safeResult.Json!.AsObject().ContainsKey("remove").Should().BeFalse();
        safeTarget.AsObject().ContainsKey("remove").Should().BeTrue();

        // 2. ApplyMergePatchInPlace (ref JsonNode?)
        JsonNode? refTarget = JsonNode.Parse(targetJson)!;
        var refChanged = JsonMergePatch.ApplyMergePatchInPlace(ref refTarget, refTarget);
        refChanged.Should().BeTrue();
        refTarget!.AsObject().ContainsKey("remove").Should().BeFalse();

        // 3. ApplyMergePatchInPlace (JsonObject)
        var objTarget = JsonNode.Parse(targetJson)!.AsObject();
        var objChanged = JsonMergePatch.ApplyMergePatchInPlace(objTarget, objTarget);
        objChanged.Should().BeTrue();
        objTarget.ContainsKey("remove").Should().BeFalse();
    }

    [Fact]
    public void All_Merge_Methods_Should_Prevalidate_Depth_Before_Mutation()
    {
        var deepPatchObject = new JsonObject
        {
            ["shallow"] = "change",
            ["deep"] = CreateDeepObject(5)
        };
        var deepPatchArray = CreateDeepArray(5);

        // 1. SafeApplyMergePatch (Object & Array)
        Action safeObjectOp = () => JsonMergePatch.SafeApplyMergePatch(new JsonObject(), deepPatchObject, maxDepth: 4);
        Action safeArrayOp = () => JsonMergePatch.SafeApplyMergePatch(new JsonObject(), deepPatchArray, maxDepth: 4);
        safeObjectOp.Should().Throw<InvalidOperationException>();
        safeArrayOp.Should().Throw<InvalidOperationException>();

        // 2. ApplyMergePatchInPlace (ref JsonNode? Object & Array)
        JsonNode? refTarget = new JsonObject { ["existing"] = true };
        Action refObjectOp = () => JsonMergePatch.ApplyMergePatchInPlace(ref refTarget, deepPatchObject, maxDepth: 4);
        Action refArrayOp = () => JsonMergePatch.ApplyMergePatchInPlace(ref refTarget, deepPatchArray, maxDepth: 4);
        refObjectOp.Should().Throw<InvalidOperationException>();
        refArrayOp.Should().Throw<InvalidOperationException>();
        refTarget.AsObject().ContainsKey("shallow").Should().BeFalse();

        // 3. ApplyMergePatchInPlace (JsonObject)
        var objTarget = new JsonObject { ["existing"] = true };
        Action objOp = () => JsonMergePatch.ApplyMergePatchInPlace(objTarget, deepPatchObject, maxDepth: 4);
        objOp.Should().Throw<InvalidOperationException>();
        objTarget.ContainsKey("shallow").Should().BeFalse();
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
        JsonNode? refTarget = target;

        Action safeOp = () => JsonMergePatch.SafeApplyMergePatch(target, patch, maxDepth: 0);
        Action refOp = () => JsonMergePatch.ApplyMergePatchInPlace(ref refTarget, patch, maxDepth: 0);
        Action objOp = () => JsonMergePatch.ApplyMergePatchInPlace(target, patch, maxDepth: 0);

        safeOp.Should().Throw<ArgumentException>();
        refOp.Should().Throw<ArgumentException>();
        objOp.Should().Throw<ArgumentException>();
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
