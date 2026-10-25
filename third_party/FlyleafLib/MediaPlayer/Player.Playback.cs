using FlyleafLib.MediaFramework.MediaDecoder;

namespace FlyleafLib.MediaPlayer;

partial class Player
{
    string stoppedWithError = null;

    /// <summary>
    /// Fires on playback stopped by an error or completed / ended successfully <see cref="Status"/>
    /// Warning: Uses Invoke and it comes from playback thread so you can't pause/stop etc. You need to use another thread if you have to.
    /// </summary>
    public event EventHandler<PlaybackStoppedArgs> PlaybackStopped;
    protected virtual void OnPlaybackStopped(string error = null)
    {
        if (error != null && lastError == null)
        {
            lastError = error;
            UI(() => LastError = lastError);
        }

        PlaybackStopped?.Invoke(this, new PlaybackStoppedArgs(error));
    }

    /// <summary>
    /// Fires on seek completed for the specified ms (ms will be -1 on failure)
    /// </summary>
    public event EventHandler<int> SeekCompleted;

    /// <summary>
    /// Plays AVS streams
    /// </summary>
    public void Play()
    {
        lock (lockActions)
        {
            if (!canPlay || status == Status.Playing || status == Status.Ended)
                return;

            status = Status.Playing;
            UI(() => Status = status);
        }

        while (taskPlayRuns || taskSeekRuns) Thread.Sleep(5);
        taskPlayRuns = true;

        Thread t = new(PlayThread)
        {
            #if DEBUG
            Name            = $"[#{PlayerId}] Playback",
            #endif
            Priority        = Config.Player.ThreadPriority,
            IsBackground    = true
        };

        t.Start();
    }

    void PlayThread()
    {
        try
        {
            Engine.TimeBeginPeriod1();
            Engine.ThreadExecutionStateBegin();

            onBufferingStarted   = 0;
            onBufferingCompleted = 0;
            requiresBuffering    = true;

            if (lastError != null)
            {
                lastError = null;
                UI(() => LastError = lastError);
            }

            if (Config.Player.Usage == Usage.Audio || !Video.IsOpened)
                ScreamerAudioOnly();
            else
            {
                if (Config.Player.ZeroLatency)
                    ScreamerZeroLatency();
                else if (ReversePlayback)
                {
                    shouldFlushNext = true;
                    ScreamerReverse();
                }
                else
                {
                    shouldFlushPrev = true;
                    ScreamerVASD();
                }

                vFrame = null;
            }

        }
        catch (Exception e)
        {
            Log.Error($"Playback failed ({e.Message})");
        }
        finally
        {
            sFrame = null;

            if (status == Status.Stopped)
                decoder?.Initialize();
            else if (decoder != null)
            {
                decoder.PauseOnQueueFull();
                decoder.PauseDecoders();
            }

            Audio.ClearBuffer();
            Engine.TimeEndPeriod1();
            Engine.ThreadExecutionStateEnd();
            stoppedWithError = null;

            if (status == Status.Playing)
            {
                if (decoderHasEnded)
                    status = Status.Ended;
                else
                {
                    if (Video.IsOpened && VideoDemuxer.Interrupter.Timedout)
                        stoppedWithError = "Timeout";
                    else if (onBufferingStarted - 1 == onBufferingCompleted)
                    {
                        stoppedWithError = "Playback stopped unexpectedly";
                        OnBufferingCompleted("Buffering failed");
                    }
                    else
                    {
                        if (!ReversePlayback)
                        {
                            if (isLive || Math.Abs(duration - curTime) > 3 * 1000 * 10000)
                                stoppedWithError = "Playback stopped unexpectedly";
                        }
                        else if (curTime > 3 * 1000 * 10000)
                            stoppedWithError = "Playback stopped unexpectedly";
                    }

                    status = Status.Paused;
                }
            }

            OnPlaybackStopped(stoppedWithError);
            if (CanDebug) Log.Debug($"[SCREAMER] Finished (Status: {status}, Error: {stoppedWithError})");

            UI(() =>
            {
                Status = status;
                SetCurTime();
            });

            taskPlayRuns = false;
            StartSeekTaskIfNeeded();
        }
    }

    /// <summary>
    /// Pauses AVS streams
    /// </summary>
    public void Pause()
    {
        lock (lockActions)
        {
            if (!canPlay || status == Status.Ended)
                return;

            status = Status.Paused;
            UI(() => Status = status);

            while (taskPlayRuns) Thread.Sleep(5);
            StartSeekTaskIfNeeded();
        }
    }

    public void TogglePlayPause()
    {
        if (status == Status.Playing)
            Pause();
        else
            Play();
    }

    public void ToggleReversePlayback()
        => ReversePlayback = !ReversePlayback;

    public void ToggleLoopPlayback()
        => LoopPlayback = !LoopPlayback;

    /// <summary>
    /// Seeks backwards or forwards based on the specified ms to the nearest keyframe
    /// </summary>
    /// <param name="ms"></param>
    /// <param name="forward"></param>
    public void Seek(int ms, bool forward = false) => Seek(ms, forward, false, SeekAccurateMode);

    /// <summary>
    /// Seeks at the exact timestamp (with half frame distance accuracy) using current SeekAccurateMode
    /// </summary>
    /// <param name="ms"></param>
    public void SeekAccurate(int ms)
        => Seek(ms, false, !IsLive, SeekAccurateMode);

    public void ToggleSeekAccurate()
        => Config.Player.SeekAccurate = !Config.Player.SeekAccurate;

