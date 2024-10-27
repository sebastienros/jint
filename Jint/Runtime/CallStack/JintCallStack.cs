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
        ref readonly var item = ref _stack.Pop();
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

            sb.Append("   at");

            if (!string.IsNullOrWhiteSpace(shortDescription))
            {
                sb.Append(' ');
                sb.Append(shortDescription);
            }

            if (element?.Arguments is not null)
            {
                // it's a function
                sb.Append(" (");
                var arguments = element.Value.Arguments.Value;
                for (var i = 0; i < arguments.Count; i++)
                {
                    if (i != 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(GetPropertyKey(arguments[i]));
                }
                sb.Append(')');
            }

            sb.Append(' ');
            sb.Append(loc.SourceFile);
            sb.Append(':');
            sb.Append(loc.End.Line);
            sb.Append(':');
            sb.Append(loc.Start.Column + 1); // report column number instead of index
            sb.Append(System.Environment.NewLine);
        }

        var customCallStackBuilder = engine.Options.Interop.BuildCallStackHandler;
        var builder = new ValueStringBuilder();

        // stack is one frame behind function-wise when we start to process it from expression level
        var index = _stack._size - 1 - excludeTop;
        var element = index >= 0 ? _stack[index] : (CallStackElement?) null;
        var shortDescription = element?.ToString() ?? "";

        AppendLocation(ref builder, shortDescription, location, element, customCallStackBuilder);

        location = element?.Location ?? default;
        index--;

        while (index >= -1)
        {
            element = index >= 0 ? _stack[index] : null;
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
