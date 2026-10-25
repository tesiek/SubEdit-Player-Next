using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FlyleafLib.MediaPlayer;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

public class FlyleafSeekArbitrationTests
{
    [Fact]
    public async Task F03_ScenarioA_LatestRequestSurvives_WorkerPopAndDrainDoesNotDestroySubsequentRequest()
    {
        var arbitrator = new SeekArbitrator();
        var workerStarted = new ManualResetEventSlim(false);
        var producerPushed = new ManualResetEventSlim(false);
        var executedSeeks = new ConcurrentBag<int>();

        // Step 1: Enqueue Request A
        arbitrator.Enqueue(1000, forward: false, accurate: true, SeekAccurateMode.Conditional);

        // Step 2: Background worker pops Request A
        var workerTask = Task.Run(() =>
        {
            var seekA = arbitrator.TryPopLatest();
            Assert.NotNull(seekA);
            Assert.Equal(1000, seekA!.ms);

            // Signal that worker has popped A
            workerStarted.Set();

            // Wait until producer pushes Request B before proceeding
            producerPushed.Wait(TimeSpan.FromSeconds(5));

            // Check supersession on seekA
            if (!arbitrator.IsSuperseded(seekA.intentId))
            {
                executedSeeks.Add(seekA.ms);
            }

            // Worker pops latest again (which should be Request B)
            var seekB = arbitrator.TryPopLatest();
            if (seekB != null && !arbitrator.IsSuperseded(seekB.intentId))
            {
                executedSeeks.Add(seekB.ms);
            }
        });

        // Step 3: Producer waits for worker to pop A, then pushes Request B
        Assert.True(workerStarted.Wait(TimeSpan.FromSeconds(5)));
        var enqueuedB = arbitrator.Enqueue(2000, forward: false, accurate: true, SeekAccurateMode.Conditional);
        Assert.Equal(2000, enqueuedB.ms);
        producerPushed.Set();

        // Step 4: Await worker completion
        await workerTask;

        // Request A was superseded, Request B survived and executed
        Assert.Single(executedSeeks);
        Assert.Contains(2000, executedSeeks);
        Assert.True(arbitrator.IsEmpty);
    }

    [Fact]
    public void F03_ScenarioB_MultipleSupersession_LatestIntentWinsAndQueueIsDrained()
    {
        var arbitrator = new SeekArbitrator();

        // Producer enqueues A, then B, then C rapidly
        var seekA = arbitrator.Enqueue(1000, forward: false, accurate: true, SeekAccurateMode.Conditional);
        var seekB = arbitrator.Enqueue(2000, forward: false, accurate: true, SeekAccurateMode.Conditional);
        var seekC = arbitrator.Enqueue(3000, forward: false, accurate: true, SeekAccurateMode.Conditional);

        // Intent IDs must be strictly monotonically increasing
        Assert.True(seekA.intentId < seekB.intentId);
        Assert.True(seekB.intentId < seekC.intentId);
        Assert.Equal(seekC.intentId, arbitrator.LatestIntentId);

        // A and B must be recognized as superseded
        Assert.True(arbitrator.IsSuperseded(seekA.intentId));
        Assert.True(arbitrator.IsSuperseded(seekB.intentId));

        // Worker pops the latest request
        var popped = arbitrator.TryPopLatest();
        Assert.NotNull(popped);
        Assert.Equal(3000, popped!.ms);
        Assert.Equal(seekC.intentId, popped.intentId);
        Assert.False(arbitrator.IsSuperseded(popped.intentId));

        // Queue must now be empty (A and B were cleared atomically)
        Assert.True(arbitrator.IsEmpty);
        Assert.Null(arbitrator.TryPopLatest());
    }

    [Fact]
    public void F03_ScenarioC_PauseToPlayHandoff_PendingSeekNotDiscardedWhenStateTransitions()
    {
        var arbitrator = new SeekArbitrator();
        bool isPlaying = false;

        // Producer enqueues seek while player is Paused
        arbitrator.Enqueue(5000, forward: false, accurate: true, SeekAccurateMode.Conditional);

        // State changes to Playing concurrently
        isPlaying = true;

        // Paused worker attempts to pop if paused: predicate returns false
        var pausedPopped = arbitrator.TryPopLatestIf(() => !isPlaying);
        Assert.Null(pausedPopped);
        Assert.False(arbitrator.IsEmpty); // Seek is preserved in queue

        // Playing screamer worker pops the pending seek
        var playingPopped = arbitrator.TryPopLatest();
        Assert.NotNull(playingPopped);
        Assert.Equal(5000, playingPopped!.ms);
        Assert.True(arbitrator.IsEmpty);
    }

    [Fact]
    public void F03_ScenarioD_NoDuplicateExecution_PoppedSeekNeverReExecuted()
    {
        var arbitrator = new SeekArbitrator();
        var popCount = 0;

        arbitrator.Enqueue(7000, forward: false, accurate: true, SeekAccurateMode.Conditional);

        // First consumer pops
        var first = arbitrator.TryPopLatest();
        if (first != null) popCount++;

        // Second consumer tries to pop
        var second = arbitrator.TryPopLatest();
        if (second != null) popCount++;

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(1, popCount);
        Assert.True(arbitrator.IsEmpty);
    }

