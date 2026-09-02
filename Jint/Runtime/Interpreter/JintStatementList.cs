using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Error;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter;

internal enum CompletionValueObservability : byte
{
    /// <summary>Keep the enclosing frame's observability (nested blocks, switch consequents).</summary>
    Inherit = 0,

    /// <summary>Completion values feed Engine.Evaluate / eval / module results (top-level lists).</summary>
    Observable = 1,

    /// <summary>Function bodies: only Return/Throw completions surface, Normal values may be elided.</summary>
    NotObservable = 2,
}

internal sealed class JintStatementList
{
    private readonly record struct Pair(JintStatement Statement, JsValue? Value, bool NormalValueDead);

    private readonly Statement? _statement;
    private readonly CompletionValueObservability _observability;

    // This class must stay immutable after construction: instances are cached on shared
    // interpreter handlers (function definitions, blocks, switch cases) and executed by
    // multiple live generator / async function instances concurrently. Per-execution
    // resume positions live on the suspendable (SuspendDataDictionary), not here.
    private readonly Pair[] _jintStatements;

    // exposed so enclosing loop fast paths can iterate the exact handler instances of this list
    internal int Count => _jintStatements.Length;
    internal JintStatement GetStatement(int index) => _jintStatements[index].Statement;

    public JintStatementList(IFunction function) : this((FunctionBody) function.Body)
    {
    }

    public JintStatementList(BlockStatement blockStatement)
        : this(blockStatement, blockStatement.Body)
    {
    }

    public JintStatementList(Program program)
        : this(null, program.Body, CompletionValueObservability.Observable)
    {
    }

