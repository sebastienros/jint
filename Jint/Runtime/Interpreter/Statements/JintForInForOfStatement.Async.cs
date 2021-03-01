using Esprima.Ast;
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-for-in-and-for-of-statements
    /// </summary>
    internal sealed partial class JintForInForOfStatement : JintStatement<Statement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            if (!HeadEvaluation(out var keyResult))
            {
                return new Completion(CompletionType.Normal, JsValue.Undefined, null, Location);
            }

            return await BodyEvaluationAsync(_expr, _body, keyResult, IterationKind.Enumerate, _lhsKind);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset
        /// </summary>
        private async Task<Completion> BodyEvaluationAsync(
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
                            lhsRef = (Reference)lhs.Evaluate();
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
                            lhsName ??= ((Identifier)((VariableDeclaration)_leftNode).Declarations[0].Id).Name;
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

                    var result = await stmt.ExecuteAsync();
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
    }
}