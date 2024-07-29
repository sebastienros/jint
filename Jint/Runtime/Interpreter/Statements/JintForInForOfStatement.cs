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

    protected override void Initialize(EvaluationContext context)
    {
        _lhsKind = LhsKind.Assignment;
        var engine = context.Engine;
        if (_leftNode is VariableDeclaration variableDeclaration)
        {
            _lhsKind = variableDeclaration.Kind == VariableDeclarationKind.Var
                ? LhsKind.VarBinding
                : LhsKind.LexicalBinding;

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
        }
        else if (_leftNode is DestructuringPattern pattern)
        {
            _destructuring = true;
            _assignmentPattern = pattern;
        }
        else if (_leftNode is MemberExpression memberExpression)
        {
            _expr = new JintMemberExpression(memberExpression);
        }
        else
        {
            _expr = new JintIdentifierExpression((Identifier) _leftNode);
        }

        _body = new ProbablyBlockStatement(_forBody);
        _right = JintExpression.Build(_rightExpression);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        if (!HeadEvaluation(context, out var keyResult))
        {
            return new Completion(CompletionType.Normal, JsValue.Undefined, _statement);
        }

        return BodyEvaluation(context, _expr, _body, keyResult, IterationKind.Enumerate, _lhsKind);
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
        IteratorKind iteratorKind = IteratorKind.Sync)
    {
        var engine = context.Engine;
        var oldEnv = engine.ExecutionContext.LexicalEnvironment;
        var v = JsValue.Undefined;
        var destructuring = _destructuring;
        string? lhsName = null;

        var completionType = CompletionType.Normal;
        var close = false;

        try
        {
            while (true)
            {
                Environment? iterationEnv = null;
                if (!iteratorRecord.TryIteratorStep(out var nextResult))
                {
                    close = true;
                    return new Completion(CompletionType.Normal, v, _statement!);
                }

                if (iteratorKind == IteratorKind.Async)
                {
                    // nextResult = await nextResult;
                    ExceptionHelper.ThrowNotImplementedException("await");
                }

                var nextValue = nextResult.Get(CommonProperties.Value);
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
                            reference.InitializeReferencedBinding(nextValue);
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

                var result = stmt.Execute(context);
                engine.UpdateLexicalEnvironment(oldEnv);

                if (!result.Value.IsEmpty)
                {
                    v = result.Value;
                }

                if (result.Type == CompletionType.Break && (context.Target == null || string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    completionType = CompletionType.Normal;
                    return new Completion(CompletionType.Normal, v, _statement!);
                }

                if (result.Type != CompletionType.Continue || (context.Target != null && !string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    completionType = result.Type;
                    if (iterationKind == IterationKind.Enumerate)
                    {
                        // TODO es6-generators make sure we can start from where we left off
                        //return result;
                    }
                    if (result.IsAbrupt())
                    {
                        close = true;
                        return result;
                    }
                }
            }
        }
        catch
        {
            completionType = CompletionType.Throw;
            throw;
        }
        finally
        {
            if (close)
            {
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