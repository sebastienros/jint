using Jint.Runtime;

namespace Jint;

public partial class Options
{
    /// <summary>
    /// Opt-in evented script profiling. Nothing is recorded — and nothing can be started — unless
    /// <see cref="ProfilingOptions.Enabled"/> is set, so a default engine is exactly the engine it was
    /// before this existed.
    /// </summary>
    /// <seealso cref="Engine.AdvancedOperations.StartProfiling"/>
    public ProfilingOptions Profiling { get; } = new();

    /// <summary>
    /// Configuration for <see cref="Engine.AdvancedOperations.StartProfiling"/>.
    /// </summary>
    /// <remarks>
    /// Like every other <see cref="Options"/> group this may be shared by any number of engines, including
    /// concurrent ones: nothing on it is engine-affine, and both members are read once, on the thread that
    /// calls <see cref="Engine.AdvancedOperations.StartProfiling"/>, into the session it starts. Changing
    /// either afterwards does not reach a session already running.
    /// </remarks>
    public class ProfilingOptions
    {
        /// <summary>
        /// The default value of <see cref="MaxEvents"/>.
        /// </summary>
        public const int DefaultMaxEvents = 1_000_000;

        private int _maxEvents = DefaultMaxEvents;

        /// <summary>
        /// Whether <see cref="Engine.AdvancedOperations.StartProfiling"/> is allowed on this engine,
        /// defaults to <see langword="false"/>. When it is false that method throws and no profiler is
        /// ever attached, which is what makes the engine's cost of not profiling a single null field test
        /// per call-stack push and pop.
        /// </summary>
        /// <remarks>
        /// This is a capability gate, not a switch that starts recording: a host still has to call
        /// <see cref="Engine.AdvancedOperations.StartProfiling"/> to open a session. Recording retains one
        /// reference per distinct function seen (see <see cref="Engine.AdvancedOperations.StartProfiling"/>),
        /// so an engine that runs untrusted script can refuse profiling outright by leaving this false.
        /// </remarks>
        public bool Enabled { get; set; }

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
                if (value <= 0)
                {
                    Throw.ArgumentOutOfRangeException(nameof(value), "MaxEvents must be positive.");
                }

                _maxEvents = value;
            }
        }
    }
}