    [Fact]
    public void F05_ScenarioE_SeekThenFrameStep_InFlightSeekIsSupersededAndCannotOverruleFrameStep()
    {
        var arbitrator = new SeekArbitrator();
        long currentVideoPosition = 10_000; // t = 10,000ms

        // Step 1: User initiates Seek to 50,000ms
        var seekReq = arbitrator.Enqueue(50000, forward: false, accurate: true, SeekAccurateMode.Conditional);

        // Paused worker pops seekReq
        var poppedSeek = arbitrator.TryPopLatest();
        Assert.NotNull(poppedSeek);

        // Step 2: User presses Frame-Step Next while seek is in-flight (waiting for decoder/lock)
        // Frame-step triggers InvalidateAndClear
        arbitrator.InvalidateAndClear();
        // Frame-step updates position to next frame (e.g., 10,040ms)
        currentVideoPosition = 10040;

        // Step 3: Seek worker acquires decoder lock and checks supersession
        if (!arbitrator.IsSuperseded(poppedSeek!.intentId))
        {
            // Should NOT execute
            currentVideoPosition = poppedSeek.ms;
        }

        // Final position must remain 10,040ms (frame-step position), not overwritten by 50,000ms
        Assert.Equal(10040, currentVideoPosition);
    }

    [Fact]
    public void F05_ScenarioF_FrameStepDirection_BothNextAndPrevInvalidatePendingAndInFlightSeeks()
    {
        var arbitrator = new SeekArbitrator();

        // 1. Test forward frame step invalidation
        var seekNext = arbitrator.Enqueue(15000, forward: false, accurate: true, SeekAccurateMode.Conditional);
        Assert.False(arbitrator.IsSuperseded(seekNext.intentId));

        // ShowFrameNext invalidation
        arbitrator.InvalidateAndClear();
        Assert.True(arbitrator.IsSuperseded(seekNext.intentId));
        Assert.True(arbitrator.IsEmpty);

        // 2. Test backward frame step invalidation
        var seekPrev = arbitrator.Enqueue(8000, forward: false, accurate: true, SeekAccurateMode.Conditional);
        Assert.False(arbitrator.IsSuperseded(seekPrev.intentId));

        // ShowFramePrev invalidation
        arbitrator.InvalidateAndClear();
        Assert.True(arbitrator.IsSuperseded(seekPrev.intentId));
        Assert.True(arbitrator.IsEmpty);
    }

    [Fact]
    public void F05_ScenarioG_RapidTransition_SeekThenFrameStepThenNewSeek_LatestSeekWins()
    {
        var arbitrator = new SeekArbitrator();
        long committedPosition = 0;

        // 1. User seeks to 1000
        var seek1 = arbitrator.Enqueue(1000, forward: false, accurate: true, SeekAccurateMode.Conditional);

        // 2. User frame-steps (invalidates pending seeks)
        arbitrator.InvalidateAndClear();
        committedPosition = 2040; // frame position

        // 3. User seeks to 3000
        var seek2 = arbitrator.Enqueue(3000, forward: false, accurate: true, SeekAccurateMode.Conditional);

        // Seek 1 is superseded
        Assert.True(arbitrator.IsSuperseded(seek1.intentId));

        // Worker pops latest seek
        var workerSeek = arbitrator.TryPopLatest();
        Assert.NotNull(workerSeek);
        Assert.Equal(3000, workerSeek!.ms);
        Assert.False(arbitrator.IsSuperseded(workerSeek.intentId));

        // Executes Seek 2
        committedPosition = workerSeek.ms;
        Assert.Equal(3000, committedPosition);
        Assert.True(arbitrator.IsEmpty);
    }

    [Fact]
    public async Task Concurrency_HighVolumeSeekProducersAndConsumers_NeverDeadlocksOrCorrupts()
    {
        var arbitrator = new SeekArbitrator();
        const int producerCount = 6;
        const int iterationsPerProducer = 1000;
        var startBarrier = new Barrier(producerCount + 2); // 6 producers + 1 consumer + 1 invalidator
        var cts = new CancellationTokenSource();
        var lastExecutedIntentId = 0L;
        var duplicateExecutionDetected = false;
        var executedIntents = new ConcurrentDictionary<long, byte>();

        // Consumer Task
        var consumerTask = Task.Run(() =>
        {
            startBarrier.SignalAndWait();
            while (!cts.IsCancellationRequested)
            {
                var seek = arbitrator.TryPopLatest();
                if (seek != null)
                {
                    if (!arbitrator.IsSuperseded(seek.intentId))
                    {
                        if (!executedIntents.TryAdd(seek.intentId, 1))
                        {
                            duplicateExecutionDetected = true;
                        }
                        Interlocked.Exchange(ref lastExecutedIntentId, seek.intentId);
                    }
                }
                Thread.Yield();
            }

            // Drain remaining
            while (arbitrator.TryPopLatest() is { } seek)
            {
                if (!arbitrator.IsSuperseded(seek.intentId))
                {
                    if (!executedIntents.TryAdd(seek.intentId, 1))
                    {
                        duplicateExecutionDetected = true;
                    }
                }
            }
        });

        // Frame-step Invalidator Task
        var invalidatorTask = Task.Run(() =>
        {
            startBarrier.SignalAndWait();
            for (int i = 0; i < 100 && !cts.IsCancellationRequested; i++)
            {
                Thread.Sleep(5);
                arbitrator.InvalidateAndClear();
            }
        });

        // Producer Tasks
        var producerTasks = new Task[producerCount];
        for (int p = 0; p < producerCount; p++)
        {
            int producerId = p;
            producerTasks[p] = Task.Run(() =>
            {
                startBarrier.SignalAndWait();
                for (int i = 0; i < iterationsPerProducer; i++)
                {
                    arbitrator.Enqueue(producerId * 10000 + i, false, true, SeekAccurateMode.Conditional);
                }
            });
        }

        // Wait for all producers to finish
        await Task.WhenAll(producerTasks);
        await invalidatorTask;

        // Give consumer a moment to drain then cancel
        await Task.Delay(50);
        cts.Cancel();
        await consumerTask;

        Assert.False(duplicateExecutionDetected);
        Assert.True(arbitrator.IsEmpty);
    }
}
