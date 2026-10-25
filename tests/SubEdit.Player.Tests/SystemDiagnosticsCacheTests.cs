using SubEdit.Player.Core.Diagnostics;
using SubEdit.Player.WinForms.Diagnostics;
using Xunit;

namespace SubEdit.Player.Tests;

public class SystemDiagnosticsCacheTests
{
    [Fact]
    public async Task Cache_StartsOnlyOnRequestSharesInFlightWorkAndRetainsCompleteImmutableResult()
    {
        var calls = 0;
        using var release = new ManualResetEventSlim();
        var cache = new SystemDiagnosticsCache(() =>
        {
            Interlocked.Increment(ref calls);
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            return ProgramDiagnosticsDialogTests.SystemInfo();
        });
        Assert.Equal(0, calls);
        var first = cache.GetAsync();
        var second = cache.GetAsync();
        Assert.Same(first, second);
        Assert.False(first.IsCompleted);
        release.Set();
        var result = await first;
        Assert.Same(result, await cache.GetAsync());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task PartialOrFailedRead_IsSafeAndDoesNotBecomeSuccessfulCacheEntry()
    {
        var calls = 0;
        var cache = new SystemDiagnosticsCache(() => ++calls switch
        {
            1 => throw new InvalidOperationException("private detail must not enter a report"),
            2 => new DiagnosticSystemInfo(cpu: "Only CPU"),
            _ => ProgramDiagnosticsDialogTests.SystemInfo()
        });
        Assert.Null((await cache.GetAsync()).Cpu);
        Assert.Equal("Only CPU", (await cache.GetAsync()).Cpu);
        var final = await cache.GetAsync();
        Assert.Same(final, await cache.GetAsync());
        Assert.Equal(3, calls);
    }
}
