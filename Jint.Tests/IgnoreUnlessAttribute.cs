#nullable enable

using System.Reflection;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Jint.Tests;

/// <summary>
/// Skips a test unless a <see langword="static" /> <see cref="bool" /> member of the same class says to run
/// it, reporting the given reason.
/// </summary>
/// <remarks>
/// <para>
/// The replacement for xUnit v3's <c>[Fact(Skip = "…", SkipUnless = nameof(…))]</c>, which NUnit has no
/// equivalent of. The conditions it gates on are decided by the environment the run was started in and are
/// the same for every test in the assembly — whether host-contract verification is on, whether the runtime
/// can account for managed allocations, whether the platform's IDNA is the URL Standard's — so answering
/// them once while the test tree is built costs nothing and keeps the gate where a reader looks for it.
/// </para>
/// <para>
/// A skipped test is reported skipped rather than passed, which is the point: a gate that reported a pass
/// would make an unrun test indistinguishable from a run one.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class IgnoreUnlessAttribute : NUnitAttribute, IApplyToTest
{
    private readonly string _conditionMember;
    private readonly string _reason;

    /// <param name="conditionMember">
    /// The name of a static <see cref="bool" /> property or field on the test's own class. Spell it with
    /// <c>nameof</c> so that renaming the member moves the gate with it.
    /// </param>
    /// <param name="reason">Why the test did not run, as it will be reported.</param>
    public IgnoreUnlessAttribute(string conditionMember, string reason)
    {
        _conditionMember = conditionMember;
        _reason = reason;
    }

    public void ApplyToTest(Test test)
    {
        if (test.RunState == RunState.NotRunnable)
        {
            return;
        }

        if (!Condition(test))
        {
            test.RunState = RunState.Ignored;
            test.Properties.Set(PropertyNames.SkipReason, _reason);
        }
    }

    private bool Condition(Test test)
    {
        // The declaring type rather than the fixture type: a gate declared on a base class stays readable
        // from a derived one, and it is the type the nameof was written against.
        var declaring = test.Method?.MethodInfo.DeclaringType
            ?? throw new InvalidOperationException($"{nameof(IgnoreUnlessAttribute)} needs a test method to read {_conditionMember} from.");

        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        var value = declaring.GetProperty(_conditionMember, Flags)?.GetValue(obj: null)
            ?? declaring.GetField(_conditionMember, Flags)?.GetValue(obj: null);

        // A missing or non-boolean member is a mistake in the test, not a reason to skip it silently.
        return value as bool?
            ?? throw new InvalidOperationException(
                $"{declaring.FullName} has no static bool named {_conditionMember} for {nameof(IgnoreUnlessAttribute)} to read.");
    }
}
