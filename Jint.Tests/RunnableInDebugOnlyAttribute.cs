#nullable enable

using System.Diagnostics;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Jint.Tests;

/// <summary>
/// Marks a test that only runs under a debugger, because it needs something an unattended run has no
/// business doing — reaching the network, for instance.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RunnableInDebugOnlyAttribute : NUnitAttribute, IApplyToTest
{
    public void ApplyToTest(Test test)
    {
        if (test.RunState == RunState.NotRunnable || Debugger.IsAttached)
        {
            return;
        }

        test.RunState = RunState.Ignored;
        test.Properties.Set(PropertyNames.SkipReason, "Only running in interactive mode.");
    }
}
