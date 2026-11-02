using System.Diagnostics.CodeAnalysis;
using Jint.Native;

namespace Jint.Runtime.Interpreter;

/// <summary>
/// Carries the return completion that a generator's <c>return(value)</c> resumption hands a <c>yield</c>, out of
/// the expression that contains the <c>yield</c> and into the nearest statement list, which turns it back into a
/// <see cref="CompletionType.Return"/> completion (<see cref="JintStatementList.HandleException"/>).
/// </summary>
/// <remarks>
/// <para>
/// <see href="https://tc39.es/ecma262/#sec-generatorresumeabrupt">GeneratorResumeAbrupt</see> resumes the
/// generator with the return completion, so the <c>yield</c> evaluates to that abrupt completion
/// (<see href="https://tc39.es/ecma262/#sec-generatoryield">GeneratorYield</see>), and every <c>?</c> between the
/// <c>yield</c> and the statement propagates it: no operator is applied, no function is called, no
/// <c>PutValue</c> happens and no branch is taken. An expression handler in Jint propagates an abrupt completion
/// only as an exception, which is how a <c>throw(value)</c> resumption already reaches it. A return completion
/// handed back as an ordinary value instead let every enclosing expression finish with it: <c>n += yield</c> stored
/// <c>n + value</c> before the <c>finally</c> ran (sebastienros/jint#4304), and an <c>if</c> test took a branch.
/// </para>
/// <para>
/// A <c>yield</c> that makes up a whole statement still hands the return value back as a value: the statement list
/// or block running that statement tests <see cref="ISuspendable.ReturnRequested"/> straight after it, and nothing
/// runs in between (<see cref="Expressions.JintYieldExpression.MarkStatementPosition"/>). That keeps the
/// common case, a <c>for</c>-<c>of</c> loop leaving early through a generator suspended at <c>yield x;</c>, free of
/// a CLR exception.
/// </para>
/// </remarks>
internal sealed class GeneratorReturnException : JintException
{
    private GeneratorReturnException(JsValue value) : base("Generator return completion")
    {
        Value = value;
    }

    /// <summary>
    /// The value of the return completion: the argument <c>return()</c> was called with (in an async generator, its
    /// awaited value), or what <c>yield*</c> received from the inner iterator's <c>return()</c>.
    /// </summary>
    public JsValue Value { get; }

    [DoesNotReturn]
    internal static void Raise(JsValue value) => throw new GeneratorReturnException(value);
}
