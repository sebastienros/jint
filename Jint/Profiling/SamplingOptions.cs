using System.Diagnostics.CodeAnalysis;
using Jint.Runtime;

namespace Jint.Profiling;

/// <summary>
/// What one sampling session records: how often to sample, and how many samples to keep.
/// </summary>
/// <remarks>
/// <para>
/// Both settings are read once, into the session <see cref="Engine.DiagnosticOperations.StartSampling"/>
/// opens, so one instance may be reused for any number of sessions and changing it afterwards changes
/// nothing that is already running.
/// </para>
/// <para>
/// This type is in a preview area, declared to the compiler as <c>JINT0002</c>; see
/// <see cref="JintDiagnosticIds"/>.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed class SamplingOptions
{
    /// <summary>
    /// The default value of <see cref="MaxSamples"/> — a hundred thousand, which is a hundred seconds of
    /// script at the default interval.
    /// </summary>
    public const int DefaultMaxSamples = 100_000;

    private TimeSpan _interval = TimeSpan.FromMilliseconds(1);
    private int _maxSamples = DefaultMaxSamples;

    /// <summary>
    /// Creates the options a session runs with unless the host changes them: one sample per millisecond,
    /// and <see cref="DefaultMaxSamples"/> of them.
    /// </summary>
    public SamplingOptions()
    {
    }

    /// <summary>
    /// How much time must pass before the next sample is taken, defaulting to one millisecond.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a floor, not a schedule. Samples are taken at the engine's periodic check points — every 64
    /// statements, the cadence the timeout and cancellation constraints already ride — so the first check
    /// point at or after the interval elapses is the one that samples, and a step the engine cannot
    /// interrupt (one long built-in call, one long host callback) delays it for as long as it runs.
    /// </para>
    /// <para>
    /// <see cref="TimeSpan.Zero"/> means every check point, which is the setting a test uses when it wants
    /// a sample count that does not depend on how fast the machine is.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            if (value < TimeSpan.Zero)
            {
                Throw.ArgumentOutOfRangeException(nameof(value), "Interval cannot be negative.");
            }

            _interval = value;
        }
    }

    /// <summary>
    /// Upper bound on the number of samples one session keeps, defaulting to
    /// <see cref="DefaultMaxSamples"/>. Reaching it stops recording and counts what is refused into
    /// <see cref="SampledProfile.DroppedSampleCount"/>.
    /// </summary>
    /// <remarks>
    /// The cap is what keeps a runaway script from turning a diagnostic into an out-of-memory: the frame,
    /// stack and function tables only grow through a recorded sample, so capping samples caps all four.
    /// Unlike the execution constraints there is no "unlimited" sentinel — a non-positive value is rejected
    /// rather than read as no limit.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaxSamples
    {
        get => _maxSamples;
        set
        {
            if (value <= 0)
            {
                Throw.ArgumentOutOfRangeException(nameof(value), "MaxSamples must be positive.");
            }

            _maxSamples = value;
        }
    }
}