    private void Seek(int ms, bool forward, bool accurate, SeekAccurateMode? mode = null)
    {
        if (!canPlay)
            return;

        var effectiveMode = mode ?? SeekAccurateMode;
        _CurTime = curTime = ms * (long)10000;
        seeks.Enqueue(ms, forward, accurate, effectiveMode);

        // TBR: We consider UI here? (_CurTime loses sync)
        // BufferedDuration = 0; Can't se this as it reads the demuxer's (which we didn't seek yet..)
        Raise(nameof(CurTime));

        if (status == Status.Playing)
            return;

        StartSeekTaskIfNeeded();
    }

    private void StartSeekTaskIfNeeded()
    {
        lock (seeks.SyncRoot)
        {
            if (seeks.IsEmpty || taskSeekRuns || status == Status.Playing || !canPlay)
                return;

            taskSeekRuns = true;
        }

        Task.Run(ProcessPausedSeeks);
    }

    private void ProcessPausedSeeks()
    {
        int ret;
        bool wasEnded = false;
        SeekData seekData = null;

        try
        {
            Engine.TimeBeginPeriod1();

            while (true)
            {
                seekData = seeks.TryPopLatestIf(() => canPlay && status != Status.Playing);
                if (seekData == null)
                {
                    lock (seeks.SyncRoot) taskSeekRuns = false;
                    break;
                }

                if (status == Status.Ended)
                {
                    wasEnded = true;
                    status = Status.Paused;
                    UI(() => Status = status);
                }

                if (sFramePrev != null)
                {
                    sFramePrev = null;
                    Renderer.SubsDispose();
                    Subtitles.ClearSubsText();
                }

                if (!Video.IsOpened)
                {
                    if (seeks.IsSuperseded(seekData.intentId) || !CanPlay)
                        continue;

                    if (AudioDecoder.OnVideoDemuxer)
                    {
                        ret = decoder.Seek(seekData.ms, seekData.forward);
                        if (CanWarn && ret < 0)
                            Log.Warn("Seek failed 2");

                        VideoDemuxer.Start();
                        SeekCompleted?.Invoke(this, -1);
                    }
                    else
                    {
                        ret = decoder.SeekAudio(seekData.ms, seekData.forward);
                        if (CanWarn && ret < 0)
                            Log.Warn("Seek failed 3");

                        AudioDemuxer.Start();
                        SeekCompleted?.Invoke(this, -1);
                    }

                    decoder.PauseOnQueueFull();
                    SeekCompleted?.Invoke(this, seekData.ms);
                }
                else
                {
                    int? seekCompletedMs = null;

                    lock (stepSeekLock)
                    {
                        if (seeks.IsSuperseded(seekData.intentId) || !CanPlay)
                            continue;

                        decoder.PauseDecoders();
                        ret = seekData.accurate ? ExecuteAccurateSeek(seekData) : decoder.Seek(seekData.ms, seekData.forward, true);
                        if (seeks.IsSuperseded(seekData.intentId))
                            continue;

                        if (seekData.accurate && (ret == -2 || !seeks.IsEmpty))
                            continue;

                        if (ret < 0)
                        {
                            if (CanWarn) Log.Warn("Seek failed");
                            seekCompletedMs = -1;
                        }
                        else if (!ReversePlayback && CanPlay)
                        {
                            if (!seekData.accurate) decoder.GetVideoFrame(-1);
                            ResetFrameStats();
                            ShowOneFrame();
                            VideoDemuxer.Start();
                            AudioDemuxer.Start();
                            SubtitlesDemuxer.Start();
                            DataDemuxer.Start();
                            decoder.PauseOnQueueFull();
                            seekCompletedMs = seekData.ms;
                        }
                    }

                    if (seekCompletedMs is int seekCompletedMsValue && !seeks.IsSuperseded(seekData.intentId))
                        SeekCompleted?.Invoke(this, seekCompletedMsValue);
                }

                Thread.Sleep(20);
            }
        }
        catch (Exception e)
        {
            lock (seeks.SyncRoot) taskSeekRuns = false;
            Log.Error($"Seek failed ({e.Message})");
        }
        finally
        {
            decoder.OpenedPlugin?.OnBufferingCompleted();
            Engine.TimeEndPeriod1();
            if ((wasEnded && Config.Player.AutoPlay) || stoppedWithError != null) // TBR: Possible race condition with if (Status == Status.Playing)
                Play();
        }
    }

    /// <summary>
    /// Flushes the buffer (demuxers (packets) and decoders (frames))
    /// This is useful mainly for live streams to push the playback at very end (low latency)
    /// </summary>
    public void Flush()
        => decoder.Flush();

    /// <summary>
    /// Closes AVS media while preserving the renderer for a subsequent Open.
    /// </summary>
    public void CloseMedia()
    {
        lock (lockActions)
        {
            Initialize();
            Renderer?.ClearScreen(force: true);
            Renderer?.ReleaseVideoFrames();
        }
    }

    /// <summary>
    /// Stops and Closes AVS streams
    /// </summary>
    public void Stop()
    {
        lock (lockActions)
        {
            Initialize();
            Renderer?.Reset();
        }
    }
}

public class PlaybackStoppedArgs : EventArgs
{
    public string   Error       { get; }
    public bool     Success     { get; }

    public PlaybackStoppedArgs(string error)
    {
        Error   = error;
        Success = Error == null;
    }
}

public class SeekData
{
    public int  ms;
    public bool forward;
    public bool accurate;
    public SeekAccurateMode accurateMode;
    public long intentId;
    public SeekData(int ms, bool forward, bool accurate, SeekAccurateMode accurateMode = SeekAccurateMode.Legacy, long intentId = 0)
    {
        this.ms = ms;
        this.forward = forward && !accurate;
        this.accurate = accurate;
        this.accurateMode = accurateMode;
        this.intentId = intentId;
    }
}
