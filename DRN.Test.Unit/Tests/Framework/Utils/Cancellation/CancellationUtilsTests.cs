using System.Reflection;
using DRN.Framework.Utils.Cancellation;

namespace DRN.Test.Unit.Tests.Framework.Utils.Cancellation;

public class CancellationUtilsTests
{
    private static readonly CancellationScopeKey FirstKey = CancellationScopeKey.For<FirstScope>();
    private static readonly CancellationScopeKey SecondKey = CancellationScopeKey.For<SecondScope>();
    private static readonly CancellationScopeKey NamedFirstKey = CancellationScopeKey.For<FirstScope>("operation-group");
    private static readonly CancellationScopeKey LookupKey = CancellationScopeKey.For<LookupScope>();

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Root_Merge_Should_Preserve_Stable_Token_Across_Multiple_Sources(bool cancelFirstSource)
    {
        using var cancellation = new CancellationUtils();
        using var firstSource = new CancellationTokenSource();
        using var secondSource = new CancellationTokenSource();
        var root = cancellation.Root;
        var effectiveToken = root.Token;

        root.Merge(firstSource.Token);
        root.Merge(secondSource.Token);
        if (cancelFirstSource)
            firstSource.Cancel();
        else
            secondSource.Cancel();

        cancellation.Root.Should().BeSameAs(root);
        root.Token.Should().Be(effectiveToken);
        effectiveToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void GetOrCreateScope_Should_Return_Same_Scope_And_Token_For_Same_Key()
    {
        using var cancellation = new CancellationUtils();

        var first = cancellation.GetOrCreateScope(FirstKey);
        var token = first.Token;
        var second = cancellation.GetOrCreateScope(FirstKey);

        second.Should().BeSameAs(first);
        second.Token.Should().Be(token);
    }

    [Fact]
    public void Concurrent_GetOrCreateScope_Should_Return_One_Shared_Scope_For_Same_Key()
    {
        const int threadCount = 8;
        using var cancellation = new CancellationUtils();
        using var ready = new CountdownEvent(threadCount);
        using var start = new ManualResetEventSlim();
        var scopes = new ICancellationScope?[threadCount];
        var errors = new Exception?[threadCount];
        var threads = Enumerable.Range(0, threadCount)
            .Select(index => new Thread(() =>
            {
                try
                {
                    ready.Signal();
                    start.Wait();
                    scopes[index] = cancellation.GetOrCreateScope(FirstKey);
                }
                catch (Exception exception)
                {
                    errors[index] = exception;
                }
            })
            {
                IsBackground = true
            })
            .ToArray();

        foreach (var thread in threads)
            thread.Start();

        var allThreadsReady = ready.Wait(TimeSpan.FromSeconds(5));
        start.Set();
        var joinedThreads = threads.Select(thread => thread.Join(TimeSpan.FromSeconds(5))).ToArray();

        allThreadsReady.Should().BeTrue();
        joinedThreads.All(joined => joined).Should().BeTrue();
        errors.Any(error => error is not null).Should().BeFalse();
        scopes.Any(scope => scope is null).Should().BeFalse();
        scopes.All(scope => ReferenceEquals(scope, scopes[0])).Should().BeTrue();
        scopes.Select(scope => scope!.Token).Distinct().Should().ContainSingle();
    }

    [Fact]
    public void Different_Keys_Should_Produce_Isolated_Scopes_And_Child_Cancel_Should_Not_Propagate()
    {
        using var cancellation = new CancellationUtils();
        var first = cancellation.GetOrCreateScope(FirstKey);
        var second = cancellation.GetOrCreateScope(SecondKey);
        var firstToken = first.Token;
        var secondToken = second.Token;

        first.Should().NotBeSameAs(second);
        firstToken.Should().NotBe(secondToken);

        first.Cancel();

        firstToken.IsCancellationRequested.Should().BeTrue();
        secondToken.IsCancellationRequested.Should().BeFalse();
        first.IsCancellationRequested.Should().BeTrue();
        cancellation.Root.IsCancellationRequested.Should().BeFalse();
        second.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Named_Keys_Should_Isolate_Distinct_Groups_Owned_By_The_Same_Type()
    {
        using var cancellation = new CancellationUtils();
        var typeOnly = cancellation.GetOrCreateScope(FirstKey);
        var named = cancellation.GetOrCreateScope(NamedFirstKey);

        named.Should().NotBeSameAs(typeOnly);
        named.Token.Should().NotBe(typeOnly.Token);

        named.Cancel();

        named.IsCancellationRequested.Should().BeTrue();
        typeOnly.IsCancellationRequested.Should().BeFalse();
        cancellation.Root.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Child_Merge_Should_Cancel_Only_That_Child()
    {
        using var cancellation = new CancellationUtils();
        using var externalSource = new CancellationTokenSource();
        var first = cancellation.GetOrCreateScope(FirstKey);
        var second = cancellation.GetOrCreateScope(SecondKey);
        var firstToken = first.Token;

        first.Merge(externalSource.Token);
        externalSource.Cancel();

        first.Token.Should().Be(firstToken);
        firstToken.IsCancellationRequested.Should().BeTrue();
        first.IsCancellationRequested.Should().BeTrue();
        cancellation.Root.IsCancellationRequested.Should().BeFalse();
        second.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Local_Operation_Link_Should_Cancel_Only_The_Linked_Operation()
    {
        using var cancellation = new CancellationUtils();
        using var operationSource = new CancellationTokenSource();
        var child = cancellation.GetOrCreateScope(FirstKey);
        var sibling = cancellation.GetOrCreateScope(SecondKey);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(child.Token, operationSource.Token);

        operationSource.Cancel();

        linkedSource.IsCancellationRequested.Should().BeTrue();
        child.IsCancellationRequested.Should().BeFalse();
        sibling.IsCancellationRequested.Should().BeFalse();
        cancellation.Root.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Root_Cancel_Should_Cancel_All_Existing_Children()
    {
        using var cancellation = new CancellationUtils();
        var first = cancellation.GetOrCreateScope(FirstKey);
        var second = cancellation.GetOrCreateScope(SecondKey);
        var rootToken = cancellation.Root.Token;
        var firstToken = first.Token;
        var secondToken = second.Token;

        cancellation.Root.Cancel();

        rootToken.IsCancellationRequested.Should().BeTrue();
        firstToken.IsCancellationRequested.Should().BeTrue();
        secondToken.IsCancellationRequested.Should().BeTrue();
        cancellation.Root.IsCancellationRequested.Should().BeTrue();
        first.IsCancellationRequested.Should().BeTrue();
        second.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Root_Merge_Should_Propagate_External_Cancellation_To_All_Children()
    {
        using var cancellation = new CancellationUtils();
        using var externalSource = new CancellationTokenSource();
        var first = cancellation.GetOrCreateScope(FirstKey);
        var second = cancellation.GetOrCreateScope(SecondKey);
        var rootToken = cancellation.Root.Token;
        var firstToken = first.Token;
        var secondToken = second.Token;

        cancellation.Root.Merge(externalSource.Token);
        externalSource.Cancel();

        rootToken.IsCancellationRequested.Should().BeTrue();
        firstToken.IsCancellationRequested.Should().BeTrue();
        secondToken.IsCancellationRequested.Should().BeTrue();
        cancellation.Root.IsCancellationRequested.Should().BeTrue();
        first.IsCancellationRequested.Should().BeTrue();
        second.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void GetOrCreateScope_After_Root_Cancellation_Should_Return_Immediately_Canceled_Child()
    {
        using var cancellation = new CancellationUtils();
        cancellation.Root.Cancel();

        var child = cancellation.GetOrCreateScope(FirstKey);

        child.IsCancellationRequested.Should().BeTrue();
        child.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void GetOrCreateScope_Should_Return_Same_Canceled_Scope_For_Existing_Key()
    {
        using var cancellation = new CancellationUtils();
        var child = cancellation.GetOrCreateScope(FirstKey);
        var token = child.Token;
        child.Cancel();

        var lookup = cancellation.GetOrCreateScope(FirstKey);

        lookup.Should().BeSameAs(child);
        lookup.Token.Should().Be(token);
        lookup.IsCancellationRequested.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Merge_Should_Ignore_None_And_The_Scope_Own_Token(bool useRoot)
    {
        using var cancellation = new CancellationUtils();
        var scope = useRoot ? cancellation.Root : cancellation.GetOrCreateScope(FirstKey);
        var token = scope.Token;

        scope.Merge(CancellationToken.None);
        scope.Merge(token);

        scope.Token.Should().Be(token);
        scope.IsCancellationRequested.Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Merge_Should_Propagate_Already_Canceled_Token_Immediately(bool useRoot)
    {
        using var cancellation = new CancellationUtils();
        using var externalSource = new CancellationTokenSource();
        var scope = useRoot ? cancellation.Root : cancellation.GetOrCreateScope(FirstKey);
        var token = scope.Token;
        externalSource.Cancel();

        scope.Merge(externalSource.Token);

        scope.Token.Should().Be(token);
        token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Merge_Should_Deduplicate_Repeated_Tokens_Without_Duplicate_Observable_Callbacks()
    {
        using var cancellation = new CancellationUtils();
        using var externalSource = new CancellationTokenSource();
        var child = cancellation.GetOrCreateScope(FirstKey);
        var callbackCount = 0;
        using var registration = child.Token.Register(() => callbackCount++);

        child.Merge(externalSource.Token);
        child.Merge(externalSource.Token);
        externalSource.Cancel();

        callbackCount.Should().Be(1);
        child.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Cancel_Should_Release_Merged_Token_Registrations_Before_Scope_Disposal()
    {
        var scope = new CancellationScope();
        using var firstSource = new CancellationTokenSource();
        using var secondSource = new CancellationTokenSource();
        scope.Merge(firstSource.Token);
        scope.Merge(secondSource.Token);

        GetMergedRegistrationCount(scope).Should().Be(2);

        scope.Cancel();

        GetMergedRegistrationCount(scope).Should().Be(0);
        Action cancelFirstSource = firstSource.Cancel;
        Action cancelSecondSource = secondSource.Cancel;
        cancelFirstSource.Should().NotThrow();
        cancelSecondSource.Should().NotThrow();
        scope.Dispose();
    }

    [Fact]
    public void Parent_Dispose_During_Child_Cancellation_Should_Defer_Child_Cleanup()
    {
        var cancellation = new CancellationUtils();
        using var externalSource = new CancellationTokenSource();
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        var child = cancellation.GetOrCreateScope(FirstKey);
        var childToken = child.Token;
        child.Merge(externalSource.Token);
        using var registration = childToken.Register(() =>
        {
            callbackEntered.Set();
            releaseCallback.Wait();
        });
        Exception? cancellationException = null;
        var cancellationThread = new Thread(() =>
        {
            try
            {
                externalSource.Cancel();
            }
            catch (Exception exception)
            {
                cancellationException = exception;
            }
        })
        {
            IsBackground = true
        };

        cancellationThread.Start();
        var callbackStarted = callbackEntered.Wait(TimeSpan.FromSeconds(5));

        Exception? disposalException = null;
        try
        {
            cancellation.Dispose();
        }
        catch (Exception exception)
        {
            disposalException = exception;
        }

        releaseCallback.Set();
        var cancellationCompleted = cancellationThread.Join(TimeSpan.FromSeconds(5));

        callbackStarted.Should().BeTrue();
        cancellationCompleted.Should().BeTrue();
        cancellationException.Should().BeNull();
        disposalException.Should().BeNull();
        childToken.IsCancellationRequested.Should().BeTrue();
        Action getChildToken = () => _ = child.Token;
        getChildToken.Should().Throw<ObjectDisposedException>();
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Cancellation_Callbacks_Should_Not_Hold_State_Or_Parent_Dictionary_Locks(bool cancelRoot)
    {
        using var cancellation = new CancellationUtils();
        var child = cancellation.GetOrCreateScope(FirstKey);
        var target = cancelRoot ? cancellation.Root : child;

        AssertCallbacksRunWithoutLocks(cancellation, target, target.Cancel);
    }

    [Fact]
    public void Parent_Dispose_From_Root_Callback_Should_Preserve_Child_Cancellation_Before_Disposal()
    {
        var cancellation = new CancellationUtils();
        var child = cancellation.GetOrCreateScope(FirstKey);
        var rootToken = cancellation.Root.Token;
        var childToken = child.Token;
        using var registration = rootToken.Register(cancellation.Dispose);

        Action cancelRoot = cancellation.Root.Cancel;

        cancelRoot.Should().NotThrow();
        rootToken.IsCancellationRequested.Should().BeTrue();
        childToken.IsCancellationRequested.Should().BeTrue();
        Action getChildToken = () => _ = child.Token;
        getChildToken.Should().Throw<ObjectDisposedException>();
        Action secondDispose = cancellation.Dispose;
        secondDispose.Should().NotThrow();
    }

    [Fact]
    public void Parent_Dispose_From_Child_Callback_Should_Be_Safe_And_Not_Cancel_Root()
    {
        var cancellation = new CancellationUtils();
        var child = cancellation.GetOrCreateScope(FirstKey);
        var rootToken = cancellation.Root.Token;
        var childToken = child.Token;
        using var registration = childToken.Register(cancellation.Dispose);

        Action cancelChild = child.Cancel;

        cancelChild.Should().NotThrow();
        childToken.IsCancellationRequested.Should().BeTrue();
        rootToken.IsCancellationRequested.Should().BeFalse();
        Action getChildToken = () => _ = child.Token;
        getChildToken.Should().Throw<ObjectDisposedException>();
        Action secondDispose = cancellation.Dispose;
        secondDispose.Should().NotThrow();
    }

    [Fact]
    public void Parent_Dispose_Should_Be_Idempotent_And_Dispose_Owned_Scopes()
    {
        var cancellation = new CancellationUtils();
        var root = cancellation.Root;
        var firstChild = cancellation.GetOrCreateScope(FirstKey);
        var secondChild = cancellation.GetOrCreateScope(SecondKey);

        cancellation.Dispose();

        Action secondDispose = cancellation.Dispose;
        secondDispose.Should().NotThrow();
        Action getRootToken = () => _ = root.Token;
        getRootToken.Should().Throw<ObjectDisposedException>();
        Action getFirstChildToken = () => _ = firstChild.Token;
        getFirstChildToken.Should().Throw<ObjectDisposedException>();
        Action getSecondChildToken = () => _ = secondChild.Token;
        getSecondChildToken.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Parent_Dispose_Should_Leave_External_Token_Sources_Usable()
    {
        var cancellation = new CancellationUtils();
        using var rootSource = new CancellationTokenSource();
        using var childSource = new CancellationTokenSource();
        var child = cancellation.GetOrCreateScope(FirstKey);
        cancellation.Root.Merge(rootSource.Token);
        child.Merge(childSource.Token);

        cancellation.Dispose();

        Action cancelRootSource = rootSource.Cancel;
        Action cancelChildSource = childSource.Cancel;
        cancelRootSource.Should().NotThrow();
        cancelChildSource.Should().NotThrow();
        rootSource.IsCancellationRequested.Should().BeTrue();
        childSource.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void GetOrCreateScope_After_Parent_Disposal_Should_Fail_Consistently()
    {
        var cancellation = new CancellationUtils();
        cancellation.Dispose();

        Action firstLookup = () => cancellation.GetOrCreateScope(FirstKey);
        Action secondLookup = () => cancellation.GetOrCreateScope(SecondKey);

        firstLookup.Should().ThrowExactly<ObjectDisposedException>();
        secondLookup.Should().ThrowExactly<ObjectDisposedException>();
    }

    [Fact]
    public void CancellationScopeKey_Factories_Should_Use_Owner_Identity_And_Ordinal_Name_Equality()
    {
        var genericOwner = CancellationScopeKey.For<FirstScope>();
        var runtimeOwner = CancellationScopeKey.For(typeof(FirstScope));
        var genericNamed = CancellationScopeKey.For<FirstScope>("operation-group");
        var runtimeNamed = CancellationScopeKey.For(typeof(FirstScope), "operation-group");

        genericOwner.Should().Be(runtimeOwner);
        genericNamed.Should().Be(runtimeNamed);
        genericNamed.Should().Be(CancellationScopeKey.For<FirstScope>("operation-group"));
        genericNamed.Should().NotBe(CancellationScopeKey.For<FirstScope>("Operation-Group"));
        CancellationScopeKey.For<FirstScope>("\u00E9")
            .Should().NotBe(CancellationScopeKey.For<FirstScope>("e\u0301"));
        genericNamed.Should().NotBe(CancellationScopeKey.For<SecondScope>("operation-group"));
        genericOwner.Should().NotBe(genericNamed);
    }

    [Fact]
    public void CancellationScopeKey_Factories_Should_Reject_Null_Owner_And_Invalid_Names()
    {
        Action nullOwner = () => CancellationScopeKey.For(null!);
        Action nullOwnerWithName = () => CancellationScopeKey.For(null!, "operation-group");

        nullOwner.Should().ThrowExactly<ArgumentNullException>();
        nullOwnerWithName.Should().ThrowExactly<ArgumentNullException>();

        foreach (var invalidName in new string?[] { null, string.Empty, "   " })
        {
            Action genericFactory = () => CancellationScopeKey.For<FirstScope>(invalidName!);
            Action runtimeFactory = () => CancellationScopeKey.For(typeof(FirstScope), invalidName!);

            genericFactory.Should().Throw<ArgumentException>();
            runtimeFactory.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void CancellationScopeKey_Factories_Should_Accept_Maximum_Name_And_Reject_Oversized_Name()
    {
        var maximumName = new string('a', 128);
        var oversizedName = new string('a', 129);
        Action genericMaximum = () => CancellationScopeKey.For<FirstScope>(maximumName);
        Action runtimeMaximum = () => CancellationScopeKey.For(typeof(FirstScope), maximumName);
        Action genericOversized = () => CancellationScopeKey.For<FirstScope>(oversizedName);
        Action runtimeOversized = () => CancellationScopeKey.For(typeof(FirstScope), oversizedName);

        genericMaximum.Should().NotThrow();
        runtimeMaximum.Should().NotThrow();
        genericOversized.Should().Throw<ArgumentException>();
        runtimeOversized.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetOrCreateScope_Should_Reject_Default_Key()
    {
        using var cancellation = new CancellationUtils();

        Action lookup = () => cancellation.GetOrCreateScope(default);

        lookup.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ICancellationScope_Should_Not_Expose_Disposal_Ownership()
        => typeof(IDisposable).IsAssignableFrom(typeof(ICancellationScope)).Should().BeFalse();

    [Fact]
    public void ICancellationUtils_Should_Not_Expose_Unnamed_Scope_Creation()
        => typeof(ICancellationUtils).GetMethod("CreateScope").Should().BeNull();

    [Fact]
    public void Returned_Child_Runtime_Type_Should_Not_Expose_Disposal_Ownership()
    {
        using var cancellation = new CancellationUtils();
        var child = cancellation.GetOrCreateScope(FirstKey);

        typeof(IDisposable).IsAssignableFrom(child.GetType()).Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Parent_Dispose_From_Callback_Triggered_By_Merged_Source_Should_Be_Safe(bool mergeIntoRoot)
    {
        var cancellation = new CancellationUtils();
        using var externalSource = new CancellationTokenSource();
        var child = cancellation.GetOrCreateScope(FirstKey);
        var target = mergeIntoRoot ? cancellation.Root : child;
        var rootToken = cancellation.Root.Token;
        var childToken = child.Token;
        var targetToken = target.Token;
        target.Merge(externalSource.Token);
        using var registration = targetToken.Register(cancellation.Dispose);

        Action cancelExternalSource = externalSource.Cancel;

        cancelExternalSource.Should().NotThrow();
        externalSource.IsCancellationRequested.Should().BeTrue();
        targetToken.IsCancellationRequested.Should().BeTrue();
        childToken.IsCancellationRequested.Should().BeTrue();
        rootToken.IsCancellationRequested.Should().Be(mergeIntoRoot);
        Action getTargetToken = () => _ = target.Token;
        getTargetToken.Should().Throw<ObjectDisposedException>();
        Action secondDispose = cancellation.Dispose;
        secondDispose.Should().NotThrow();
    }

    [Fact]
    public void Internal_CancellationScope_Dispose_Should_Be_Idempotent_And_Leave_External_Source_Usable()
    {
        var scope = new CancellationScope();
        using var externalSource = new CancellationTokenSource();
        scope.Merge(externalSource.Token);

        scope.Dispose();

        Action secondDispose = () => scope.Dispose();
        secondDispose.Should().NotThrow();
        Action cancelExternalSource = externalSource.Cancel;
        cancelExternalSource.Should().NotThrow();
        Action getToken = () => _ = scope.Token;
        getToken.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Root_Cancel_Should_Preserve_Callback_Exception_Behavior_And_Cancel_Children()
    {
        using var cancellation = new CancellationUtils();
        var child = cancellation.GetOrCreateScope(FirstKey);
        using var registration = cancellation.Root.Token.Register(
            () => throw new InvalidOperationException("Expected callback failure."));

        Action cancel = cancellation.Root.Cancel;

        var exception = cancel.Should().Throw<AggregateException>().Which;
        exception.InnerExceptions.Should().Contain(error => error is InvalidOperationException);
        cancellation.Root.IsCancellationRequested.Should().BeTrue();
        child.IsCancellationRequested.Should().BeTrue();
    }

    private static void AssertCallbacksRunWithoutLocks(
        CancellationUtils cancellation,
        ICancellationScope target,
        Action triggerCancellation)
    {
        using var readerReady = new ManualResetEventSlim();
        using var beginRead = new ManualResetEventSlim();
        using var readerCompleted = new ManualResetEventSlim();
        var callbackObservedUnlockedState = false;
        Exception? readerException = null;
        var readerThread = new Thread(() =>
        {
            try
            {
                readerReady.Set();
                beginRead.Wait();
                _ = cancellation.Root.Token;
                _ = target.Token;
                _ = cancellation.GetOrCreateScope(LookupKey).Token;
            }
            catch (Exception exception)
            {
                readerException = exception;
            }
            finally
            {
                readerCompleted.Set();
            }
        })
        {
            IsBackground = true
        };
        readerThread.Start();
        var readerWasReady = readerReady.Wait(TimeSpan.FromSeconds(5));

        using var registration = target.Token.Register(() =>
        {
            beginRead.Set();
            callbackObservedUnlockedState = readerCompleted.Wait(TimeSpan.FromSeconds(5));
        });

        triggerCancellation();

        var readerJoined = readerThread.Join(TimeSpan.FromSeconds(5));
        readerWasReady.Should().BeTrue();
        readerJoined.Should().BeTrue();
        readerException.Should().BeNull();
        callbackObservedUnlockedState.Should().BeTrue();
    }

    private static int GetMergedRegistrationCount(CancellationScope scope)
    {
        var registrationsField = typeof(CancellationScope).GetField("_registrations", BindingFlags.Instance | BindingFlags.NonPublic);
        registrationsField.Should().NotBeNull();
        var registrations = registrationsField!.GetValue(scope).Should()
            .BeAssignableTo<IReadOnlyCollection<CancellationTokenRegistration>>().Subject;

        return registrations.Count;
    }

    private sealed class FirstScope
    {
    }

    private sealed class SecondScope
    {
    }

    private sealed class LookupScope
    {
    }
}
