using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// https://tc39.es/ecma262/#sec-for-in-and-for-of-statements
/// </summary>
internal sealed class JintForInForOfStatement : JintStatement<Statement>
{
    private readonly Node _leftNode;
    private readonly Statement _forBody;
    private readonly Expression _rightExpression;
    private readonly IterationKind _iterationKind;

    private ProbablyBlockStatement _body;
    private JintExpression? _expr;
    private DestructuringPattern? _assignmentPattern;
    private JintExpression _right = null!;
    private List<Key>? _tdzNames;
    private bool _destructuring;
    private LhsKind _lhsKind;
    private DisposeHint _disposeHint;

    public JintForInForOfStatement(ForInStatement statement) : base(statement)
    {
        _leftNode = statement.Left;
        _rightExpression = statement.Right;
        _forBody = statement.Body;
        _iterationKind = IterationKind.Enumerate;
    }

    public JintForInForOfStatement(ForOfStatement statement) : base(statement)
    {
        _leftNode = statement.Left;
        _rightExpression = statement.Right;
        _forBody = statement.Body;
        _iterationKind = IterationKind.Iterate;
    }

    protected override void Initialize(EvaluationContext context2)
    {
        _lhsKind = LhsKind.Assignment;
        _disposeHint = DisposeHint.Normal;
        switch (_leftNode)
        {
            case VariableDeclaration variableDeclaration:
                {
                    _lhsKind = variableDeclaration.Kind == VariableDeclarationKind.Var
                        ? LhsKind.VarBinding
                        : LhsKind.LexicalBinding;

                    _disposeHint = variableDeclaration.Kind.GetDisposeHint();

                    var variableDeclarationDeclaration = variableDeclaration.Declarations[0];
                    var id = variableDeclarationDeclaration.Id;
                    if (_lhsKind == LhsKind.LexicalBinding)
                    {
                        _tdzNames = new List<Key>(1);
                        id.GetBoundNames(_tdzNames);
                    }

                    if (id is DestructuringPattern pattern)
                    {
                        _destructuring = true;
                        _assignmentPattern = pattern;
                    }
                    else
                    {
                        var identifier = (Identifier) id;
                        _expr = new JintIdentifierExpression(identifier);
                    }

                    break;
                }
            case DestructuringPattern pattern:
                _destructuring = true;
                _assignmentPattern = pattern;
                break;
            case MemberExpression memberExpression:
                _expr = new JintMemberExpression(memberExpression);
                break;
            default:
                _expr = new JintIdentifierExpression((Identifier) _leftNode);
                break;
        }

        _body = new ProbablyBlockStatement(_forBody);
        _right = JintExpression.Build(_rightExpression);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var generator = engine.ExecutionContext.Generator;

        // Check if we're resuming from a yield inside this for-of loop
        IteratorInstance? keyResult = null;
        ForOfSuspendData? suspendData = null;
        var resuming = false;

        if (generator is not null && generator._isResuming)
        {
            if (generator.TryGetForOfSuspendData(_statement!, out suspendData))
            {
                // We're resuming into this for-of loop - use the saved iterator
                keyResult = suspendData!.Iterator;
                resuming = true;
            }
        }

        if (!resuming)
        {
            // Normal execution - create new iterator via HeadEvaluation
            if (!HeadEvaluation(context, out keyResult))
            {
                return new Completion(CompletionType.Normal, JsValue.Undefined, _statement);
            }
        }

        return BodyEvaluation(context, _expr, _body, keyResult!, _iterationKind, _lhsKind, suspendData, resuming);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofheadevaluation-tdznames-expr-iterationkind
    /// </summary>
    private bool HeadEvaluation(EvaluationContext context, [NotNullWhen(true)] out IteratorInstance? result)
    {
        var engine = context.Engine;
        var oldEnv = engine.ExecutionContext.LexicalEnvironment;
        var tdz = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
        if (_tdzNames != null)
        {
            var TDZEnvRec = tdz;
            foreach (var name in _tdzNames)
            {
                TDZEnvRec.CreateMutableBinding(name);
            }
        }

        engine.UpdateLexicalEnvironment(tdz);
        var exprValue = _right.GetValue(context);
        engine.UpdateLexicalEnvironment(oldEnv);

        if (_iterationKind == IterationKind.Enumerate)
        {
            if (exprValue.IsNullOrUndefined())
            {
                result = null;
                return false;
            }

            var obj = TypeConverter.ToObject(engine.Realm, exprValue);
            result = new IteratorInstance.EnumerableIterator(engine, obj.GetKeys());
        }
        else
        {
            result = exprValue as IteratorInstance ?? exprValue.GetIterator(engine.Realm);
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset
    /// </summary>
    private Completion BodyEvaluation(
        EvaluationContext context,
        JintExpression? lhs,
        in ProbablyBlockStatement stmt,
        IteratorInstance iteratorRecord,
        IterationKind iterationKind,
        LhsKind lhsKind,
        ForOfSuspendData? suspendData = null,
        bool resuming = false,
        IteratorKind iteratorKind = IteratorKind.Sync)
    {
        var engine = context.Engine;
        var generator = engine.ExecutionContext.Generator;
        var oldEnv = engine.ExecutionContext.LexicalEnvironment;

        // Restore accumulated value if resuming
        var v = suspendData?.AccumulatedValue ?? JsValue.Undefined;
        var destructuring = _destructuring;
        string? lhsName = null;

        var completionType = CompletionType.Normal;
        var close = false;

        try
        {
            while (true)
            {
                DeclarativeEnvironment? iterationEnv = null;
                JsValue nextValue;

                // Skip TryIteratorStep if we're resuming and already have a current value
                // (this happens when yield occurred during body execution)
                if (resuming && suspendData?.CurrentValue is not null)
                {
                    nextValue = suspendData.CurrentValue;
                    iterationEnv = suspendData.IterationEnv;
                    suspendData.CurrentValue = null; // Clear after use
                    resuming = false; // Only skip step on first iteration after resume

                    // Restore the iteration environment if it was saved
                    if (iterationEnv is not null)
                    {
                        engine.UpdateLexicalEnvironment(iterationEnv);
                    }
                }
                else
                {
                    if (!iteratorRecord.TryIteratorStep(out var nextResult))
                    {
                        close = true;
                        // Clean up suspend data on normal completion
                        generator?.ClearForOfSuspendData(_statement!);
                        return new Completion(CompletionType.Normal, v, _statement!);
                    }

                    if (iteratorKind == IteratorKind.Async)
                    {
                        // nextResult = await nextResult;
                        Throw.NotImplementedException("await");
                    }

                    nextValue = nextResult.Get(CommonProperties.Value);
                }

                close = true;

                object lhsRef = null!;
                if (lhsKind != LhsKind.LexicalBinding)
                {
                    if (!destructuring)
                    {
                        lhsRef = lhs!.Evaluate(context);
                    }
                }
                else
                {
                    iterationEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                    if (_tdzNames != null)
                    {
                        BindingInstantiation(iterationEnv);
                    }
                    engine.UpdateLexicalEnvironment(iterationEnv);

                    if (!destructuring)
                    {
                        var identifier = (Identifier) ((VariableDeclaration) _leftNode).Declarations[0].Id;
                        lhsName ??= identifier.Name;
                        lhsRef = engine.ResolveBinding(lhsName);
                    }
                }

                if (context.DebugMode)
                {
                    context.Engine.Debugger.OnStep(_leftNode);
                }

                var status = CompletionType.Normal;
                if (!destructuring)
                {
                    if (context.IsAbrupt())
                    {
                        close = true;
                        status = context.Completion;
                    }
                    else
                    {
                        var reference = (Reference) lhsRef;
                        if (lhsKind == LhsKind.LexicalBinding || _leftNode.Type == NodeType.Identifier && !reference.IsUnresolvableReference)
                        {
                            reference.InitializeReferencedBinding(nextValue, _disposeHint);
                        }
                        else
                        {
                            engine.PutValue(reference, nextValue);
                        }
                    }
                }
                else
                {
                    nextValue = DestructuringPatternAssignmentExpression.ProcessPatterns(
                        context,
                        _assignmentPattern!,
                        nextValue,
                        iterationEnv,
                        checkPatternPropertyReference: _lhsKind != LhsKind.VarBinding);

                    // Check for generator suspension after destructuring
                    if (context.IsGeneratorSuspended())
                    {
                        close = false; // Don't close iterator, we'll resume later
                        completionType = CompletionType.Return;
                        return new Completion(CompletionType.Return, generator?._suspendedValue ?? nextValue, _statement!);
                    }

                    // Check for generator return request after destructuring
                    if (generator?._returnRequested == true)
                    {
                        completionType = CompletionType.Return;
                        close = false; // Prevent double-close in finally
                        generator.ClearForOfSuspendData(_statement!);
                        iteratorRecord.Close(completionType);
                        var returnValue = generator._suspendedValue ?? nextValue;
                        return new Completion(CompletionType.Return, returnValue, _statement!);
                    }

                    status = context.Completion;

                    if (lhsKind == LhsKind.Assignment)
                    {
                        // DestructuringAssignmentEvaluation of assignmentPattern using nextValue as the argument.
                    }
#pragma warning disable MA0140
                    else if (lhsKind == LhsKind.VarBinding)
                    {
                        // BindingInitialization for lhs passing nextValue and undefined as the arguments.
                    }
                    else
                    {
                        // BindingInitialization for lhs passing nextValue and iterationEnv as arguments
                    }
#pragma warning restore MA0140
                }

                if (status != CompletionType.Normal)
                {
                    engine.UpdateLexicalEnvironment(oldEnv);
                    generator?.ClearForOfSuspendData(_statement!);
                    if (_iterationKind == IterationKind.AsyncIterate)
                    {
                        iteratorRecord.Close(status);
                        return new Completion(status, nextValue, context.LastSyntaxElement);
                    }

                    if (iterationKind == IterationKind.Enumerate)
                    {
                        return new Completion(status, nextValue, context.LastSyntaxElement);
                    }

                    iteratorRecord.Close(status);
                    return new Completion(status, nextValue, context.LastSyntaxElement);
                }

                // Before executing body, save state in case of yield
                if (generator is not null)
                {
                    var data = generator.GetOrCreateForOfSuspendData(_statement!, iteratorRecord);
                    data.AccumulatedValue = v;
                    data.CurrentValue = nextValue;
                    data.IterationEnv = iterationEnv;
                }

                var result = stmt.Execute(context);

                // Clear current value after successful body execution (not suspended)
                if (generator is not null && !context.IsGeneratorSuspended())
                {
                    if (generator.TryGetForOfSuspendData(_statement!, out var currentData))
                    {
                        currentData!.CurrentValue = null;
                    }
                }

                result = iterationEnv?.DisposeResources(result) ?? result;
                engine.UpdateLexicalEnvironment(oldEnv);

                if (!result.Value.IsEmpty)
                {
                    v = result.Value;
                    // Update accumulated value in suspend data
                    if (generator is not null && generator.TryGetForOfSuspendData(_statement!, out var data))
                    {
                        data!.AccumulatedValue = v;
                    }
                }

                // Check for generator suspension - if the generator is suspended, we need to exit the loop
                if (context.IsGeneratorSuspended())
                {
                    // Iterator is already saved in suspend data, just exit
                    close = false; // Don't close - we'll resume
                    var suspendedValue = generator?._suspendedValue ?? result.Value;
                    completionType = CompletionType.Return;
                    return new Completion(CompletionType.Return, suspendedValue, _statement!);
                }

                // Check for generator return request (generator.return() was called)
                if (generator?._returnRequested == true)
                {
                    // Close iterator with Return completion
                    completionType = CompletionType.Return;
                    close = false; // Prevent double-close in finally
                    generator.ClearForOfSuspendData(_statement!);
                    iteratorRecord.Close(completionType);
                    var returnValue = generator._suspendedValue ?? result.Value;
                    return new Completion(CompletionType.Return, returnValue, _statement!);
                }

                if (result.Type == CompletionType.Break && (context.Target == null || string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    completionType = CompletionType.Normal;
                    generator?.ClearForOfSuspendData(_statement!);
                    return new Completion(CompletionType.Normal, v, _statement!);
                }

                if (result.Type != CompletionType.Continue || (context.Target != null && !string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    completionType = result.Type;
                    if (result.IsAbrupt())
                    {
                        close = true;
                        generator?.ClearForOfSuspendData(_statement!);
                        return result;
                    }
                }
            }
        }
        catch
        {
            completionType = CompletionType.Throw;
            generator?.ClearForOfSuspendData(_statement!);
            throw;
        }
        finally
        {
            if (close)
            {
                generator?.ClearForOfSuspendData(_statement!);
                try
                {
                    iteratorRecord.Close(completionType);
                }
                catch
                {
                    // if we already have and exception, use it
                    if (completionType != CompletionType.Throw)
                    {
#pragma warning disable CA2219
#pragma warning disable MA0072
                        throw;
#pragma warning restore MA0072
#pragma warning restore CA2219
                    }
                }
            }
            engine.UpdateLexicalEnvironment(oldEnv);
        }
    }

    private void BindingInstantiation(Environment environment)
    {
        var envRec = (DeclarativeEnvironment) environment;
        var variableDeclaration = (VariableDeclaration) _leftNode;
        var boundNames = new List<Key>();
        variableDeclaration.GetBoundNames(boundNames);
        for (var i = 0; i < boundNames.Count; i++)
        {
            var name = boundNames[i];
            if (variableDeclaration.Kind == VariableDeclarationKind.Const)
            {
                envRec.CreateImmutableBinding(name, strict: true);
            }
            else
            {
                envRec.CreateMutableBinding(name, canBeDeleted: false);
            }
        }
    }

    private enum LhsKind
    {
        Assignment,
        VarBinding,
        LexicalBinding
    }

    private enum IteratorKind
    {
        Sync,
        Async
    }

    private enum IterationKind
    {
        Enumerate,
        Iterate,
        AsyncIterate
    }
}
