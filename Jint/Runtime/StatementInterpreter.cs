using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using Jint.Parser.Ast;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime
{
    public class StatementInterpreter
    {
        private readonly Engine _engine;

        public StatementInterpreter(Engine engine)
        {
            _engine = engine;
        }

        private Completion ExecuteStatement(Statement statement)
        {
            return _engine.ExecuteStatement(statement);
        }

        public Completion ExecuteEmptyStatement(EmptyStatement emptyStatement)
        {
            return new Completion(Completion.Normal, null, null);
        }

        public Completion ExecuteExpressionStatement(ExpressionStatement expressionStatement)
        {
            var exprRef = _engine.EvaluateExpression(expressionStatement.Expression);
            return new Completion(Completion.Normal, _engine.GetValue(exprRef), null);
        }

        public Completion ExecuteIfStatement(IfStatement ifStatement)
        {
            var exprRef = _engine.EvaluateExpression(ifStatement.Test);
            Completion result;

            if (TypeConverter.ToBoolean(_engine.GetValue(exprRef)))
            {
                result = ExecuteStatement(ifStatement.Consequent);
            }
            else if (ifStatement.Alternate != null)
            {
                result = ExecuteStatement(ifStatement.Alternate);
            }
            else
            {
                return new Completion(Completion.Normal, null, null);
            }

            return result;
        }

        public Completion ExecuteLabelledStatement(LabelledStatement labelledStatement)
        {
            labelledStatement.Body.LabelSet = labelledStatement.Label.Name;
            var result = ExecuteStatement(labelledStatement.Body);
            if (result.Type == Completion.Break && result.Identifier == labelledStatement.Label.Name)
            {
                return new Completion(Completion.Normal, result.Value, null);
            }

            return result;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.1
        /// </summary>
        /// <param name="doWhileStatement"></param>
        /// <returns></returns>
        public Completion ExecuteDoWhileStatement(DoWhileStatement doWhileStatement)
        {
            JsValue v = Undefined.Instance;
            bool iterating;

            do
            {
                var stmt = ExecuteStatement(doWhileStatement.Body);
                if (stmt.Value != null)
                {
                    v = stmt.Value;
                }
                if (stmt.Type != Completion.Continue || stmt.Identifier != doWhileStatement.LabelSet)
                {
                    if (stmt.Type == Completion.Break && (stmt.Identifier == null || stmt.Identifier == doWhileStatement.LabelSet))
                    {
                        return new Completion(Completion.Normal, v, null);
                    }

                    if (stmt.Type != Completion.Normal)
                    {
                        return stmt;
                    }
                }
                var exprRef = _engine.EvaluateExpression(doWhileStatement.Test);
                iterating = TypeConverter.ToBoolean(_engine.GetValue(exprRef));

            } while (iterating);

            return new Completion(Completion.Normal, v, null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.2
        /// </summary>
        /// <param name="whileStatement"></param>
        /// <returns></returns>
        public Completion ExecuteWhileStatement(WhileStatement whileStatement)
        {
            JsValue v = Undefined.Instance;
            while (true)
            {
                var exprRef = _engine.EvaluateExpression(whileStatement.Test);

                if (!TypeConverter.ToBoolean(_engine.GetValue(exprRef)))
                {
                    return new Completion(Completion.Normal, v, null);
                }

                var stmt = ExecuteStatement(whileStatement.Body);

                if (stmt.Value != null)
                {
                    v = stmt.Value;
                }

                if (stmt.Type != Completion.Continue || stmt.Identifier != whileStatement.LabelSet)
                {
                    if (stmt.Type == Completion.Break && (stmt.Identifier == null || stmt.Identifier == whileStatement.LabelSet))
                    {
                        return new Completion(Completion.Normal, v, null);
                    }

                    if (stmt.Type != Completion.Normal)
                    {
                        return stmt;
                    }
                }
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.3
        /// </summary>
        /// <param name="forStatement"></param>
        /// <returns></returns>
        public Completion ExecuteForStatement(ForStatement forStatement)
        {

            if (forStatement.Init != null)
            {
                if (forStatement.Init.Type == SyntaxNodes.VariableDeclaration)
                {
                    ExecuteStatement(forStatement.Init.As<Statement>());
                }
                else
                {
                    _engine.GetValue(_engine.EvaluateExpression(forStatement.Init.As<Expression>()));
                }
            }

            JsValue v = Undefined.Instance;
            while (true)
            {
                if (forStatement.Test != null)
                {
                    var testExprRef = _engine.EvaluateExpression(forStatement.Test);
                    if (!TypeConverter.ToBoolean(_engine.GetValue(testExprRef)))
                    {
                        return new Completion(Completion.Normal, v, null);
                    }
                }

                var stmt = ExecuteStatement(forStatement.Body);
                if (stmt.Value != null)
                {
                    v = stmt.Value;
                }
                if (stmt.Type == Completion.Break && (stmt.Identifier == null || stmt.Identifier == forStatement.LabelSet))
                {
                    return new Completion(Completion.Normal, v, null);
                }
                if (stmt.Type != Completion.Continue || ((stmt.Identifier != null) && stmt.Identifier != forStatement.LabelSet))
                {
                    if (stmt.Type != Completion.Normal)
                    {
                        return stmt;
                    }
                }
                if (forStatement.Update != null)
                {
                    var incExprRef = _engine.EvaluateExpression(forStatement.Update);
                    _engine.GetValue(incExprRef);
                }
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.4
        /// </summary>
        /// <param name="forInStatement"></param>
        /// <returns></returns>
        public Completion ExecuteForInStatement(ForInStatement forInStatement)
        {
            Identifier identifier = forInStatement.Left.Type == SyntaxNodes.VariableDeclaration
                                        ? forInStatement.Left.As<VariableDeclaration>().Declarations.First().Id
                                        : forInStatement.Left.As<Identifier>();

            var varRef = _engine.EvaluateExpression(identifier) as Reference;
            var exprRef = _engine.EvaluateExpression(forInStatement.Right);
            var experValue = _engine.GetValue(exprRef);
            if (experValue == Undefined.Instance || experValue == Null.Instance)
            {
                return new Completion(Completion.Normal, null, null);
            }


            var obj = TypeConverter.ToObject(_engine, experValue);
            JsValue v = Null.Instance;

            // keys are constructed using the prototype chain
            var cursor = obj;
            var processedKeys = new HashSet<string>();

            while (cursor != null)
            {
                var keys = _engine.Object.GetOwnPropertyNames(Undefined.Instance, Arguments.From(cursor)).AsArray();

                for (var i = 0; i < keys.GetLength(); i++)
                {
                    var p = keys.GetOwnProperty(i.ToString()).Value.AsString();

                    if (processedKeys.Contains(p))
                    {
                        continue;
                    }

                    processedKeys.Add(p);

                    // collection might be modified by inner statement
                    if (cursor.GetOwnProperty(p) == PropertyDescriptor.Undefined)
                    {
                        continue;
                    }


                    var value = cursor.GetOwnProperty(p);
                    if (!value.Enumerable.HasValue || !value.Enumerable.Value)
                    {
                        continue;
                    }

                    _engine.PutValue(varRef, p);

                    var stmt = ExecuteStatement(forInStatement.Body);
                    if (stmt.Value != null)
                    {
                        v = stmt.Value;
                    }
                    if (stmt.Type == Completion.Break)
                    {
                        return new Completion(Completion.Normal, v, null);
                    }
                    if (stmt.Type != Completion.Continue)
                    {
                        if (stmt.Type != Completion.Normal)
                        {
                            return stmt;
                        }
                    }
                }

                cursor = cursor.Prototype;
            }

            return new Completion(Completion.Normal, v, null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.7
        /// </summary>
        /// <param name="continueStatement"></param>
        /// <returns></returns>
        public Completion ExecuteContinueStatement(ContinueStatement continueStatement)
        {
            return new Completion(Completion.Continue, null, continueStatement.Label != null ? continueStatement.Label.Name : null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.8
        /// </summary>
        /// <param name="breakStatement"></param>
        /// <returns></returns>
        public Completion ExecuteBreakStatement(BreakStatement breakStatement)
        {
            return new Completion(Completion.Break, null, breakStatement.Label != null ? breakStatement.Label.Name : null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.9
        /// </summary>
        /// <param name="statement"></param>
        /// <returns></returns>
        public Completion ExecuteReturnStatement(ReturnStatement statement)
        {
            if (statement.Argument == null)
            {
                return new Completion(Completion.Return, Undefined.Instance, null);
            }

            var exprRef = _engine.EvaluateExpression(statement.Argument);
            return new Completion(Completion.Return, _engine.GetValue(exprRef), null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.10
        /// </summary>
        /// <param name="withStatement"></param>
        /// <returns></returns>
        public Completion ExecuteWithStatement(WithStatement withStatement)
        {
            var val = _engine.EvaluateExpression(withStatement.Object);
            var obj = TypeConverter.ToObject(_engine, _engine.GetValue(val));
            var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
            var newEnv = LexicalEnvironment.NewObjectEnvironment(_engine, obj, oldEnv, true);
            _engine.ExecutionContext.LexicalEnvironment = newEnv;

            Completion c;
            try
            {
                c = ExecuteStatement(withStatement.Body);
            }
            catch (JavaScriptException e)
            {
                c = new Completion(Completion.Throw, e.Error, null);
                c.Location = withStatement.Location;
            }
            finally
            {
                _engine.ExecutionContext.LexicalEnvironment = oldEnv;
            }

            return c;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.11
        /// </summary>
        /// <param name="switchStatement"></param>
        /// <returns></returns>
        public Completion ExecuteSwitchStatement(SwitchStatement switchStatement)
        {
            var exprRef = _engine.EvaluateExpression(switchStatement.Discriminant);
            var r = ExecuteSwitchBlock(switchStatement.Cases, _engine.GetValue(exprRef));
            if (r.Type == Completion.Break && r.Identifier == switchStatement.LabelSet)
            {
                return new Completion(Completion.Normal, r.Value, null);
            }
            return r;
        }

        public Completion ExecuteSwitchBlock(IEnumerable<SwitchCase> switchBlock, JsValue input)
        {
            JsValue v = Undefined.Instance;
            SwitchCase defaultCase = null;
            bool hit = false;
            foreach (var clause in switchBlock)
            {
                if (clause.Test == null)
                {
                    defaultCase = clause;
                }
                else
                {
                    var clauseSelector = _engine.GetValue(_engine.EvaluateExpression(clause.Test));
                    if (ExpressionInterpreter.StrictlyEqual(clauseSelector, input))
                    {
                        hit = true;
                    }
                }

                if (hit && clause.Consequent != null)
                {
                    var r = ExecuteStatementList(clause.Consequent);
                    if (r.Type != Completion.Normal)
                    {
                        return r;
                    }

                    v = r.Value != null ? r.Value : Undefined.Instance;
                }

            }

            // do we need to execute the default case ?
            if (hit == false && defaultCase != null)
            {
                var r = ExecuteStatementList(defaultCase.Consequent);
                if (r.Type != Completion.Normal)
                {
                    return r;
                }

                v = r.Value != null ? r.Value : Undefined.Instance;
            }

            return new Completion(Completion.Normal, v, null);
        }

        public Completion ExecuteStatementList(IEnumerable<Statement> statementList)
        {
            var c = new Completion(Completion.Normal, null, null);
            Completion sl = c;
            Statement s = null;

            try
            {
                foreach (var statement in statementList)
                {
                    s = statement;
                    c = ExecuteStatement(statement);
                    if (c.Type != Completion.Normal)
                    {
                        return new Completion(c.Type, c.Value != null ? c.Value : sl.Value, c.Identifier)
                        {
                            Location = c.Location
                        };
                    }

                    sl = c;
                }
            }
            catch (JavaScriptException v)
            {
                c = new Completion(Completion.Throw, v.Error, null);
                c.Location = v.Location ?? s.Location;
                return c;
            }

            return new Completion(c.Type, c.GetValueOrDefault(), c.Identifier);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.13
        /// </summary>
        /// <param name="throwStatement"></param>
        /// <returns></returns>
        public Completion ExecuteThrowStatement(ThrowStatement throwStatement)
        {
            var exprRef = _engine.EvaluateExpression(throwStatement.Argument);
            Completion c = new Completion(Completion.Throw, _engine.GetValue(exprRef), null);
            c.Location = throwStatement.Location;
            return c;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.14
        /// </summary>
        /// <param name="tryStatement"></param>
        /// <returns></returns>
        public Completion ExecuteTryStatement(TryStatement tryStatement)
        {
            var b = ExecuteStatement(tryStatement.Block);
            if (b.Type == Completion.Throw)
            {
                // execute catch
                if (tryStatement.Handlers.Any())
                {
                    foreach (var catchClause in tryStatement.Handlers)
                    {
                        var c = _engine.GetValue(b);
                        var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                        var catchEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                        catchEnv.Record.CreateMutableBinding(catchClause.Param.Name);
                        catchEnv.Record.SetMutableBinding(catchClause.Param.Name, c, false);
                        _engine.ExecutionContext.LexicalEnvironment = catchEnv;
                        b = ExecuteStatement(catchClause.Body);
                        _engine.ExecutionContext.LexicalEnvironment = oldEnv;
                    }
                }
            }

            if (tryStatement.Finalizer != null)
            {
                var f = ExecuteStatement(tryStatement.Finalizer);
                if (f.Type == Completion.Normal)
                {
                    return b;
                }

                return f;
            }

            return b;
        }

        public Completion ExecuteProgram(Program program)
        {
            return ExecuteStatementList(program.Body);
        }

        public Completion ExecuteVariableDeclaration(VariableDeclaration statement)
        {
            foreach (var declaration in statement.Declarations)
            {
                if (declaration.Init != null)
                {
                    var lhs = _engine.EvaluateExpression(declaration.Id) as Reference;

                    if (lhs == null)
                    {
                        throw new ArgumentException();
                    }

                    if (lhs.IsStrict() && lhs.GetBase().TryCast<EnvironmentRecord>() != null &&
                        (lhs.GetReferencedName() == "eval" || lhs.GetReferencedName() == "arguments"))
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }

                    lhs.GetReferencedName();
                    var value = _engine.GetValue(_engine.EvaluateExpression(declaration.Init));
                    _engine.PutValue(lhs, value);
                }
            }

            return new Completion(Completion.Normal, Undefined.Instance, null);
        }

        public Completion ExecuteBlockStatement(BlockStatement blockStatement)
        {
            return ExecuteStatementList(blockStatement.Body);
        }

        public Completion ExecuteDebuggerStatement(DebuggerStatement debuggerStatement)
        {
            if (_engine.Options._IsDebuggerStatementAllowed)
            {
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debugger.Launch();
                }

                System.Diagnostics.Debugger.Break();
            }

            return new Completion(Completion.Normal, null, null);
        }
    }
}
