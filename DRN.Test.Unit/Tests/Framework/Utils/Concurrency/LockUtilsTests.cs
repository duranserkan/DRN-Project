using DRN.Framework.Utils.Concurrency;

namespace DRN.Test.Unit.Tests.Framework.Utils.Concurrency;

public class LockUtilsTests
{
    [Fact]
    public void TryClaimLock_Should_Claim_Once()
    {
        var lockValue = 0;

        LockUtils.TryClaimLock(ref lockValue).Should().BeTrue();
        lockValue.Should().Be(1);
        LockUtils.TryClaimLock(ref lockValue).Should().BeFalse();
    }

    [Fact]
    public void TrySetIfNull_Should_Set_Only_If_Null()
    {
        string? location = null;
        const string value = "expected";

        LockUtils.TrySetIfNull(ref location, value).Should().BeTrue();
        location.Should().Be(value);

        LockUtils.TrySetIfNull(ref location, "other").Should().BeFalse();
        location.Should().Be(value);
    }

    [Fact]
    public void TrySetIfNotNull_Should_Set_Only_If_Not_Null()
    {
        string? location = null;
        const string value = "new";

        LockUtils.TrySetIfNotNull(ref location, value).Should().BeFalse();
        location.Should().BeNull();

        location = "initial";
        LockUtils.TrySetIfNotNull(ref location, value).Should().BeTrue();
        location.Should().Be(value);
    }

    [Fact]
    public void TrySetIfEqual_Should_Set_Only_If_Equal()
    {
        var location = "initial";
        const string value = "new";

        LockUtils.TrySetIfEqual(ref location, value, "wrong").Should().BeFalse();
        location.Should().Be("initial");

        LockUtils.TrySetIfEqual(ref location, value, "initial").Should().BeTrue();
        location.Should().Be(value);
    }

    [Fact]
    public void TrySetIfNotEqual_Should_Set_Only_If_Not_Equal()
    {
        var location = "initial";
        const string value = "new";

        LockUtils.TrySetIfNotEqual(ref location, value, "initial").Should().BeFalse();
        location.Should().Be("initial");

        LockUtils.TrySetIfNotEqual(ref location, value, "other").Should().BeTrue();
        location.Should().Be(value);
    }
    
    [Fact]
    public void TrySetIfNotEqual_With_Null_Comparand_Should_Set_If_Not_Null()
    {
        string? location = null;
        const string value = "new";

        // If comparand is null, it should fail if location is null
        LockUtils.TrySetIfNotEqual(ref location, value, null).Should().BeFalse();
        location.Should().BeNull();

        location = "not-null";
        LockUtils.TrySetIfNotEqual(ref location, value, null).Should().BeTrue();
        location.Should().Be(value);
    }

    [Fact]
    public void ReleaseLock_Should_Release_Claimed_Lock()
    {
        var lockValue = 0;

        // Claim the lock first
        LockUtils.TryClaimLock(ref lockValue).Should().BeTrue();
        lockValue.Should().Be(1);

        // Release the lock
        LockUtils.ReleaseLock(ref lockValue);
        lockValue.Should().Be(0);

        // Lock should be claimable again
        LockUtils.TryClaimLock(ref lockValue).Should().BeTrue();
        lockValue.Should().Be(1);
    }

    [Fact]
    public void TryClaimLock_Should_Allow_Only_One_Thread_To_Claim()
    {
        var lockValue = 0;
        var claimCount = 0;
        const int threadCount = 100;

        Parallel.For(0, threadCount, _ =>
        {
            if (LockUtils.TryClaimLock(ref lockValue))
                Interlocked.Increment(ref claimCount);
        });

        claimCount.Should().Be(1, "only one thread should successfully claim the lock");
        lockValue.Should().Be(1);
    }

    [Fact]
    public void TrySetIfNotEqual_Should_Handle_Concurrent_Modifications()
    {
        var location = "initial";
        var successCount = 0;
        const int threadCount = 100;

        // All threads try to set value if not equal to "blocker"
        // Since location is "initial", all should attempt but only some will succeed
        // due to CAS retry loop - each success changes the value
        Parallel.For(0, threadCount, i =>
        {
            if (LockUtils.TrySetIfNotEqual(ref location, $"value-{i}", "blocker"))
                Interlocked.Increment(ref successCount);
        });

        // All attempts should succeed since location is never "blocker"
        successCount.Should().Be(threadCount, "all threads should succeed since comparand never matches");
        location.Should().StartWith("value-", "final value should be from one of the threads");
    }
}
