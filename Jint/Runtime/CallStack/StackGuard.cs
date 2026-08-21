// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// https://github.com/dotnet/runtime/blob/a0964f9e3793cb36cc01d66c14a61e89ada5e7da/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/StackGuard.cs

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Jint.Runtime.CallStack;

internal sealed class StackGuard
{
    public const int Disabled = -1;

    private readonly Engine _engine;

    // Snapshot of Options.Constraints.MaxExecutionStackCount, taken at construction like the
    // engine's other option-derived fast-check fields. Every call expression enters through
    // TryEnterOnCurrentStack, and the disabled check previously cost three dereferences
    // (Options → Constraints → property) before it could return.
    private readonly int _maxExecutionStackCount;
    private readonly bool _enabled;

    // Snapshot of Options.Constraints.StackOverflowGuard, which is on for a default engine. The two
    // lanes are exclusive, and the reason is ordering rather than taste: the
    // backstop sits inside ScriptFunction, i.e. a few native frames *below* TryEnterOnCurrentStack's
    // call site, so on a stack low enough for either to fire the backstop reaches the condition first
    // and would throw where the older lane wanted to hop. MaxExecutionStackCount is the older and more
    // specific request, so it wins.
    private readonly bool _backstopEnabled;

    public StackGuard(Engine engine)
    {
        _engine = engine;
        _maxExecutionStackCount = engine.Options.Constraints.MaxExecutionStackCount;
        _enabled = _maxExecutionStackCount != Disabled;
        _backstopEnabled = !_enabled && engine.Options.Constraints.StackOverflowGuard;
    }

    /// <summary>
    /// The default backstop against unbounded script recursion
    /// (<see cref="Options.ConstraintOptions.StackOverflowGuard"/>), run once per entry into an
    /// interpreted function that adds a native frame — every route into one, not only a call
    /// expression. Throws a catchable <c>RangeError</c> while there is still stack left to unwind on,
    /// which is the whole difference between this and the native stack overflow that ends the process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the branch on the cached flag is inlined into the caller; the probe and the throw are both
    /// out of line. So the four <see cref="Native.Function.ScriptFunction"/> entry points that call this
    /// — <c>Call</c>, <c>CallWithStackFrame</c>, <c>CallFromRegisters</c> and <c>Construct</c> — pay one
    /// predictable test of a <see langword="bool"/> field per call when the guard is off, which is what
    /// makes it affordable to have the sites there unconditionally: the placement is what covers the
    /// routes a call expression never sees, and it is worth keeping whether or not this engine opted out.
    /// </para>
    /// <para>
    /// Those four are exactly the entries that grow the native stack, which is why the probe is not in
    /// <c>CallOnce</c> or <c>CallCore</c> even though every call funnels through them. Both are re-entered
    /// from <c>ContinueTailCalls</c>' loop, where a proper tail call has replaced the caller's frame
    /// rather than stacked one on top: there is no stack to probe for, and probing anyway would charge a
    /// strict tail recursion once per hop.
    /// </para>
    /// <para>
    /// The probe is <c>RuntimeHelpers.TryEnsureSufficientExecutionStack</c>: it asks the runtime
    /// whether a fixed reserve is still available below the current frame, so it adapts to the thread the
    /// engine happens to be on instead of to a frame count the host had to guess. The reserve is what pays
    /// for the unwind, and it is enough because the error thrown is a <see cref="JavaScriptException"/> —
    /// <see cref="Interpreter.JintStatementList.HandleException"/> turns one into a throw
    /// <c>Completion</c> at each interpreter level rather than re-throwing it, and a completion costs no
    /// stack to return. A CLR exception rethrown frame by frame does not have that property: each rethrow
    /// happens inside a catch handler whose frames the runtime cannot collapse until the dispatch ends, so
    /// it *consumes* stack per level. Measured on a 1.5 MB stack, a <c>JavaScriptException</c> unwinds
    /// 1153 interpreter frames where a rethrown CLR exception manages 158.
    /// </para>
    /// <para>
    /// On <c>net462</c>/<c>netstandard2.0</c> the probe is the throwing form wrapped in a try/catch (see
    /// <c>RuntimeHelpersPolyfills</c>), i.e. one exception on the failing probe only. That is once per
    /// stack exhaustion, immediately before an exception is thrown anyway.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureStackHeadroom()
    {
        if (_backstopEnabled)
        {
            ProbeStackHeadroom();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ProbeStackHeadroom()
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            ThrowMaximumCallStackSizeExceeded();
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowMaximumCallStackSizeExceeded()
    {
        Throw.RangeError(_engine.Realm, "Maximum call stack size exceeded");
    }

    public bool TryEnterOnCurrentStack()
    {
        if (!_enabled)
        {
            return true;
        }

        if (RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return true;
        }

        if (_engine.CallStack.Count > _maxExecutionStackCount)
        {
            Throw.RangeError(_engine.Realm, "Maximum call stack size exceeded");
        }

        return false;
    }

    public static TR RunOnEmptyStack<T1, TR>(Func<T1, TR> action, T1 arg1)
    {
#if NETFRAMEWORK
        return RunOnEmptyStackCore(static s =>
        {
            var t = (Tuple<Func<T1, TR>, T1>) s;
            return t.Item1(t.Item2);
        }, Tuple.Create(action, arg1));
#else
        // Prefer ValueTuple when available to reduce dependencies on Tuple
        return RunOnEmptyStackCore(static s =>
        {
            var t = ((Func<T1, TR>, T1)) s;
            return t.Item1(t.Item2);
        }, (action, arg1));
#endif

    }

    private static R RunOnEmptyStackCore<R>(Func<object, R> action, object state)
    {
        // Using default scheduler rather than picking up the current scheduler.
        Task<R> task = Task.Factory.StartNew((Func<object?, R>) action, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

        // Avoid AsyncWaitHandle lazy allocation of ManualResetEvent in the rare case we finish quickly.
        if (!task.IsCompleted)
        {
            // Task.Wait has the potential of inlining the task's execution on the current thread; avoid this.
            ((IAsyncResult) task).AsyncWaitHandle.WaitOne();
        }

        // Using awaiter here to propagate original exception
        return task.GetAwaiter().GetResult();
    }
}
