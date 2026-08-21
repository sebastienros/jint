using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Profiling;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.CallStack;

// smaller version with only required info
internal readonly record struct CallStackExecutionContext
{
    public CallStackExecutionContext(in ExecutionContext context)
    {
        LexicalEnvironment = context.LexicalEnvironment;
    }

    internal readonly Environment LexicalEnvironment;

    internal Environment GetThisEnvironment()
    {
        var lex = LexicalEnvironment;
        while (true)
        {
            if (lex is not null)
            {
                if (lex.HasThisBinding())
                {
                    return lex;

                }

                lex = lex._outerEnv;
            }
        }
    }
}

internal sealed class JintCallStack
{
    private readonly RefStack<CallStackElement> _stack = new();
    private readonly Dictionary<CallStackElement, int>? _statistics;

    /// <summary>
    /// The profiling session recording this stack's activations, or <see langword="null"/> — which it is
    /// unless a host has called <see cref="Engine.AdvancedOperations.StartProfiling"/>. Every mutation of
    /// <see cref="_stack"/> happens in this class, so hooking the four of them here is what makes a
    /// profile's enter/exit stream balanced by construction, exceptions included: an unwinding throw pops
    /// through the same <c>finally</c>s a return does.
    /// </summary>
    /// <remarks>
    /// The cost of not profiling is exactly this: one field load and one null test per push and per pop,
    /// on a field of an object the push is already writing to.
    /// </remarks>
    internal ScriptProfiler? _profiler;

    // Internal for use by DebugHandler
    internal RefStack<CallStackElement> Stack => _stack;

    public JintCallStack(bool trackRecursionDepth)
    {
        if (trackRecursionDepth)
        {
            _statistics = new Dictionary<CallStackElement, int>(CallStackElementComparer.Instance);
        }
    }

    public int Push(Function function, JintExpression? expression, in ExecutionContext executionContext)
    {
        // A frameless built-in that reaches here has re-entered interpreted code, which means its
        // missing frame is observable in any stack trace taken below this point.
        Native.Function.LeafCallGuard.AssertNotInLeafCall("a nested call (JintCallStack.Push)");

        var item = new CallStackElement(function, expression, new CallStackExecutionContext(in executionContext));
        return Push(in item);
    }

    private int Push(in CallStackElement item)
    {
        _stack.Push(in item);
        _profiler?.RecordEnter(item.Function);
        if (_statistics is not null)
        {
#pragma warning disable CA1854
#pragma warning disable CA1864
            if (_statistics.ContainsKey(item))
#pragma warning restore CA1854
#pragma warning restore CA1864
            {
                return ++_statistics[item];
            }
            else
            {
                _statistics.Add(item, 0);
                return 0;
            }
        }

        return -1;
    }

    internal void RestoreTop(in CallStackElement item)
    {
        Push(in item);
    }

    public CallStackElement Pop()
    {
        var item = _stack.Pop();
        _profiler?.RecordExit();
        if (_statistics is not null)
        {
            if (_statistics[item] == 0)
            {
                _statistics.Remove(item);
            }
            else
            {
                _statistics[item]--;
            }
        }

        return item;
    }

    public bool TryPop([NotNullWhen(true)] out CallStackElement item)
    {
        if (_stack._size == 0)
        {
            item = default;
            return false;
        }

        item = Pop();
        return true;
    }

    /// <summary>
    /// Replaces the top frame with a proper tail call's target, and returns the target's new recursion
    /// depth exactly as <c>Push</c> would have (-1 when depth is not tracked).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The displaced function is deliberately <em>not</em> discounted. Its frame is gone — that is what
    /// PrepareForTailCall asks for, and what stack traces and <see cref="Count"/> report — but the
    /// activation itself is not over: the trampoline that displaced it (<c>ScriptFunction.ContinueTailCalls</c>)
    /// is still running, and so are all the native frames beneath it. Discounting it made
    /// <c>Options.LimitRecursion</c> unable to see a recursion that leaves and re-enters a trampoline
    /// through a non-tail route — a getter, <c>new</c>, a coercion, a Proxy trap, a host callback — because
    /// each re-entry pushed the displaced function again at depth zero while the native stack kept growing.
    /// Retaining it is also what the option already promises: repeated tail transfers count against the
    /// limit, so an infinite strict tail recursion terminates with
    /// <see cref="RecursionDepthOverflowException"/> rather than running forever.
    /// </para>
    /// <para>
    /// The retention is the trampoline's to undo. The frame count does not change here while
    /// <paramref name="function"/> gains an occurrence, so the statistic runs exactly one occurrence of
    /// the <em>displaced</em> function ahead of the stack — that is the one <c>ContinueTailCalls</c>
    /// hands back through <see cref="ReleaseTailRetention"/> when it returns. A same-definition
    /// replacement is counted like any other, so the statistic keeps step with the hop count instead of
    /// standing still.
    /// </para>
    /// </remarks>
    public int ReplaceTop(Function function, JintExpression? expression)
    {
        ref var top = ref _stack._array[_stack._size - 1];
        var replacement = new CallStackElement(function, expression, in top.CallingExecutionContext);
        top = replacement;
        _profiler?.RecordTailReplace(function);

        if (_statistics is null)
        {
            return -1;
        }

        if (_statistics.TryGetValue(replacement, out var depth))
        {
            return _statistics[replacement] = depth + 1;
        }

        _statistics.Add(replacement, 0);
        return 0;
    }

