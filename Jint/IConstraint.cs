namespace Jint;

/// <summary>
/// A constraint that engine can check for validate during statement execution.
/// </summary>
public abstract class Constraint
{
    internal Action? BeforeFailure { get; set; }

    /// <summary>
    /// Called before each statement to check if your requirements are met; if not - throws an exception.
    /// </summary>
    public abstract void Check();

    /// <summary>
    /// Called before script is run. Useful when you use an engine object for multiple executions.
    /// </summary>
    public abstract void Reset();

    /// <summary>
    /// Whether the engine may check this constraint every N statements instead of before every single one.
    /// This is the sole input to that classification, so a constraint decides its own check cadence here.
    /// Defaults to <see langword="false"/>, which is how every user-derived constraint behaved before this
    /// property existed; the built-in constraints each override it and say why.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Amortization is sound only when <see cref="Check"/> <b>observes external state without consuming
    /// it</b> — a wall clock, a <see cref="System.Threading.CancellationToken"/>, a flag another thread
    /// sets. For such a constraint, checking less often changes nothing but detection latency, which stays
    /// bounded (the engine also re-checks whenever control returns from host code, so a single long CLR
    /// call cannot stretch it).
    /// </para>
    /// <para>
    /// It is <b>not</b> sound when the call frequency is itself the semantics. A constraint that counts its
    /// own invocations (a statement budget) must stay exact, because skipping calls changes what it
    /// measures. Neither is it sound for a budget over a quantity that can grow without bound between two
    /// checks — an allocation cap, for instance, can be blown arbitrarily far past its limit inside one
    /// statement, so the built-in memory-limit constraint deliberately stays exact.
    /// </para>
    /// <para>
    /// Returning <see langword="true"/> also keeps the interpreter's tight-loop lanes armed, since those
    /// lanes drive amortized constraints at their own bounded cadence but cannot run exact ones.
    /// </para>
    /// </remarks>
    public virtual bool IsAmortizable => false;
}
