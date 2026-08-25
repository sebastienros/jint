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
    }
}
