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
    internal sealed class JintForInStatement : JintStatement<ForInStatement>
    {
        private JintStatement _body;
        private JintExpression _expr;
        private IterationKind _iterationKind;
        private BindingPattern _assignmentPattern;
        private JintExpression _right;
        private List<string> _tdzNames;
        private bool _destructuring;
        private LhsKind _lhsKind;

        public JintForInStatement(Engine engine, ForInStatement statement) : base(engine, statement)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            _lhsKind = LhsKind.Assignment;
            if (_statement.Left is VariableDeclaration variableDeclaration)
            {
                _lhsKind = variableDeclaration.Kind == VariableDeclarationKind.Var 
                    ? LhsKind.VarBinding
                    : LhsKind.LexicalBinding;

                var variableDeclarationDeclaration = variableDeclaration.Declarations[0];
                var id = variableDeclarationDeclaration.Id;
                if (_lhsKind == LhsKind.LexicalBinding)
                {
                    _tdzNames = new List<string>(1);
                    GetBoundNames(id, _tdzNames);
                }

                _iterationKind = IterationKind.Enumerate;
                if (id is BindingPattern bindingPattern)
                {
                    _assignmentPattern = bindingPattern;
                    _destructuring = true;
                }
                else
                {
                    var identifier = (Identifier) id;
                    _expr = new JintIdentifierExpression(_engine, identifier);
                }
            }
            else if (_statement.Left is BindingPattern bindingPattern)
            {
                _iterationKind = IterationKind.Iterate;
                _assignmentPattern = bindingPattern;
            }
            else if (_statement.Left is MemberExpression memberExpression)
            {
                _iterationKind = IterationKind.Enumerate;
                _expr = new JintMemberExpression(_engine, memberExpression);
            }
            else
            {
                _iterationKind = IterationKind.Enumerate;
                _expr = new JintIdentifierExpression(_engine, (Identifier) _statement.Left);
            }

            _body = Build(_engine, _statement.Body);
            _right = JintExpression.Build(_engine, _statement.Right);
        }

        protected override Completion ExecuteInternal()
        {
            if (!HeadEvaluation(out var keyResult))
            {
                return new Completion(CompletionType.Normal, JsValue.Undefined, null, Location);
            }

            return BodyEvaluation(_expr, _body, keyResult, IterationKind.Enumerate, _lhsKind, false);
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
                result = exprValue.GetIterator(_engine);
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
            bool labelSet, 
            IteratorKind iteratorKind = IteratorKind.Sync)
        {
            var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
            var v = Undefined.Instance;
            var destructuring = _destructuring;
            string lhsName = null;

            try
            {
                while (true)
                {
                    if (!iteratorRecord.TryIteratorStep(out var nextResult))
                    {
                        return new Completion(CompletionType.Normal, v, null, Location);
                    }

                    if (iteratorKind == IteratorKind.Async)
                    {
                        // nextResult = await nextResult;
                    }

                    nextResult.TryGetValue("value", out var nextValue);

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
                        var iterationEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                        if (_tdzNames != null)
                        {
                            BindingInstantiation(iterationEnv);
                        }
                        _engine.UpdateLexicalEnvironment(iterationEnv);

                        if (!destructuring)
                        {
                            lhsName ??= ((Identifier) ((VariableDeclaration) _statement.Left).Declarations[0].Id).Name;
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
                            !(_statement.Left is VariableDeclaration));

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

                    if (result.Type == CompletionType.Break)
                    {
                        return new Completion(CompletionType.Normal, v, null, Location);
                    }

                    if (result.Type != CompletionType.Continue)
                    {
                        if (result.Type != CompletionType.Normal)
                        {
                            return result;
                        }
                    }
                }
            }
            catch
            {
                if (iteratorKind == IteratorKind.Async)
                {
                    //  AsyncIteratorClose(iteratorRecord, status).
                }

                if (iterationKind == IterationKind.Enumerate)
                {
                    // ok
                }
                else
                {
                    iteratorRecord.Close(CompletionType.Throw);
                }

                throw;
            }
            finally
            {
                _engine.UpdateLexicalEnvironment(oldEnv);
            }
        }

        private void BindingInstantiation(LexicalEnvironment environment)
        {
            var envRec = (DeclarativeEnvironmentRecord) environment._record;
            var variableDeclaration = (VariableDeclaration) _statement.Left;
            ref readonly var declarations = ref variableDeclaration.Declarations;
            var boundNames = new List<string>();
            for (var i = 0; i < declarations.Count; i++)
            {
                boundNames.Clear();
                GetBoundNames(declarations[i].Id, boundNames);
                for (var j = 0; j < boundNames.Count; j++)
                {
                    var name = boundNames[j];
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
        }

        private static void GetBoundNames(Node parameter, List<string> target)
        {
            if (parameter.Type == Nodes.Literal)
            {
                return;
            }
            
            if (parameter is Identifier identifier)
            {
                target.Add(identifier.Name);
                return;
            }

            while (true)
            {
                if (parameter is RestElement restElement)
                {
                    parameter = restElement.Argument;
                    continue;
                }
                else if (parameter is ArrayPattern arrayPattern)
                {
                    ref readonly var arrayPatternElements = ref arrayPattern.Elements;
                    for (var i = 0; i < arrayPatternElements.Count; i++)
                    {
                        var expression = arrayPatternElements[i];
                        GetBoundNames(expression, target);
                    }
                }
                else if (parameter is ObjectPattern objectPattern)
                {
                    ref readonly var objectPatternProperties = ref objectPattern.Properties;
                    for (var i = 0; i < objectPatternProperties.Count; i++)
                    {
                        var property = objectPatternProperties[i];
                        if (property is Property p)
                        {
                            GetBoundNames(p.Value, target);
                        }
                        else
                        {
                            GetBoundNames((RestElement) property, target);
                        }
                    }
                }
                else if (parameter is AssignmentPattern assignmentPattern)
                {
                    parameter = assignmentPattern.Left;
                    continue;
                }
                break;
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