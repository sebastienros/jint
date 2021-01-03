using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-for-in-and-for-of-statements
    /// </summary>
    internal sealed class JintForInForOfStatement : JintStatement<Statement>
    {
        private readonly Node _leftNode;
        private readonly Statement _forBody;
        private readonly Expression _rightExpression;
        private readonly IterationKind _iterationKind;

        private JintStatement _body;
        private JintExpression _expr;
        private BindingPattern _assignmentPattern;
        private JintExpression _right;
        private List<string> _tdzNames;
        private bool _destructuring;
        private LhsKind _lhsKind;

        public JintForInForOfStatement(
            Engine engine, 
            ForInStatement statement) : base(engine, statement)
        {
            _initialized = false;
            _leftNode = statement.Left;
            _rightExpression = statement.Right;
            _forBody = statement.Body;
            _iterationKind = IterationKind.Enumerate;
        }

        public JintForInForOfStatement(
            Engine engine,
            ForOfStatement statement) : base(engine, statement)
        {
            _initialized = false;
            _leftNode = statement.Left;
            _rightExpression = statement.Right;
            _forBody = statement.Body;
            _iterationKind = IterationKind.Iterate;
        }

        protected override void Initialize()
        {
            _lhsKind = LhsKind.Assignment;
            if (_leftNode is VariableDeclaration variableDeclaration)
            {
                _lhsKind = variableDeclaration.Kind == VariableDeclarationKind.Var 
                    ? LhsKind.VarBinding
                    : LhsKind.LexicalBinding;

                var variableDeclarationDeclaration = variableDeclaration.Declarations[0];
                var id = variableDeclarationDeclaration.Id;
                if (_lhsKind == LhsKind.LexicalBinding)
                {
                    _tdzNames = new List<string>(1);
                    id.GetBoundNames(_tdzNames);
                }

                if (id is BindingPattern bindingPattern)
                {
                    _destructuring = true;
                    _assignmentPattern = bindingPattern;
                }
                else
                {
                    var identifier = (Identifier) id;
                    _expr = new JintIdentifierExpression(_engine, identifier);
                }
            }
            else if (_leftNode is BindingPattern bindingPattern)
            {
                _destructuring = true;
                _assignmentPattern = bindingPattern;
            }
            else if (_leftNode is MemberExpression memberExpression)
            {
                _expr = new JintMemberExpression(_engine, memberExpression);
            }
            else
            {
                _expr = new JintIdentifierExpression(_engine, (Identifier) _leftNode);
            }

            _body = Build(_engine, _forBody);
            _right = JintExpression.Build(_engine, _rightExpression);
        }

        protected override Completion ExecuteInternal()
        {
            if (!HeadEvaluation(out var keyResult))
            {
                return new Completion(CompletionType.Normal, JsValue.Undefined, null, Location);
            }

            return BodyEvaluation(_expr, _body, keyResult, IterationKind.Enumerate, _lhsKind);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofheadevaluation-tdznames-expr-iterationkind
        /// </summary>
        private bool HeadEvaluation(out IIterator result)
        {
            var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
            var tdz = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
            if (_tdzNames != null)
            {
                var TDZEnvRec = tdz._record;
                foreach (var name in _tdzNames)
                {
                    TDZEnvRec.CreateMutableBinding(name);
                }
            }

            _engine.UpdateLexicalEnvironment(tdz);
            var exprRef = _right.Evaluate();
            _engine.UpdateLexicalEnvironment(oldEnv);

            var exprValue = _engine.GetValue(exprRef, true);
            if (_iterationKind == IterationKind.Enumerate)
            {
                if (exprValue.IsNullOrUndefined())
                {
                    result = null;
                    return false;
                }

                var obj = TypeConverter.ToObject(_engine, exprValue);
                result = EnumeratorObjectProperties(obj);
            }
            else
            {
                result = exprValue as IIterator ?? exprValue.GetIterator(_engine);
            }

            return true;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset
        /// </summary>
        private Completion BodyEvaluation(
            JintExpression lhs,
            JintStatement stmt, 
            IIterator iteratorRecord,
            IterationKind iterationKind,
            LhsKind lhsKind,
            IteratorKind iteratorKind = IteratorKind.Sync)
        {
            var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
            var v = Undefined.Instance;
            var destructuring = _destructuring;
            string lhsName = null;

            var completionType = CompletionType.Normal;
            var close = false;

            try
            {
                while (true)
                {
                    LexicalEnvironment iterationEnv = null;
                    if (!iteratorRecord.TryIteratorStep(out var nextResult))
                    {
                        close = true;
                        return new Completion(CompletionType.Normal, v, null, Location);
                    }

                    if (iteratorKind == IteratorKind.Async)
                    {
                        // nextResult = await nextResult;
                    }

                    var nextValue = nextResult.Get(CommonProperties.Value);
                    close = true;

                    Reference lhsRef = null;
                    if (lhsKind != LhsKind.LexicalBinding)
                    {
                        if (!destructuring)
                        {
                            lhsRef = (Reference) lhs.Evaluate();
                        }
                    }
                    else
                    {
                        iterationEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                        if (_tdzNames != null)
                        {
                            BindingInstantiation(iterationEnv);
                        }
                        _engine.UpdateLexicalEnvironment(iterationEnv);

                        if (!destructuring)
                        {
                            lhsName ??= ((Identifier) ((VariableDeclaration) _leftNode).Declarations[0].Id).Name;
                            lhsRef = _engine.ResolveBinding(lhsName);
                        }
                    }

                    if (!destructuring)
                    {
                        // If lhsRef is an abrupt completion, then
                        // Let status be lhsRef.

                        if (lhsKind == LhsKind.LexicalBinding)
                        {
                            lhsRef.InitializeReferencedBinding(nextValue);
                        }
                        else
                        {
                            _engine.PutValue(lhsRef, nextValue);
                        }
                    }
                    else
                    {
                        BindingPatternAssignmentExpression.ProcessPatterns(
                            _engine,
                            _assignmentPattern,
                            nextValue,
                            iterationEnv,
                            checkObjectPatternPropertyReference: _lhsKind != LhsKind.VarBinding);

                        if (lhsKind == LhsKind.Assignment)
                        {
                            // DestructuringAssignmentEvaluation of assignmentPattern using nextValue as the argument.
                        }
                        else if (lhsKind == LhsKind.VarBinding)
                        {
                            // BindingInitialization for lhs passing nextValue and undefined as the arguments.
                        }
                        else
                        {
                            // BindingInitialization for lhs passing nextValue and iterationEnv as arguments                            
                        }
                    }

                    var result = stmt.Execute();
                    _engine.UpdateLexicalEnvironment(oldEnv);
                    
                    if (!ReferenceEquals(result.Value, null))
                    {
                        v = result.Value;
                    }

                    if (result.Type == CompletionType.Break && (result.Identifier == null || result.Identifier == _statement?.LabelSet?.Name))
                    {
                        completionType = CompletionType.Normal;
                        return new Completion(CompletionType.Normal, v, null, Location);
                    }

                    if (result.Type != CompletionType.Continue || (result.Identifier != null && result.Identifier != _statement?.LabelSet?.Name))
                    {
                        completionType = result.Type;
                        if (result.Type != CompletionType.Normal)
                        {
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
                            throw;
                        }
                    }
                }
                _engine.UpdateLexicalEnvironment(oldEnv);
            }
        }

        private void BindingInstantiation(LexicalEnvironment environment)
        {
            var envRec = (DeclarativeEnvironmentRecord) environment._record;
            var variableDeclaration = (VariableDeclaration) _leftNode;
            var boundNames = new List<string>();
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

        private IIterator EnumeratorObjectProperties(ObjectInstance obj)
        {
            return new ObjectKeyVisitor(_engine, obj);
        }

        private enum IterationKind
        {
            Enumerate,
            Iterate,
            AsyncIterate
        }

        internal class ObjectKeyVisitor : IteratorInstance
        {
            public ObjectKeyVisitor(Engine engine, ObjectInstance obj)
                : base(engine, CreateEnumerator(engine, obj))
            {
            }

            private static IEnumerable<JsValue> CreateEnumerator(Engine engine, ObjectInstance obj)
            {
                var visited = new HashSet<JsValue>();
                foreach (var key in obj.GetOwnPropertyKeys(Types.String))
                {
                    var desc = obj.GetOwnProperty(key);
                    if (desc != PropertyDescriptor.Undefined)
                    {
                        visited.Add(key);
                        if (desc.Enumerable)
                        {
                            yield return key;
                        }
                    }
                }

                if (obj.Prototype is null)
                {
                    yield break;
                }

                foreach (var protoKey in CreateEnumerator(engine, obj.Prototype))
                {
                    if (!visited.Contains(protoKey))
                    {
                        yield return protoKey;
                    }
                }
            }
        }
    }
}