    /// <summary>
    /// Hands back <paramref name="count"/> occurrences retained by <see cref="ReplaceTop"/>, once the
    /// trampoline that displaced them has returned.
    /// </summary>
    /// <remarks>
    /// Tolerant of a missing entry on purpose: a host callback can call
    /// <c>Engine.Advanced.ResetCallStack()</c> from inside a running trampoline, which clears the
    /// statistics outright, and the release still has to be a no-op rather than a throw.
    /// </remarks>
    internal void ReleaseTailRetention(Function function, int count)
    {
        if (_statistics is null || count <= 0)
        {
            return;
        }

        var item = new CallStackElement(function, null, default);
        if (!_statistics.TryGetValue(item, out var depth))
        {
            return;
        }

        // depth counts occurrences beyond the first, so `count` occurrences leave depth - count, and
        // removing the entry is how zero occurrences are spelled.
        if (depth < count)
        {
            _statistics.Remove(item);
        }
        else
        {
            _statistics[item] = depth - count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TopIs(Function function)
        => _stack._size > 0 && ReferenceEquals(_stack._array[_stack._size - 1].Function, function);

    public bool TryPeek([NotNullWhen(true)] out CallStackElement item)
    {
        return _stack.TryPeek(out item);
    }

    public int Count => _stack._size;

    public void Clear()
    {
        _stack.Clear();
        _statistics?.Clear();
        _profiler?.RecordAbandon();
    }

    /// <summary>
    /// Pops frames until only <paramref name="targetDepth"/> remain. Used when an unhandled throw
    /// unwinds a nested script evaluation (a host callback re-entering the engine mid-run): only the
    /// frames that evaluation pushed are discarded, so an OUTER still-live run keeps its frames \u2014
    /// its stack traces stay intact and its balanced pops stay balanced. Popping (rather than
    /// clearing) also keeps the recursion-depth statistics and the profiler's enter/exit stream
    /// consistent. A no-op when the stack is already at or below <paramref name="targetDepth"/>.
    /// </summary>
    internal void TrimTo(int targetDepth)
    {
        while (_stack._size > targetDepth)
        {
            Pop();
        }
    }

    public override string ToString()
    {
        return string.Join("->", _stack.Select(static cse => cse.ToString()).Reverse());
    }

    internal string BuildCallStackString(Engine engine, SourceLocation location, int excludeTop = 0)
    {
        // The live stack is walked directly; only the elements below the excluded top frames are read.
        return BuildCallStackString(engine, location, _stack._array, _stack._size - excludeTop);
    }

    /// <summary>
    /// Copies the current call-stack frames (excluding the top <paramref name="excludeTop"/> ones) into a
    /// standalone array so the stack trace can be rendered later, after the live stack has unwound. Used by
    /// <see cref="ErrorStackCapture"/> to defer the (often unread) stack-trace string of a constructed error.
    /// </summary>
    internal CallStackElement[] SnapshotFrames(int excludeTop)
    {
        var count = _stack._size - excludeTop;
        if (count <= 0)
        {
            return [];
        }

        var frames = new CallStackElement[count];
        Array.Copy(_stack._array, 0, frames, 0, count);
        return frames;
    }

    /// <summary>
    /// Renders the implementation-defined stack-trace string from a set of frames. Shared by the live path
    /// (<see cref="BuildCallStackString(Engine, SourceLocation, int)"/>) and the deferred capture path
    /// (<see cref="ErrorStackCapture"/>) so both produce byte-identical output. <paramref name="frames"/> is
    /// walked from index <paramref name="frameCount"/>-1 down; only that prefix is read (the live array may
    /// hold additional, excluded top frames beyond <paramref name="frameCount"/>).
    /// </summary>
    internal static string BuildCallStackString(Engine engine, SourceLocation location, CallStackElement[] frames, int frameCount)
    {
        static void AppendLocation(
            ref ValueStringBuilder sb,
            string shortDescription,
            in SourceLocation loc,
            CallStackElement? element,
            Options.BuildCallStackDelegate? callStackBuilder)
        {
            if (callStackBuilder != null && TryInvokeCustomCallStackHandler(callStackBuilder, element, shortDescription, loc, ref sb))
            {
                return;
            }

            var hasShortDescription = !string.IsNullOrWhiteSpace(shortDescription);

            sb.Append("    at ");

            if (hasShortDescription)
            {
                sb.Append(shortDescription);
                sb.Append(" (");
            }

            sb.Append(loc.SourceFile);
            sb.Append(':');
            sb.Append(loc.End.Line);
            sb.Append(':');
            sb.Append(loc.Start.Column + 1); // report column number instead of index

            if (hasShortDescription)
            {
                sb.Append(')');
            }

            sb.Append(System.Environment.NewLine);
        }

        var customCallStackBuilder = engine.Options.Interop.BuildCallStackHandler;
        var builder = new ValueStringBuilder();

        // stack is one frame behind function-wise when we start to process it from expression level
        var index = frameCount - 1;
        var element = index >= 0 ? frames[index] : (CallStackElement?) null;
        var shortDescription = element?.ToString() ?? "";

        AppendLocation(ref builder, shortDescription, in location, element, customCallStackBuilder);

        location = element?.Location ?? default;
        index--;

        while (index >= -1)
        {
            element = index >= 0 ? frames[index] : null;
            shortDescription = element?.ToString() ?? "";

            AppendLocation(ref builder, shortDescription, in location, element, customCallStackBuilder);

            location = element?.Location ?? default;
            index--;
        }

        var result = builder.AsSpan().TrimEnd().ToString();

        builder.Dispose();

        return result;
    }

    private static bool TryInvokeCustomCallStackHandler(
        Options.BuildCallStackDelegate handler,
        CallStackElement? element,
        string shortDescription,
        SourceLocation loc,
        ref ValueStringBuilder sb)
    {
        string[]? arguments = null;
        if (element?.Arguments is not null)
        {
            var args = element.Value.Arguments.Value;
            arguments = args.Count > 0 ? new string[args.Count] : [];
            for (var i = 0; i < arguments.Length; i++)
            {
                arguments[i] = GetPropertyKey(args[i]);
            }
        }

        var str = handler(shortDescription, loc, arguments);
        if (!string.IsNullOrEmpty(str))
        {
            sb.Append(str);
            return true;
        }

        return false;
    }

    /// <summary>
    /// A version of <see cref="AstExtensions.GetKey"/> that cannot get into loop as we are already building a stack.
    /// </summary>
    private static string GetPropertyKey(Node expression)
    {
        if (expression is Literal literal)
        {
            return AstExtensions.LiteralKeyToString(literal);
        }

        if (expression is Identifier identifier)
        {
            return identifier.Name ?? "";
        }

        if (expression is MemberExpression { Computed: false } staticMemberExpression)
        {
            return $"{GetPropertyKey(staticMemberExpression.Object)}.{GetPropertyKey(staticMemberExpression.Property)}";
        }

        return "?";
    }

}

/// <summary>
/// A deferred snapshot of the call stack captured when an error is constructed. The stack-trace string is
/// implementation-defined and, in the common throw/catch case, never read, so building it eagerly per error
/// is wasted work. Instead the frames (which unwind after construction) are copied here and rendered on the
/// first <c>error.stack</c> read, producing a string byte-identical to the eager one.
/// </summary>
/// <remarks>
/// The snapshot holds the frames' <see cref="Native.Function.Function"/> / expression references from the
/// construction site, so those (and the environments they transitively reference) stay alive until the error
/// is collected or the stack is first rendered (whichever comes first). This matches how the frames were
/// already reachable while the error was on the stack; for the dominant thrown-and-discarded case the error
/// and its capture die together.
/// </remarks>
internal sealed class ErrorStackCapture
{
    private readonly Engine _engine;
    private readonly SourceLocation _location;
    private readonly CallStackElement[] _frames;

    internal ErrorStackCapture(Engine engine, in SourceLocation location, CallStackElement[] frames)
    {
        _engine = engine;
        _location = location;
        _frames = frames;
    }

    internal string Render() => JintCallStack.BuildCallStackString(_engine, _location, _frames, _frames.Length);
}
