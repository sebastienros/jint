using Jint.Runtime;

namespace Jint;

public sealed partial class Options
{
    /// <summary>
    /// Opt-in script profiling, evented and sampling both. Nothing is recorded — and neither instrument can
    /// be started — unless <see cref="ProfilingOptions.Enabled"/> is set, so a default engine is exactly the
    /// engine it was before this existed.
    /// </summary>
    /// <seealso cref="Engine.DiagnosticOperations.StartProfiling"/>
    /// <seealso cref="Engine.DiagnosticOperations.StartSampling"/>
    public ProfilingOptions Profiling => Materialize(ref _profiling, ref _readOnly);

    private ProfilingOptions? _profiling;

    /// <summary>
    /// Configuration for <see cref="Engine.DiagnosticOperations.StartProfiling"/>, and the gate
    /// <see cref="Engine.DiagnosticOperations.StartSampling"/> shares with it.
    /// </summary>
    /// <remarks>
    /// Like every other <see cref="Options"/> group this may be shared by any number of engines, including
    /// concurrent ones: nothing on it is engine-affine, and both members are read once, on the thread that
    /// calls <see cref="Engine.DiagnosticOperations.StartProfiling"/>, into the session it starts. A
    /// sampling session takes its own <see cref="Profiling.SamplingOptions"/> per session instead, since
    /// how often to sample is a question about one investigation rather than about the engine.
    /// </remarks>
    public sealed partial class ProfilingOptions
    {
        /// <summary>
        /// The default value of <see cref="MaxEvents"/>.
        /// </summary>
        public const int DefaultMaxEvents = 1_000_000;

        private int _maxEvents = DefaultMaxEvents;

        /// <summary>
        /// Whether <see cref="Engine.DiagnosticOperations.StartProfiling"/> and
        /// <see cref="Engine.DiagnosticOperations.StartSampling"/> are allowed on this engine, defaults to
        /// <see langword="false"/>. When it is false both methods throw and no profiler is ever attached,
        /// which is what makes the engine's cost of not profiling a single null field test per call-stack
        /// push and pop.
        /// </summary>
        /// <remarks>
        /// This is a capability gate, not a switch that starts recording: a host still has to open a session.
        /// Both instruments retain one reference per distinct function they see (see
        /// <see cref="Engine.DiagnosticOperations.StartProfiling"/>), so an engine that runs untrusted script
        /// can refuse profiling outright by leaving this false. The sampler is gated for that reason rather
        /// than for cost — unlike the evented profiler it charges nothing at all while idle — and it is one
        /// gate rather than two because a host that has said no to profiling has said it once.
        /// </remarks>
        public bool Enabled { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Upper bound on the number of events one profiling session records, defaults to
        /// <see cref="DefaultMaxEvents"/> (one million). Reaching it stops recording and flags the
        /// resulting <see cref="Profiling.ScriptProfile.Truncated"/>; the events already recorded stay,
        /// and every frame still open at that moment is closed so the stream remains balanced.
        /// </summary>
        /// <remarks>
        /// This is a cap on events, not on frames: a frame only enters the profile through an event, so
        /// the frame table is bounded by the same number. Unlike the execution constraints there is no
        /// "unlimited" sentinel — a non-positive value is rejected rather than read as no limit.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
        public int MaxEvents
        {
            get => _maxEvents;
            set
            {
                ThrowIfReadOnly();
                if (value <= 0)
                {
                    Throw.ArgumentOutOfRangeException(nameof(value), "MaxEvents must be positive.");
                }

                _maxEvents = value;
            }
        }

        internal ProfilingOptions Clone() => (ProfilingOptions) MemberwiseClone();
    }
}
