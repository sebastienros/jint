using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Profiler;
using Jint.DevTools.Session;

namespace Jint.DevTools.Domains;

/// <summary>
/// The <c>Profiler</c> domain: where a run's time went, and which of its code ran at all.
/// </summary>
/// <remarks>
/// <para>
/// Two instruments behind one domain. A <b>profile</b> is a call tree with samples over it, built from
/// <see cref="IProfileSource"/>; <b>coverage</b> is which constructs executed and how often, read from
/// <c>Engine.Diagnostics.GetCoverage</c>. They share nothing but the domain, and each is refused separately
/// when the engine was not built for it: profiling needs <c>Options.Profiling.Enabled</c>, which
/// <c>UseDevTools</c> always sets, and coverage needs <c>Options.Coverage.Enabled</c>, which it sets only
/// when asked.
/// </para>
/// <para>
/// <b>Recording is the engine's, so it is one attachment's.</b> The engine allows one profiling session, and
/// a second client starting one would take the first client's profile away; it is refused with a reason
/// instead. Coverage counters are engine-wide for the same reason and are shared rather than refused,
/// because reading them takes nothing away — but <b>resetting them does</b>, and the protocol says a take
/// resets.
/// </para>
/// <para>
/// <c>Profiler.*</c> is refused while the engine is paused, along with <c>Runtime.runScript</c>: both hand a
/// suspended engine something new to do.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Profiler/"/>.
/// </para>
/// </remarks>
internal sealed partial class ProfilerDomain : ProfilerDomainBase
{
    /// <summary>What the protocol's interval means, which is a rate rather than a duration.</summary>
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(1);

    private readonly EngineTarget _target;

    /// <summary>The source a caller pinned, or <see langword="null"/> to choose one per recording.</summary>
    private readonly IProfileSource? _fixed;

    /// <summary>The instrument this attachment is recording with, chosen when the recording starts.</summary>
    private IProfileSource? _source;

    private TimeSpan _interval = DefaultInterval;
    private double _startedAtMicroseconds;

    /// <summary>Whether this attachment has a recording running, written from both kinds of thread.</summary>
    /// <remarks>
    /// Every other field here is the engine thread's alone. This one is also cleared by a detach, which
    /// arrives on a transport thread, so it is the one that has to be read and written atomically.
    /// </remarks>
    private int _recording;

    internal ProfilerDomain(EngineTarget target)
        : this(target, source: null)
    {
    }

    /// <summary>Builds the domain over a source of the caller's choosing, which is what a test needs.</summary>
    internal ProfilerDomain(EngineTarget target, IProfileSource? source)
    {
        _target = target;
        _fixed = source;
    }

    /// <summary>
    /// The instrument to record the next profile with.
    /// </summary>
    /// <remarks>
    /// <b>The sampler, unless the engine is already sampling for somebody else.</b> A CDP <c>Profile</c> is
    /// what V8's sampling profiler produces and what <c>setSamplingInterval</c> sets the rate of, so that is
    /// the instrument a front end is asking for. There is one sampling session per engine, though, and it
    /// may be the host's own — a client that arrives then gets the exact profiler rather than a refusal,
    /// which costs the run more and misses nothing.
    /// </remarks>
    private IProfileSource Choose()
    {
        if (_fixed is not null)
        {
            return _fixed;
        }

#pragma warning disable JINT0002 // the sampling profiler is the engine's preview area
        var sampling = _target.Engine.Diagnostics.IsSampling;
#pragma warning restore JINT0002

        return sampling ? new EventedProfileSource(_target.Engine) : new SampledProfileSource(_target.Engine);
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <summary>
    /// Disables the domain, ending whatever this attachment had running.
    /// </summary>
    /// <remarks>
    /// A recording nobody is going to stop is a cost the engine keeps paying, so <c>disable</c> ends it and
    /// throws the profile away — which is what a client that disabled the domain asked for.
    /// </remarks>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override ValueTask OnDisabledAsync(CommandContext context)
    {
        Detach();
        return default;
    }

    /// <summary>Releases everything this attachment holds of the engine's profiler and coverage.</summary>
    /// <remarks>
    /// <b>Called from a transport thread when the client goes away, so the stop is queued rather than
    /// performed.</b> A recording is written by the engine thread on every call it makes, and ending one from
    /// another thread would read what that thread is writing — the one place this domain could break the rule
    /// the rest of the package is built on. A target already being disposed takes its engine with it, and
    /// there is then nothing left to stop.
    /// </remarks>
    internal void Detach()
    {
        _preciseCoverage = false;
        _detailedCoverage = false;
        _interval = DefaultInterval;

        if (Interlocked.Exchange(ref _recording, 0) == 0)
        {
            return;
        }

        var source = _source;
        if (source is null)
        {
            return;
        }

        try
        {
            _target.Post(_ =>
            {
                if (source.IsRecording)
                {
                    source.Stop();
                }

                _source = null;
            });
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Records the rate the client asked for, which the exact profiler does not have.
    /// </summary>
    /// <remarks>
    /// The front end sends this before every recording, so refusing it would fail an ordinary one. The engine
    /// profiler behind this domain records at the call boundary rather than on a timer, so there is no rate
    /// to set; the value is kept and handed to <see cref="IProfileSource.Start"/>, where a sampling source
    /// would honour it.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetSamplingIntervalAsync(SetSamplingIntervalRequest parameters, CommandContext context)
    {
        // The protocol's unit is microseconds, and a client that sends zero means "as fast as you can".
        _interval = parameters.Interval > 0 ? TimeSpan.FromTicks(parameters.Interval * TimeSpan.TicksPerMicrosecond) : TimeSpan.Zero;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> StartAsync(EmptyParameters parameters, CommandContext context)
    {
        if (Volatile.Read(ref _recording) != 0)
        {
            // Chrome answers a second start as a success that changes nothing, and a client that lost track
            // of its own state should not lose its recording over it.
            return new ValueTask<EmptyResult>(EmptyResult.Instance);
        }

        var source = Choose();

        try
        {
            source.Start(_interval);
        }
        catch (InvalidOperationException exception)
        {
            Throw.ServerError(
                "The engine cannot record a profile",
                exception.Message);
        }

        _source = source;

        _startedAtMicroseconds = EngineTarget.UnixMilliseconds() * MicrosecondsPerMillisecond;
        Volatile.Write(ref _recording, 1);

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Ends the recording and answers the profile a front end's Performance panel loads.
    /// </summary>
    /// <remarks>
    /// The timestamps are microseconds since the Unix epoch, which is the unit the protocol asks for; the
    /// panel reads only the difference and the deltas, and the deltas here add up to the recorded duration
    /// exactly, because the source knows when every call happened rather than sampling for it.
    /// </remarks>
    protected override ValueTask<StopResponse> StopAsync(EmptyParameters parameters, CommandContext context)
    {
        if (Interlocked.Exchange(ref _recording, 0) == 0)
        {
            Throw.ServerError(
                "No profile is being recorded",
                "send Profiler.start first; a profile is the engine's and only one is recorded at a time");
        }

        var recorded = _source!.Stop();
        _source = null;

        return new ValueTask<StopResponse>(new StopResponse
        {
            Profile = ProfileBuilder.Build(recorded, _target.Scripts, _startedAtMicroseconds),
        });
    }

    private const double MicrosecondsPerMillisecond = 1000;
}
