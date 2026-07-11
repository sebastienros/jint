using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Jint.Collections;
using Jint.Native.Function;
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
        var item = new CallStackElement(function, expression, new CallStackExecutionContext(executionContext));
        _stack.Push(item);
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

    public CallStackElement Pop()
    {
        var item = _stack.Pop();
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

    public bool TryPeek([NotNullWhen(true)] out CallStackElement item)
    {
        return _stack.TryPeek(out item);
    }

    public int Count => _stack._size;

    public void Clear()
    {
        _stack.Clear();
        _statistics?.Clear();
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
            in CallStackElement? element,
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

        AppendLocation(ref builder, shortDescription, location, element, customCallStackBuilder);

        location = element?.Location ?? default;
        index--;

        while (index >= -1)
        {
            element = index >= 0 ? frames[index] : null;
            shortDescription = element?.ToString() ?? "";

            AppendLocation(ref builder, shortDescription, location, element, customCallStackBuilder);

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
