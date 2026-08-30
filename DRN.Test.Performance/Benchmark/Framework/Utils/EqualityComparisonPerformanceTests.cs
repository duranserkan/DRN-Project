using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Models;

namespace DRN.Test.Performance.Benchmark.Framework.Utils;

public class EqualityComparisonPerformanceTests(ITestOutputHelper output)
{
#if !DEBUG
    [Fact] //should run on release build
#endif
    public void Run_Benchmarks()
    {
        var logger = new AccumulationLogger();
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddLogger(logger)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);
        var summary = BenchmarkRunner.Run<EqualityComparisonBenchmark>(config);
        summary.Reports.Should().NotBeEmpty();

        output.WriteLine("===================================");
        output.WriteLine("Benchmark Results Path");
        output.WriteLine("===================================");
        output.WriteLine(summary.ResultsDirectoryPath);
        output.WriteLine("===================================");
        output.WriteLine("Benchmark Logs");
        output.WriteLine("===================================");

        var log = logger.GetLog();
        var lines = log.Split(Environment.NewLine);
        foreach (var line in lines)
            output.WriteLine(line);
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[WarmupCount(20)]
[IterationCount(20)]
[InvocationCount(100_000)]
[SuppressMessage("ReSharper", "UnusedTypeParameter")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedParameter.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "RedundantCast")]
[SuppressMessage("ReSharper", "RedundantNameQualifier")]
[SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
public class EqualityComparisonBenchmark
{
    // ==========================================
    // 1. Test Payloads & Pre-allocated References
    // ==========================================

    private const long Long1 = 987654321012345678L;
    private const long Long2 = 987654321012345678L;

    private static readonly string StringA = "SampleMethodNameForReflectionLookups";
    private static readonly string StringSameRef = StringA;
    private static readonly string StringDiffRefEqualVal = new(StringA.ToCharArray());

    private static readonly Type TypeA = typeof(EqualityComparisonBenchmark);
    private static readonly Type TypeASameRef = typeof(EqualityComparisonBenchmark);
    private static readonly Type TypeB = typeof(MethodUtils);

    private static readonly MethodInfo MethodA = typeof(EqualityComparisonBenchmark).GetMethod(nameof(SampleMethod), [typeof(string)])!;
    private static readonly MethodInfo MethodASameRef = MethodA;
    private static readonly MethodInfo MethodB = typeof(EqualityComparisonBenchmark).GetMethod(nameof(SampleMethod2), [typeof(string)])!;

    // --- MethodCacheKey Pre-allocated Instances ---

    private static readonly MethodCacheKey KeyNonGeneric1 = new(typeof(EqualityComparisonBenchmark), nameof(SampleMethod), 1, BindingFlags.Public | BindingFlags.Static);
    private static readonly MethodCacheKey KeyNonGeneric2 = new(typeof(EqualityComparisonBenchmark), nameof(SampleMethod), 1, BindingFlags.Public | BindingFlags.Static);
    private static readonly MethodCacheKey KeyNonGenericDiffName = new(typeof(EqualityComparisonBenchmark), nameof(SampleMethod2), 1, BindingFlags.Public | BindingFlags.Static);
    private static readonly MethodCacheKey KeyNonGenericDiffParams = new(typeof(EqualityComparisonBenchmark), nameof(SampleMethod), 2, BindingFlags.Public | BindingFlags.Static);

    private static readonly Type[] TypeArgsShared = [typeof(string), typeof(int), typeof(double), typeof(object)];
    private static readonly Type[] TypeArgsIdenticalArray = [typeof(string), typeof(int), typeof(double), typeof(object)];
    private static readonly Type[] TypeArgsDiffArray = [typeof(string), typeof(int), typeof(double), typeof(long)];

    private static readonly MethodCacheKey KeyGeneric1 = new(typeof(EqualityComparisonBenchmark), nameof(SampleGenericMethod), 1, BindingFlags.Public | BindingFlags.Static, new EquatableSequence<Type>(TypeArgsShared));
    private static readonly MethodCacheKey KeyGeneric2 = new(typeof(EqualityComparisonBenchmark), nameof(SampleGenericMethod), 1, BindingFlags.Public | BindingFlags.Static, new EquatableSequence<Type>(TypeArgsIdenticalArray));
    private static readonly MethodCacheKey KeyGenericDiffTypeArgs = new(typeof(EqualityComparisonBenchmark), nameof(SampleGenericMethod), 1, BindingFlags.Public | BindingFlags.Static, new EquatableSequence<Type>(TypeArgsDiffArray));

    // --- Sequence Pre-allocated Instances ---

    private static readonly EquatableSequence<Type> EqSeqType1 = new(TypeArgsShared);
    private static readonly EquatableSequence<Type> EqSeqType2 = new(TypeArgsIdenticalArray);

    private static readonly EquatableImmutableSequence<Type> EqImmSeqType1 = new(ImmutableArray.Create(TypeArgsShared));
    private static readonly EquatableImmutableSequence<Type> EqImmSeqType2 = new(ImmutableArray.Create(TypeArgsIdenticalArray));

    // --- Multi-Size Int Arrays for SIMD Scaling Tests ---

    private static readonly int[] Ints4A = [10, 20, 30, 40];
    private static readonly int[] Ints4B = [10, 20, 30, 40];
    private static readonly EquatableSequence<int> EqSeqInt4A = new(Ints4A);
    private static readonly EquatableSequence<int> EqSeqInt4B = new(Ints4B);

    private static readonly int[] Ints16A = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
    private static readonly int[] Ints16B = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
    private static readonly EquatableSequence<int> EqSeqInt16A = new(Ints16A);
    private static readonly EquatableSequence<int> EqSeqInt16B = new(Ints16B);

    private static readonly int[] Ints64A = Enumerable.Range(1, 64).ToArray();
    private static readonly int[] Ints64B = Enumerable.Range(1, 64).ToArray();
    private static readonly EquatableSequence<int> EqSeqInt64A = new(Ints64A);
    private static readonly EquatableSequence<int> EqSeqInt64B = new(Ints64B);

    // ==========================================
    // 2. Primitives & Reference Comparisons
    // ==========================================

    // --- Long Comparisons ---

    [Benchmark(Baseline = true, Description = "long: ==")]
    [BenchmarkCategory("Primitives_Long")]
    public bool Long_OperatorEquals() => Long1 == Long2;

    [Benchmark(Description = "long: .Equals()")]
    [BenchmarkCategory("Primitives_Long")]
    public bool Long_InstanceEquals() => Long1.Equals(Long2);

    [Benchmark(Description = "long: EqualityComparer<long>.Default.Equals")]
    [BenchmarkCategory("Primitives_Long")]
    public bool Long_EqualityComparer() => EqualityComparer<long>.Default.Equals(Long1, Long2);

    [Benchmark(Description = "long: object.Equals (Boxed 48 B)")]
    [BenchmarkCategory("Primitives_Long")]
    public bool Long_BoxedObjectEquals() => object.Equals((object)Long1, (object)Long2);

    // --- String Comparisons ---

    [Benchmark(Baseline = true, Description = "string: == (Same Reference)")]
    [BenchmarkCategory("Reference_String")]
    public bool String_OperatorEquals_SameRef() => StringA == StringSameRef;

    [Benchmark(Description = "string: == (Distinct Reference, Equal Value)")]
    [BenchmarkCategory("Reference_String")]
    public bool String_OperatorEquals_DiffRefEqualVal() => StringA == StringDiffRefEqualVal;

    [Benchmark(Description = "string: .Equals(Ordinal)")]
    [BenchmarkCategory("Reference_String")]
    public bool String_EqualsOrdinal() => string.Equals(StringA, StringDiffRefEqualVal, StringComparison.Ordinal);

    [Benchmark(Description = "string: ReferenceEquals")]
    [BenchmarkCategory("Reference_String")]
    public bool String_ReferenceEquals() => ReferenceEquals(StringA, StringSameRef);

    [Benchmark(Description = "string: EqualityComparer<string>.Default.Equals")]
    [BenchmarkCategory("Reference_String")]
    public bool String_EqualityComparer() => EqualityComparer<string>.Default.Equals(StringA, StringDiffRefEqualVal);

    // --- Type Comparisons ---

    [Benchmark(Baseline = true, Description = "Type: ==")]
    [BenchmarkCategory("Reference_Type")]
    public bool Type_OperatorEquals() => TypeA == TypeASameRef;

    [Benchmark(Description = "Type: .Equals()")]
    [BenchmarkCategory("Reference_Type")]
    public bool Type_InstanceEquals() => TypeA.Equals(TypeASameRef);

    [Benchmark(Description = "Type: ReferenceEquals")]
    [BenchmarkCategory("Reference_Type")]
    public bool Type_ReferenceEquals() => ReferenceEquals(TypeA, TypeASameRef);

    [Benchmark(Description = "Type: EqualityComparer<Type>.Default.Equals (Diff Ref)")]
    [BenchmarkCategory("Reference_Type")]
    public bool Type_EqualityComparer() => EqualityComparer<Type>.Default.Equals(TypeA, TypeB);

    // --- MethodInfo Comparisons ---

    [Benchmark(Baseline = true, Description = "MethodInfo: ==")]
    [BenchmarkCategory("Reference_MethodInfo")]
    public bool MethodInfo_OperatorEquals() => MethodA == MethodASameRef;

    [Benchmark(Description = "MethodInfo: .Equals()")]
    [BenchmarkCategory("Reference_MethodInfo")]
    public bool MethodInfo_InstanceEquals() => MethodA.Equals(MethodASameRef);

    [Benchmark(Description = "MethodInfo: ReferenceEquals")]
    [BenchmarkCategory("Reference_MethodInfo")]
    public bool MethodInfo_ReferenceEquals() => ReferenceEquals(MethodA, MethodASameRef);

    [Benchmark(Description = "MethodInfo: EqualityComparer<MethodInfo>.Default.Equals (Diff Ref)")]
    [BenchmarkCategory("Reference_MethodInfo")]
    public bool MethodInfo_EqualityComparer() => EqualityComparer<MethodInfo>.Default.Equals(MethodA, MethodB);

    // ==========================================
    // 3. MethodCacheKey Equality & Hashing
    // ==========================================

    [Benchmark(Baseline = true, Description = "MethodCacheKey: NonGeneric Equal (==)")]
    [BenchmarkCategory("MethodCacheKey_Equality")]
    public bool MethodCacheKey_NonGeneric_Equal() => KeyNonGeneric1 == KeyNonGeneric2;

    [Benchmark(Description = "MethodCacheKey: NonGeneric Diff MethodName (==) [Short-Circuit]")]
    [BenchmarkCategory("MethodCacheKey_Equality")]
    public bool MethodCacheKey_NonGeneric_DiffName() => KeyNonGeneric1 == KeyNonGenericDiffName;

    [Benchmark(Description = "MethodCacheKey: NonGeneric Diff ParameterCount (==) [Short-Circuit]")]
    [BenchmarkCategory("MethodCacheKey_Equality")]
    public bool MethodCacheKey_NonGeneric_DiffParams() => KeyNonGeneric1 == KeyNonGenericDiffParams;

    [Benchmark(Description = "* MethodCacheKey: Generic Equal with EquatableSequence (==)")]
    [BenchmarkCategory("MethodCacheKey_Equality")]
    public bool MethodCacheKey_Generic_Equal() => KeyGeneric1 == KeyGeneric2;

    [Benchmark(Description = "* MethodCacheKey: Generic Diff TypeArgs (==)")]
    [BenchmarkCategory("MethodCacheKey_Equality")]
    public bool MethodCacheKey_Generic_DiffTypeArgs() => KeyGeneric1 == KeyGenericDiffTypeArgs;

    [Benchmark(Baseline = true, Description = "MethodCacheKey: NonGeneric .GetHashCode()")]
    [BenchmarkCategory("MethodCacheKey_Hashing")]
    public int MethodCacheKey_NonGeneric_GetHashCode() => KeyNonGeneric1.GetHashCode();

    [Benchmark(Description = "* MethodCacheKey: Generic .GetHashCode() (EquatableSequence)")]
    [BenchmarkCategory("MethodCacheKey_Hashing")]
    public int MethodCacheKey_Generic_GetHashCode() => KeyGeneric1.GetHashCode();

    // ==========================================
    // 4. Sequence Comparisons: TypeArgs (SIMD vs Alternatives)
    // ==========================================

    [Benchmark(Baseline = true, Description = "* TypeArgs: EquatableSequence<Type>.Equals (SIMD + RefCheck)")]
    [BenchmarkCategory("Sequence_TypeArgs")]
    public bool TypeArgs_EquatableSequence_Equals() => EqSeqType1.Equals(EqSeqType2);

    [Benchmark(Description = "* TypeArgs: EquatableImmutableSequence<Type>.Equals (ImmutableArray + SIMD)")]
    [BenchmarkCategory("Sequence_TypeArgs")]
    public bool TypeArgs_EquatableImmutableSequence_Equals() => EqImmSeqType1.Equals(EqImmSeqType2);

    [Benchmark(Description = "* TypeArgs: ReadOnlySpan<Type>.SequenceEqual (Native SIMD)")]
    [BenchmarkCategory("Sequence_TypeArgs")]
    public bool TypeArgs_Span_SequenceEqual() => TypeArgsShared.AsSpan().SequenceEqual(TypeArgsIdenticalArray.AsSpan());

    [Benchmark(Description = "TypeArgs: For-Loop Non-SIMD Element Comparison")]
    [BenchmarkCategory("Sequence_TypeArgs")]
    public bool TypeArgs_ForLoop_NonSIMD() => LoopEquals(TypeArgsShared, TypeArgsIdenticalArray);

    [Benchmark(Description = "TypeArgs: LINQ Enumerable.SequenceEqual (Non-SIMD)")]
    [BenchmarkCategory("Sequence_TypeArgs")]
    public bool TypeArgs_Linq_SequenceEqual() => TypeArgsShared.SequenceEqual(TypeArgsIdenticalArray);

    [Benchmark(Description = "TypeArgs: IStructuralEquatable.Equals (Non-SIMD)")]
    [BenchmarkCategory("Sequence_TypeArgs")]
    public bool TypeArgs_StructuralEquatable_Equals() =>
        ((IStructuralEquatable)TypeArgsShared).Equals(TypeArgsIdenticalArray, StructuralComparisons.StructuralEqualityComparer);

    // ==========================================
    // 5. Sequence Length Scaling: SIMD vs Non-SIMD
    // ==========================================

    // --- Length 4 ---

    [Benchmark(Baseline = true, Description = "Size 4: EquatableSequence<int>.Equals (SIMD)")]
    [BenchmarkCategory("Sequence_Scaling_Size4")]
    public bool Size4_EquatableSequence_SIMD() => EqSeqInt4A.Equals(EqSeqInt4B);

    [Benchmark(Description = "Size 4: For-Loop Non-SIMD")]
    [BenchmarkCategory("Sequence_Scaling_Size4")]
    public bool Size4_ForLoop_NonSIMD() => LoopEquals(Ints4A, Ints4B);

    [Benchmark(Description = "Size 4: LINQ Enumerable.SequenceEqual")]
    [BenchmarkCategory("Sequence_Scaling_Size4")]
    public bool Size4_Linq_SequenceEqual() => Ints4A.SequenceEqual(Ints4B);

    // --- Length 16 ---

    [Benchmark(Baseline = true, Description = "Size 16: EquatableSequence<int>.Equals (SIMD)")]
    [BenchmarkCategory("Sequence_Scaling_Size16")]
    public bool Size16_EquatableSequence_SIMD() => EqSeqInt16A.Equals(EqSeqInt16B);

    [Benchmark(Description = "Size 16: For-Loop Non-SIMD")]
    [BenchmarkCategory("Sequence_Scaling_Size16")]
    public bool Size16_ForLoop_NonSIMD() => LoopEquals(Ints16A, Ints16B);

    [Benchmark(Description = "Size 16: LINQ Enumerable.SequenceEqual")]
    [BenchmarkCategory("Sequence_Scaling_Size16")]
    public bool Size16_Linq_SequenceEqual() => Ints16A.SequenceEqual(Ints16B);

    // --- Length 64 ---

    [Benchmark(Baseline = true, Description = "* Size 64: EquatableSequence<int>.Equals (SIMD Scaling)")]
    [BenchmarkCategory("Sequence_Scaling_Size64")]
    public bool Size64_EquatableSequence_SIMD() => EqSeqInt64A.Equals(EqSeqInt64B);

    [Benchmark(Description = "Size 64: For-Loop Non-SIMD")]
    [BenchmarkCategory("Sequence_Scaling_Size64")]
    public bool Size64_ForLoop_NonSIMD() => LoopEquals(Ints64A, Ints64B);

    [Benchmark(Description = "Size 64: LINQ Enumerable.SequenceEqual")]
    [BenchmarkCategory("Sequence_Scaling_Size64")]
    public bool Size64_Linq_SequenceEqual() => Ints64A.SequenceEqual(Ints64B);

    // ==========================================
    // 6. Sequence HashCode Generation
    // ==========================================

    [Benchmark(Baseline = true, Description = "* EquatableSequence<int>: .GetHashCode() (Span ref readonly)")]
    [BenchmarkCategory("Sequence_Hashing")]
    public int EquatableSequence_GetHashCode() => EqSeqInt16A.GetHashCode();

    [Benchmark(Description = "* EquatableImmutableSequence<Type>: .GetHashCode()")]
    [BenchmarkCategory("Sequence_Hashing")]
    public int EquatableImmutableSequence_GetHashCode() => EqImmSeqType1.GetHashCode();

    [Benchmark(Description = "IStructuralEquatable: .GetHashCode() (Allocates 192 B)")]
    [BenchmarkCategory("Sequence_Hashing")]
    public int StructuralEquatable_GetHashCode() =>
        ((IStructuralEquatable)Ints16A).GetHashCode(StructuralComparisons.StructuralEqualityComparer);

    [Benchmark(Description = "LINQ Aggregate: HashCode.Combine() (Delegate Overhead)")]
    [BenchmarkCategory("Sequence_Hashing")]
    public int Linq_Aggregate_GetHashCode() => Ints16A.Aggregate(0, HashCode.Combine);

    // ==========================================
    // Helper Methods & Target Reflection Methods
    // ==========================================

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool LoopEquals<T>(T[] a, T[] b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
                return false;
        }
        return true;
    }

    public static void SampleMethod(string a) { }
    public static void SampleMethod(string a, string b) { }
    public static void SampleMethod2(string a) { }
    public static void SampleGenericMethod<T1, T2, T3, T4>(string a) { }
}
