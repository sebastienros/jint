using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// <c>return Expression;</c> in an async generator, which awaits its value before it completes:
/// https://tc39.es/ecma262/#sec-return-statement-runtime-semantics-evaluation step 3,
/// "If GetGeneratorKind() is async, set exprValue to ? Await(exprValue)".
/// </summary>
/// <remarks>
/// <para>
/// The return completion leaves the statement only once that await has settled, so the enclosing <c>finally</c>
/// blocks, a loop's iterator close and the generator's own completion all come after it, and a rejection is a throw
/// completion of the statement, which a <c>catch</c> around it sees (sebastienros/jint#4332). The await suspends the
/// body exactly as an <c>await</c> expression does (<see cref="JintAwaitExpression.SuspendAsyncGenerator"/>), with
/// this statement as the suspension point; the replayed body comes back here, and the awaited value is the completion.
/// </para>
/// <para>
/// Which handler a return statement gets is decided when it is built, never when it runs: the State of an async
/// generator marks the generator's own return statements (<see cref="MarkReturnStatements"/>), and
/// <see cref="JintStatement.Build"/> reads the mark. A return statement anywhere else, including one in a function
/// nested inside an async generator, stays a <see cref="JintReturnStatement"/> and runs exactly what it ran before; the
/// mark also keeps a literal operand off the two lanes that resolve <c>return &lt;literal&gt;;</c> ahead of time
/// (<see cref="ConstantStatement"/> and <see cref="JintStatement.FastResolve"/>), which would skip the await.
/// </para>
/// </remarks>
internal sealed class JintAsyncGeneratorReturnStatement : JintStatement<ReturnStatement>
{
    private readonly JintExpression _argument;

    public JintAsyncGeneratorReturnStatement(ReturnStatement statement) : base(statement)
    {
        _argument = JintExpression.Build(statement.Argument!);
    }

    /// <summary>
    /// Whether <paramref name="statement"/> is the <c>return Expression;</c> of an async generator, marked by
    /// <see cref="MarkReturnStatements"/>.
    /// </summary>
    internal static bool IsMarked(ReturnStatement statement) => ReferenceEquals(statement.UserData, Marker.Instance);

    /// <summary>
    /// Marks every <c>return Expression;</c> that belongs to the async generator whose body is <paramref name="body"/>,
    /// so that it is built as this handler. A return statement belongs to the nearest function around it, and can only
    /// stand where a statement can: a function, a class or any other expression starts a body of its own, and is not
    /// entered.
    /// </summary>
    /// <remarks>
    /// The mark is engine-neutral AST metadata, as <see cref="TailCallMarker"/> is, so a prepared script shares it with
    /// every engine that runs it. It is written while the function's State is built - at preparation time, or by the
    /// first call under the lock that publishes the State - and so before any engine builds the body. It replaces the
    /// <see cref="ConstantStatement"/> the static analysis left on a literal return, which is built before the State of
    /// the function around it.
    /// </remarks>
    internal static void MarkReturnStatements(Node body)
    {
        foreach (var child in body.ChildNodes)
        {
            switch (child)
            {
                case ReturnStatement { Argument: not null } returnStatement:
                    returnStatement.UserData = Marker.Instance;
                    break;
                case IFunction or ClassDeclaration:
                    break;
                case Statement or SwitchCase or CatchClause:
                    MarkReturnStatements(child);
                    break;
            }
        }
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var generator = engine.ExecutionContext.AsyncGenerator;
        if (generator is null)
        {
            // Not reachable from a parsed script, whose marked return statements only ever run in their async
            // generator's frame; a hand-built AST could share one node with another function, which returns as usual.
            return new Completion(CompletionType.Return, _argument.GetValue(context), _statement);
        }

        // The replayed body has come back to the await this statement suspended on.
        if (generator._isResuming && generator._lastYieldNode is JintAsyncGeneratorReturnStatement suspended
            && ReferenceEquals(suspended._statement, _statement))
        {
            var awaited = generator._nextValue ?? JsValue.Undefined;
            generator._isResuming = false;
            generator._lastYieldNode = null;

            if (generator._resumeWithThrow)
            {
                generator._resumeWithThrow = false;
                Throw.JavaScriptException(engine, awaited, _statement.Location);
            }

            // A later replay of the body that passes through here again - as the replays of the awaits in the
            // operand do - finds the completed await instead of evaluating the operand a second time.
            generator._completedAwaits ??= new Dictionary<object, JsValue>();
            generator._completedAwaits[_statement] = awaited;
            return new Completion(CompletionType.Return, awaited, _statement);
        }

        if (generator._completedAwaits?.TryGetValue(_statement, out var completed) == true)
        {
            return new Completion(CompletionType.Return, completed, _statement);
        }

        var value = _argument.GetValue(context);

        // A yield or an await in the operand suspended the body; the statement list records the position, and the
        // replay evaluates the operand again to resume it.
        if (context.IsSuspended())
        {
            return new Completion(CompletionType.Return, value, _statement);
        }

        // Await, step 2: PromiseResolve(%Promise%, value), whose Get of a promise's constructor can throw, which is the
        // statement's throw. A primitive, which can never be a thenable, is handed to the continuation directly.
        var promise = PromiseConstructor.ResolveForAwait(engine, value);

        JintAwaitExpression.SuspendAsyncGenerator(engine, generator, this, promise, value);
        return new Completion(CompletionType.Return, JsValue.Undefined, _statement);
    }

    /// <summary>
    /// The mark <see cref="MarkReturnStatements"/> leaves on a return statement's <see cref="Node.UserData"/>.
    /// </summary>
    private sealed class Marker
    {
        internal static readonly Marker Instance = new();

        private Marker()
        {
        }
    }
}
