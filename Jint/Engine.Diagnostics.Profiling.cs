using System.Diagnostics.CodeAnalysis;
using Jint.Profiling;
using Jint.Runtime;

namespace Jint;

public partial class Engine
{
    public sealed partial class DiagnosticOperations
    {
        /// <summary>
        /// Whether a profiling session is currently recording on this engine.
        /// </summary>
        public bool IsProfiling => _engine.CallStack._profiler is not null;

        /// <summary>
        /// Starts recording function enters and leaves on this engine. Requires
        /// <see cref="Options.ProfilingOptions.Enabled"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The profiler is evented, not sampling: it records at the call boundary on the engine's own
        /// thread rather than inspecting the engine from another one, which for a runtime with no
        /// thread-safety story is the only way to get a profile that is not a race. The cost is paid by
        /// the calls themselves — one interned frame lookup and one 16-byte event per enter and per leave
        /// — so a profiled run is slower than an unprofiled one, and this is a diagnostic to switch on,
        /// not a meter to leave running.
        /// </para>
        /// <para>
        /// While a session is open it retains one reference per distinct function it has seen: the
        /// <em>definition</em> for a script function, so all closures of one source function are one entry
        /// and nothing engine-affine is held, but the function object itself for a built-in, bound or host
        /// callable. Both are released by <see cref="StopProfiling"/>, and the
        /// <see cref="ScriptProfile"/> it returns retains neither.
        /// </para>
        /// <para>
        /// Everything the engine records is bounded by <see cref="Options.ProfilingOptions.MaxEvents"/>;
        /// see <see cref="ScriptProfile.Truncated"/>. Starting a session while script is already running —
        /// from a host callable the script invoked — is allowed: the frames already on the call stack were
        /// never opened, so their leaves are dropped and the profile simply starts at the depth it found.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <see cref="Options.ProfilingOptions.Enabled"/> is <see langword="false"/> on the options this
        /// engine was built from, or a session is already running.
        /// </exception>
        public void StartProfiling()
        {
            var options = _engine.Options.Profiling;
            if (!options.Enabled)
            {
                Throw.InvalidOperationException(
                    "Profiling is not enabled for this engine. Set Options.Profiling.Enabled to true when building it.");
            }

            var callStack = _engine.CallStack;
            if (callStack._profiler is not null)
            {
                Throw.InvalidOperationException("A profiling session is already running on this engine.");
            }

            callStack._profiler = new ScriptProfiler(options.MaxEvents);
        }

        /// <summary>
        /// Ends the session started by <see cref="StartProfiling"/> and returns what it recorded. Any frame
        /// still open — the engine being mid-call, because the host stopped the profiler from a callable
        /// script invoked — is closed at this instant, so the event stream is always balanced.
        /// </summary>
        /// <returns>The recorded profile. Never <see langword="null"/>.</returns>
        /// <exception cref="InvalidOperationException">No profiling session is running on this engine.</exception>
        public ScriptProfile StopProfiling()
        {
            var callStack = _engine.CallStack;
            var profiler = callStack._profiler;
            if (profiler is null)
            {
                Throw.InvalidOperationException("No profiling session is running on this engine.");
            }

            callStack._profiler = null;
            return profiler!.Complete();
        }

        /// <summary>
        /// Whether a sampling session is currently recording on this engine.
        /// </summary>
        [Experimental(JintDiagnosticIds.PreviewDiagnostic)]
        public bool IsSampling => _engine._sampler is not null;

        /// <summary>
        /// Starts sampling this engine's call stack, on this engine's thread, at the interval
        /// <paramref name="options"/> asks for. Requires <see cref="Options.ProfilingOptions.Enabled"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the statistical instrument, and <see cref="StartProfiling"/> is the exact one. It answers
        /// "where did the time go" rather than "what was called": nothing is recorded per call, the engine
        /// simply notes what its call stack looks like at each sample point, so a profiled run costs the
        /// samples it took and nothing else, and the answer gets more accurate the longer the run.
        /// </para>
        /// <para>
        /// <b>Where a sample can be taken.</b> On the engine's own thread, because a
        /// <see cref="Jint.Native.JsValue"/> is thread-affine and no second thread may read the stack — so
        /// the sample points are the engine's periodic check points, the once-per-64-statements cadence a
        /// timeout or a cancellation token already rides. One step the engine cannot interrupt is therefore
        /// one gap: a single long <c>RegExp</c> match, or a single long host callback, is not sampled
        /// while it runs and instead weighs on the sample taken at its call site. Arming the sampler does
        /// <em>not</em> cost the interpreter's tight-loop lane, which those loops drive the same cadence
        /// from; an exact constraint is what disarms that, and this is not one.
        /// </para>
        /// <para>
        /// While a session is open it retains one reference per distinct function it has sampled: the
        /// <em>definition</em> for a script function, so all closures of one source function are one entry
        /// and nothing engine-affine is held, but the function object itself for a built-in, bound or host
        /// callable. Both are released by <see cref="StopSampling"/>, and the <see cref="SampledProfile"/>
        /// it returns retains neither.
        /// </para>
        /// <para>
        /// Starting a session while script is already running — from a host callable the script invoked —
        /// is allowed, and sampling begins at the next check point.
        /// </para>
        /// </remarks>
        /// <param name="options">
        /// How often to sample and how many samples to keep, or <see langword="null"/> for the defaults:
        /// one millisecond and <see cref="SamplingOptions.DefaultMaxSamples"/>. Read here, into the session;
        /// changing the instance afterwards changes nothing.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="Options.ProfilingOptions.Enabled"/> is <see langword="false"/> on the options this
        /// engine was built from, or a sampling session is already running.
        /// </exception>
        [Experimental(JintDiagnosticIds.PreviewDiagnostic)]
        public void StartSampling(SamplingOptions? options = null)
        {
            if (!_engine.Options.Profiling.Enabled)
            {
                Throw.InvalidOperationException(
                    "Profiling is not enabled for this engine. Set Options.Profiling.Enabled to true when building it.");
            }

            if (_engine._sampler is not null)
            {
                Throw.InvalidOperationException("A sampling session is already running on this engine.");
            }

            options ??= new SamplingOptions();
            _engine._sampler = new SamplingProfiler(options.Interval, options.MaxSamples);
            _engine._evaluationContext.RefreshAmortizedChecks();
        }

        /// <summary>
        /// Ends the session started by <see cref="StartSampling"/> and returns what it recorded.
        /// </summary>
        /// <returns>The recorded profile. Never <see langword="null"/>.</returns>
        /// <exception cref="InvalidOperationException">No sampling session is running on this engine.</exception>
        [Experimental(JintDiagnosticIds.PreviewDiagnostic)]
        public SampledProfile StopSampling()
        {
            var sampler = _engine._sampler;
            if (sampler is null)
            {
                Throw.InvalidOperationException("No sampling session is running on this engine.");
            }

            _engine._sampler = null;
            _engine._evaluationContext.RefreshAmortizedChecks();
            return sampler!.Complete();
        }
    }
}
