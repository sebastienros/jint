using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Error;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter;

internal sealed class JintStatementList
{
    private readonly record struct Pair(JintStatement Statement, JsValue? Value);

    private readonly Statement? _statement;

    private readonly Pair[] _jintStatements;
    private uint _index;

    public JintStatementList(IFunction function) : this((FunctionBody) function.Body)
    {
    }

    public JintStatementList(BlockStatement blockStatement)
        : this(blockStatement, blockStatement.Body)
    {
    }

    public JintStatementList(Program program)
        : this(null, program.Body)
    {
    }

    public JintStatementList(Statement? statement, in NodeList<Statement> statements)
    {
        _statement = statement;
        var jintStatements = new Pair[statements.Count];
        for (var i = 0; i < jintStatements.Length; i++)
        {
            var esprimaStatement = statements[i];
            var stmt = JintStatement.Build(esprimaStatement);
            // FastResolve pre-evaluates literal return values.
            // Debug mode check moved to Execute loop to preserve stepping behavior.
            var value = JintStatement.FastResolve(esprimaStatement);
            jintStatements[i] = new Pair(stmt, value);
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

        // The value of a StatementList is the value of the last value-producing item in the StatementList
        var lastValue = JsEmpty.Instance;
        var i = _index;
        var temp = _jintStatements!;
        try
        {
            for (; i < (uint) temp.Length; i++)
            {
                ref readonly var pair = ref temp[i];

                if (pair.Value is null || context.DebugMode)
                {
                    c = pair.Statement.Execute(context);
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

                // Check for suspension (generator yield or async await)
                var suspendable = context.Engine.ExecutionContext.Suspendable;
                if (context.IsSuspended())
                {
                    // Save position for resume - we'll re-execute this statement on resume
                    // The yield/await tracking handles knowing which suspension point to resume from
                    _index = i;
                    // Use the suspended value, as the statement's completion value
                    // might be different (e.g., variable declarations return Empty, not the yielded value)
                    var suspendedValue = suspendable?.SuspendedValue ?? c.Value;
                    return new Completion(CompletionType.Return, suspendedValue, pair.Statement._statement);
                }

                // Check for return request (from generator.return() call)
                if (suspendable?.ReturnRequested == true)
                {
                    Reset();
                    var returnValue = suspendable.SuspendedValue ?? c.Value;
                    return new Completion(CompletionType.Return, returnValue, pair.Statement._statement);
                }

                if (c.Type != CompletionType.Normal)
                {
                    return c.UpdateEmpty(sl.Value);
                }

                sl = c;
                if (!c.Value.IsEmpty)
                {
                    lastValue = c.Value;
                }
            }

            // Reset index after normal loop completion for potential re-execution
            // (e.g., this block is a for-of body that will execute again on next iteration)
            // But don't reset for async function/module bodies - if pending promise reactions
            // call AsyncFunctionResume after completion, we shouldn't re-execute from start.
            // Async bodies should complete exactly once.
            var currentAsyncFn = context.Engine.ExecutionContext.AsyncFunction;
            if (currentAsyncFn?._body != this)
            {
                _index = 0;
            }
        }
        catch (Exception ex)
        {
            Reset();

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

    internal static Completion HandleException(EvaluationContext context, Exception exception, JintStatement? s)
    {
        var completion = exception switch
        {
            JavaScriptException javaScriptException => CreateThrowCompletion(s, javaScriptException),
            TypeErrorException typeErrorException => CreateThrowCompletion(context.Engine.Realm.Intrinsics.TypeError, typeErrorException, typeErrorException.Node ?? s!._statement),
            RangeErrorException rangeErrorException => CreateThrowCompletion(context.Engine.Realm.Intrinsics.RangeError, rangeErrorException, s!._statement),
            SyntaxErrorException syntaxErrorException => CreateThrowCompletion(context.Engine.Realm.Intrinsics.SyntaxError, syntaxErrorException, s!._statement),
            _ => throw exception
        };

        if (context.DebugMode)
        {
            context.Engine.Debugger.OnExceptionThrown(completion.Value, completion.Location);
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
                var definition = new JintFunctionDefinition(functionDeclaration);
                var fn = definition.Name!;
                var fo = env._engine.Realm.Intrinsics.Function.InstantiateFunctionObject(definition, env, privateEnv);
                env.InitializeBinding(fn, fo, DisposeHint.Normal);

                // B.3.2/B.3.3/B.3.3.1: Copy block-level function declaration to var scope in sloppy mode
                // Only regular function declarations get AnnexB treatment (not generators, async, or async generators)
                if (!functionDeclaration.Generator && !functionDeclaration.Async && !StrictModeScope.IsStrictModeCode)
                {
                    var engine = env._engine;
                    var executionContext = engine.ExecutionContext;
                    var varEnv = executionContext.VariableEnvironment;
                    if (!ReferenceEquals(varEnv, env))
                    {
                        var shouldCopy = false;
                        if (executionContext.Function is { _functionDefinition: { } funcDef })
                        {
                            // Function scope: check AnnexBFunctionDeclarations from B.3.3.1
                            // Must check the specific declaration, not just the name, because
                            // nested blocks can have same-named declarations with different eligibility.
                            shouldCopy = funcDef.Initialize().AnnexBFunctionDeclarations?.Contains(functionDeclaration) == true;
                        }
                        else if (varEnv is GlobalEnvironment globalEnv)
                        {
                            // Global/eval scope: copy if not a lexical declaration
                            shouldCopy = !globalEnv.HasLexicalDeclaration(fn) && globalEnv.HasBinding(fn);
                        }
                        else
                        {
                            // Eval in function scope: copy if var binding exists
                            shouldCopy = varEnv.HasBinding(fn);
                        }

                        if (shouldCopy)
                        {
                            varEnv.SetMutableBinding(fn, fo, strict: false);
                        }
                    }
                }
            }
        }

        dictionary.CheckExistingKeys = true;
    }

    public bool Completed => _index == _jintStatements?.Length;

    public void Reset()
    {
        _index = 0;
    }
}