    // blockLevel: true for the StatementList of a Block, a CaseClause or a DefaultClause - the
    // syntactic position Annex B B.3.2 gives a function declaration its var-scope alias in. See
    // JintFunctionDeclarationStatement.
    public JintStatementList(
        Statement? statement,
        in NodeList<Statement> statements,
        CompletionValueObservability? observability = null,
        bool blockLevel = false)
    {
        _statement = statement;
        _observability = observability
            ?? (statement is FunctionBody
                ? CompletionValueObservability.NotObservable
                : statement is null
                    ? CompletionValueObservability.Observable
                    : CompletionValueObservability.Inherit);
        var jintStatements = new Pair[statements.Count];
        for (var i = 0; i < jintStatements.Length; i++)
        {
            var esprimaStatement = statements[i];
            var stmt = blockLevel
                ? JintStatement.BuildBlockLevel(esprimaStatement)
                : JintStatement.Build(esprimaStatement);
            // FastResolve pre-evaluates literal return values.
            // Debug mode check moved to Execute loop to preserve stepping behavior.
            var value = JintStatement.FastResolve(esprimaStatement);
            jintStatements[i] = new Pair(stmt, value, NormalValueDead: false);
        }

        // Dead completion-value analysis, top-level (Observable) lists only: a statement's Normal
        // completion value can never become the list value when a LATER sibling always produces one
        // (an expression statement), because nothing at script/eval/module top level can jump over
        // later siblings — break/continue cannot target past them and throw abandons the value
        // entirely. NOT sound for nested (Inherit) lists: a labeled break out of a block skips the
        // block's remaining statements, so an earlier value may still surface through UpdateEmpty.
        if (_observability == CompletionValueObservability.Observable)
        {
            var laterAlwaysValued = false;
            for (var i = jintStatements.Length - 1; i >= 0; i--)
            {
                if (laterAlwaysValued)
                {
                    jintStatements[i] = jintStatements[i] with { NormalValueDead = true };
                }

                if (statements[i] is ExpressionStatement)
                {
                    laterAlwaysValued = true;
                }
            }
        }

        _jintStatements = jintStatements;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions) 512)]
    public Completion Execute(EvaluationContext context)
    {

        if (_statement is not null)
        {
            context.LastSyntaxElement = _statement;
            context.RunBeforeExecuteStatementChecks(_statement);
        }

        Completion c = Completion.Empty();
        Completion sl = c;

        // Completion-value elision: inside function bodies Normal-completion values are
        // spec-unobservable (only Return/Throw matter), so expression statements may skip
        // materializing their value; top-level lists (script, eval text, module) must surface
        // real values for Engine.Evaluate/eval results, and nested lists (blocks, switch
        // consequents) inherit the enclosing frame's observability.
        var oldCompletionValuesObservable = context.CompletionValuesObservable;
        if (_observability != CompletionValueObservability.Inherit)
        {
            context.CompletionValuesObservable = _observability == CompletionValueObservability.Observable;
        }

        // The resume position is stored per suspendable instance: this list is shared by
        // every live generator / async function created from the same declaration, and the
        // frame's suspendable is fixed for the duration of this call.
        var suspendable = context.Engine.ExecutionContext.Suspendable;

        // The value of a StatementList is the value of the last value-producing item in the StatementList
        var lastValue = JsEmpty.Instance;
        var i = suspendable is not null ? suspendable.Data.GetStatementListPosition(this) : 0;
        var temp = _jintStatements!;
        try
        {
            for (; i < (uint) temp.Length; i++)
            {
                ref readonly var pair = ref temp[i];

                // BypassStatementFastPaths is the frozen `debug mode or coverage` snapshot: both need the
                // statement to actually execute - the debugger to step onto it, coverage to count it -
                // rather than have its pre-resolved value handed over.
                if (pair.Value is null || context.BypassStatementFastPaths)
                {
                    if (pair.NormalValueDead && context.CompletionValuesObservable)
                    {
                        // a later sibling always overwrites this statement's Normal value, so it may
                        // run with elision on (unlocking the loop/statement fast paths); the flag was
                        // just set true for this Observable list, restore it for the next statement
                        context.CompletionValuesObservable = false;
                        c = pair.Statement.Execute(context);
                        context.CompletionValuesObservable = true;
                    }
                    else
                    {
                        c = pair.Statement.Execute(context);
                    }

                    if (context.Engine._error is not null)
                    {
                        c = HandleError(context.Engine, pair.Statement);
                        break;
                    }
                }
                else
                {
                    c = new Completion(CompletionType.Return, pair.Value, pair.Statement._statement);
                }

                // Check for suspension (generator yield or async await). The frame's suspendable
                // reference is fixed for this call (captured above); only its state changes, so
                // probing the captured reference avoids re-reading the execution context per statement.
                if (suspendable is not null && suspendable.IsSuspended)
                {
                    // Save position for resume - we'll re-execute this statement on resume
                    // The yield/await tracking handles knowing which suspension point to resume from
                    suspendable!.Data.SetStatementListPosition(this, i);
                    // Use the suspended value, as the statement's completion value
                    // might be different (e.g., variable declarations return Empty, not the yielded value)
                    var suspendedValue = suspendable.SuspendedValue ?? c.Value;
                    return new Completion(CompletionType.Return, suspendedValue, pair.Statement._statement);
                }

                // Check for return request (from generator.return() call)
                if (suspendable?.ReturnRequested == true)
                {
                    suspendable.Data.ClearStatementListPosition(this);
                    var returnValue = suspendable.SuspendedValue ?? c.Value;
                    return new Completion(CompletionType.Return, returnValue, pair.Statement._statement);
                }

                if (c.Type != CompletionType.Normal)
                {
                    if (suspendable is null || !suspendable.IsSuspended)
                    {
                        var asyncFunction = context.Engine.ExecutionContext.AsyncFunction;
                        if (asyncFunction?._body != this)
                        {
                            suspendable?.Data.ClearStatementListPosition(this);
                        }
                    }

                    return c.UpdateEmpty(sl.Value);
                }

                sl = c;
                if (!c.Value.IsEmpty)
                {
                    lastValue = c.Value;
                }
            }

            // Clear the saved position after normal loop completion for potential re-execution
            // (e.g., this block is a for-of body that will execute again on next iteration)
            // But don't clear for async function/module bodies - if pending promise reactions
            // call AsyncFunctionResume after completion, we shouldn't re-execute from start.
            // Async bodies should complete exactly once.
            var currentAsyncFn = context.Engine.ExecutionContext.AsyncFunction;
            if (currentAsyncFn?._body != this)
            {
                suspendable?.Data.ClearStatementListPosition(this);
            }
        }
        catch (Exception ex) when (LeavingOnException(suspendable, ex))
        {
            if (ex is JintException)
            {
                c = HandleException(context, ex, temp[i].Statement);
            }
            else
            {
                var locationNode = (Node?) context.Engine._lastSyntaxElement ?? temp[i].Statement._statement;
                ExceptionDataHelper.TryAttachJavaScriptLocation(ex, context.Engine, locationNode.Location);
                throw;
            }
        }
        finally
        {
            context.CompletionValuesObservable = oldCompletionValuesObservable;
        }

        // Only apply the final UpdateEmpty(Undefined) at program/script level or function body level.
        // Nested block statements should return empty completion to not override the previous statement's value.
        // _statement is null for Program/Script, or is a FunctionBody for function bodies.
        var result = c.UpdateEmpty(lastValue);
        if (_statement is null or FunctionBody)
        {
            result = result.UpdateEmpty(JsValue.Undefined);
        }
        return result;
    }

    /// <summary>
    /// Exception filter for <see cref="Execute"/>. It does the bookkeeping every exception leaving this
    /// list owes — clearing the resume position, which the old catch-everything handler did — and then
    /// answers <see cref="ShouldCatch"/>, so that the exceptions this frame would only rethrow are never
    /// caught here in the first place.
    /// </summary>
    private bool LeavingOnException(ISuspendable? suspendable, Exception exception)
    {
        // Safe to do from the first pass: nothing between the throw point and this frame reads or writes
        // this list's saved position — only this method's own suspension path sets it, and that path
        // cannot be running while an exception is in flight through it.
        suspendable?.Data.ClearStatementListPosition(this);
        return ShouldCatch(exception);
    }

    /// <summary>
    /// Whether a statement list has anything to do with an exception passing through it, asked from an
    /// exception filter so that the answer <c>false</c> keeps the frame out of the unwind entirely.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Answering <c>false</c> is the point. A statement list wraps every function body and every block,
    /// so a deep recursion stacks one of these frames per JavaScript call. Catching an exception the
    /// handler can only rethrow costs a nested exception dispatch at every one of them — roughly 10 KB
    /// of stack apiece that the runtime cannot reclaim while the dispatch is live — so the engine's own
    /// <see cref="RecursionDepthOverflowException"/>, raised at exactly the depth
    /// <c>Options.LimitRecursion</c> asks for, used to overflow the stack on the way out and take
    /// the host process with it. A filter that declines is answered in the first pass and leaves nothing
    /// behind, so the same exception now unwinds as far as a JavaScript <c>throw</c> does.
    /// </para>
    /// <para>
    /// The two <c>true</c> arms are the two things this frame really does, and both have to keep matching
    /// their handler: the exceptions <see cref="HandleException"/> turns into a Throw completion, and a
    /// CLR exception that has not been annotated with a JavaScript location yet — the innermost frame
    /// alone, since the annotation is idempotent and the frames above it would only redo the same test.
    /// </para>
    /// </remarks>
    internal static bool ShouldCatch(Exception exception) => exception switch
    {
        // Keep in lock-step with HandleException's switch below.
        JavaScriptException or TypeErrorException or RangeErrorException or SyntaxErrorException => true,
        JintException => false,
        _ => !ExceptionDataHelper.HasJavaScriptLocation(exception),
    };

    internal static Completion HandleException(EvaluationContext context, Exception exception, JintStatement? s)
    {
        var completion = exception switch
        {
            JavaScriptException javaScriptException => CreateThrowCompletion(s, javaScriptException),
            TypeErrorException typeErrorException => CreateThrowCompletion(context.Engine.Realm.Intrinsics.TypeError, typeErrorException, typeErrorException.Node ?? s!._statement),
            RangeErrorException rangeErrorException => CreateThrowCompletion(context.Engine.Realm.Intrinsics.RangeError, rangeErrorException, s!._statement),
            SyntaxErrorException syntaxErrorException => CreateThrowCompletion(context.Engine.Realm.Intrinsics.SyntaxError, syntaxErrorException, s!._statement),
            // Unreachable through the callers, whose filters decline anything this switch cannot map;
            // kept so the switch stays total and so a future caller without a filter still behaves.
            _ => throw exception
        };

        if (context.DebugMode)
        {
            // A throw that unwinds through several frames arrives here once per frame, as a fresh
            // JavaScriptException each time. Only the first of them is the throw; the rest are the same
            // throw leaving a body, and the debugger must not stop again for them.
            var reRaised = exception is JavaScriptException { _reRaisedAtBodyBoundary: true };
            context.Engine.Debugger.OnExceptionThrown(completion.Value, completion.Location, reRaised);
        }

        return completion;
    }

    internal static Completion HandleError(Engine engine, JintStatement? s)
    {
        var error = engine._error!;
        engine._error = null;
        var completion = CreateThrowCompletion(error.ErrorConstructor, error.Message, engine._lastSyntaxElement ?? s!._statement);

        if (engine._isDebugMode)
        {
            engine.Debugger.OnExceptionThrown(completion.Value, completion.Location);
        }

        return completion;
    }

    private static Completion CreateThrowCompletion(ErrorConstructor errorConstructor, string? message, Node s)
    {
        var error = errorConstructor.Construct(message);
        return new Completion(CompletionType.Throw, error, s);
    }

    private static Completion CreateThrowCompletion(ErrorConstructor errorConstructor, Exception e, Node s)
    {
        var error = errorConstructor.Construct(e.Message);
        return new Completion(CompletionType.Throw, error, s);
    }

    private static Completion CreateThrowCompletion(JintStatement? s, JavaScriptException v)
    {
        Node source = s!._statement;
        if (v.Location != default)
        {
            source = AstExtensions.CreateLocationNode(v.Location);
        }

        return new Completion(CompletionType.Throw, v.Error, source);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-blockdeclarationinstantiation
    /// </summary>
    internal static void BlockDeclarationInstantiation(DeclarativeEnvironment env, DeclarationCache declarations)
    {
        var privateEnv = env._engine.ExecutionContext.PrivateEnvironment;

        var list = declarations.Declarations;
        var dictionary = env._dictionary ??= new HybridDictionary<Binding>(list.Count, checkExistingKeys: !declarations.AllLexicalScoped);
        dictionary.EnsureCapacity(list.Count);

        for (var i = 0; i < list.Count; i++)
        {
            var declaration = list[i];
            foreach (var bn in declaration.BoundNames)
            {
                if (declaration.IsConstantDeclaration)
                {
                    dictionary.CreateImmutableBinding(bn, strict: true);
                }
                else
                {
                    dictionary.CreateMutableBinding(bn, canBeDeleted: false);
                }
            }

            if (declaration.Declaration is FunctionDeclaration functionDeclaration)
            {
                var definition = env._engine.GetOrCreateFunctionDefinition(functionDeclaration);
                var fn = definition.Name!;
                var fo = env._engine.Realm.Intrinsics.Function.InstantiateFunctionObject(definition, env, privateEnv);
                env.InitializeBinding(fn, fo, DisposeHint.Normal);

                // NOTE: Annex B's var-scope alias for a sloppy-mode block-level function declaration is
                // written when the declaration is EVALUATED, not here - see
                // JintFunctionDeclarationStatement.ExecuteAnnexBVarScopeAssignment.
            }
        }

        dictionary.CheckExistingKeys = true;
    }
}
