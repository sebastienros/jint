using System;
using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
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
            return Completion.Empty;
        }

        public Completion ExecuteExpressionStatement(ExpressionStatement expressionStatement)
        {
            var exprRef = _engine.EvaluateExpression(expressionStatement.Expression);
            return _engine.CompletionPool.Rent(Completion.Normal, _engine.GetValue(exprRef, true), null);
        }

        public Completion ExecuteIfStatement(IfStatement ifStatement)
        {
            Completion result;
            if (TypeConverter.ToBoolean(_engine.GetValue(_engine.EvaluateExpression(ifStatement.Test), true)))
            {
                result = ExecuteStatement(ifStatement.Consequent);
            }
            else if (ifStatement.Alternate != null)
            {
                result = ExecuteStatement(ifStatement.Alternate);
            }
            else
            {
                return Completion.Empty;
            }

            return result;
        }

        public Completion ExecuteLabeledStatement(LabeledStatement labeledStatement)
        {
            // TODO: Esprima added Statement.Label, maybe not necessary as this line is finding the
            // containing label and could keep a table per program with all the labels
            // labeledStatement.Body.LabelSet = labeledStatement.Label;
            var result = ExecuteStatement(labeledStatement.Body);
            if (result.Type == Completion.Break && result.Identifier == labeledStatement.Label.Name)
            {
                var value = result.Value;
                _engine.CompletionPool.Return(result);
                return _engine.CompletionPool.Rent(Completion.Normal, value, null);
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
                if (!ReferenceEquals(stmt.Value, null))
                {
                    v = stmt.Value;
                }
                if (stmt.Type != Completion.Continue || stmt.Identifier != doWhileStatement?.LabelSet?.Name)
                {
                    if (stmt.Type == Completion.Break && (stmt.Identifier == null || stmt.Identifier == doWhileStatement?.LabelSet?.Name))
                    {
                        _engine.CompletionPool.Return(stmt);
                        return _engine.CompletionPool.Rent(Completion.Normal, v, null);
                    }

                    if (stmt.Type != Completion.Normal)
                    {
                        return stmt;
                    }
                }

                _engine.CompletionPool.Return(stmt);
                var exprRef = _engine.EvaluateExpression(doWhileStatement.Test);
                iterating = TypeConverter.ToBoolean(_engine.GetValue(exprRef, true));

            } while (iterating);

            return _engine.CompletionPool.Rent(Completion.Normal, v, null);
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
                var jsValue = _engine.GetValue(_engine.EvaluateExpression(whileStatement.Test), true);
                if (!TypeConverter.ToBoolean(jsValue))
                {
                    return _engine.CompletionPool.Rent(Completion.Normal, v, null);
                }

                var stmt = ExecuteStatement(whileStatement.Body);

                if (!ReferenceEquals(stmt.Value, null))
                {
                    v = stmt.Value;
                }

                if (stmt.Type != Completion.Continue || stmt.Identifier != whileStatement?.LabelSet?.Name)
                {
                    if (stmt.Type == Completion.Break && (stmt.Identifier == null || stmt.Identifier == whileStatement?.LabelSet?.Name))
                    {
                        _engine.CompletionPool.Return(stmt);
                        return _engine.CompletionPool.Rent(Completion.Normal, v, null);
                    }

                    if (stmt.Type != Completion.Normal)
                    {
                        return stmt;
                    }
                }

                _engine.CompletionPool.Return(stmt);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.3
        /// </summary>
        /// <param name="forStatement"></param>
        /// <returns></returns>
        public Completion ExecuteForStatement(ForStatement forStatement)
        {
            var init = forStatement.Init;
            if (init != null)
            {
                if (init.Type == Nodes.VariableDeclaration)
                {
                    var c = ExecuteStatement((Statement) init);
                    _engine.CompletionPool.Return(c);

                }
                else
                {
                    _engine.GetValue(_engine.EvaluateExpression(init), true);
                }
            }

            JsValue v = Undefined.Instance;
            while (true)
            {
                if (forStatement.Test != null)
                {
                    var testExprRef = _engine.EvaluateExpression(forStatement.Test);
                    if (!TypeConverter.ToBoolean(_engine.GetValue(testExprRef, true)))
                    {
                        return _engine.CompletionPool.Rent(Completion.Normal, v, null);
                    }
                }

                var stmt = ExecuteStatement(forStatement.Body);
                if (!ReferenceEquals(stmt.Value, null))
                {
                    v = stmt.Value;
                }
                if (stmt.Type == Completion.Break && (stmt.Identifier == null || stmt.Identifier == forStatement?.LabelSet?.Name))
                {
                    _engine.CompletionPool.Return(stmt);
                    return _engine.CompletionPool.Rent(Completion.Normal, v, null);
                }
                if (stmt.Type != Completion.Continue || ((stmt.Identifier != null) && stmt.Identifier != forStatement?.LabelSet?.Name))
                {
                    if (stmt.Type != Completion.Normal)
                    {
                        return stmt;
                    }
                }
                if (forStatement.Update != null)
                {
                    _engine.GetValue(_engine.EvaluateExpression(forStatement.Update), true);
                }
                _engine.CompletionPool.Return(stmt);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.4
        /// </summary>
        /// <param name="forInStatement"></param>
        /// <returns></returns>
        public Completion ExecuteForInStatement(ForInStatement forInStatement)
        {
            var identifier = forInStatement.Left.Type == Nodes.VariableDeclaration
                ? (Identifier) ((VariableDeclaration) forInStatement.Left).Declarations[0].Id
                : (Identifier) forInStatement.Left;

            var varRef = _engine.EvaluateExpression(identifier) as Reference;
            var experValue = _engine.GetValue(_engine.EvaluateExpression(forInStatement.Right), true);
            if (ReferenceEquals(experValue, Undefined.Instance) || ReferenceEquals(experValue,  Null.Instance))
            {
                return Completion.Empty;
            }

            var obj = TypeConverter.ToObject(_engine, experValue);
            JsValue v = Null.Instance;

            // keys are constructed using the prototype chain
            var cursor = obj;
            var processedKeys = new HashSet<string>();

            while (!ReferenceEquals(cursor, null))
            {
                var keys = _engine.Object.GetOwnPropertyNames(Undefined.Instance, Arguments.From(cursor)).AsArray();

                for (var i = 0; i < keys.GetLength(); i++)
                {
                    var p = keys.GetOwnProperty(TypeConverter.ToString(i)).Value.AsString();

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
                    if (!value.Enumerable)
                    {
                        continue;
                    }

                    _engine.PutValue(varRef, p);

                    var stmt = ExecuteStatement(forInStatement.Body);
                    if (!ReferenceEquals(stmt.Value, null))
                    {
                        v = stmt.Value;
                    }
                    if (stmt.Type == Completion.Break)
                    {
                        _engine.CompletionPool.Return(stmt);
                        return _engine.CompletionPool.Rent(Completion.Normal, v, null);
                    }
                    if (stmt.Type != Completion.Continue)
                    {
                        if (stmt.Type != Completion.Normal)
                        {
                            return stmt;
                        }
                    }
                    _engine.CompletionPool.Return(stmt);
                }

                cursor = cursor.Prototype;
            }

            return _engine.CompletionPool.Rent(Completion.Normal, v, null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.7
        /// </summary>
        /// <param name="continueStatement"></param>
        /// <returns></returns>
        public Completion ExecuteContinueStatement(ContinueStatement continueStatement)
        {
            return _engine.CompletionPool.Rent(
                Completion.Continue,
                null,
                continueStatement.Label != null ? continueStatement.Label.Name : null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.8
        /// </summary>
        /// <param name="breakStatement"></param>
        /// <returns></returns>
        public Completion ExecuteBreakStatement(BreakStatement breakStatement)
        {
            return _engine.CompletionPool.Rent(
                Completion.Break,
                null,
                breakStatement.Label != null ? breakStatement.Label.Name : null);
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
                return _engine.CompletionPool.Rent(Completion.Return, Undefined.Instance, null);
            }

            var jsValue = _engine.GetValue(_engine.EvaluateExpression(statement.Argument), true);
            return _engine.CompletionPool.Rent(Completion.Return, jsValue, null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.10
        /// </summary>
        /// <param name="withStatement"></param>
        /// <returns></returns>
        public Completion ExecuteWithStatement(WithStatement withStatement)
        {
            var jsValue = _engine.GetValue(_engine.EvaluateExpression(withStatement.Object), true);
            var obj = TypeConverter.ToObject(_engine, jsValue);
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
                c = _engine.CompletionPool.Rent(Completion.Throw, e.Error, null, withStatement.Location);
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
            var jsValue = _engine.GetValue(_engine.EvaluateExpression(switchStatement.Discriminant), true);
            var r = ExecuteSwitchBlock(switchStatement.Cases, jsValue);
            if (r.Type == Completion.Break && r.Identifier == switchStatement.LabelSet?.Name)
            {
                return _engine.CompletionPool.Rent(Completion.Normal, r.Value, null);
            }
            return r;
        }

        public Completion ExecuteSwitchBlock(List<SwitchCase> switchBlock, JsValue input)
        {
            JsValue v = Undefined.Instance;
            SwitchCase defaultCase = null;
            bool hit = false;

            var switchBlockCount = switchBlock.Count;
            for (var i = 0; i < switchBlockCount; i++)
            {
                var clause = switchBlock[i];
                if (clause.Test == null)
                {
                    defaultCase = clause;
                }
                else
                {
                    var clauseSelector = _engine.GetValue(_engine.EvaluateExpression(clause.Test), true);
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

                    v = r.Value ?? Undefined.Instance;
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

                v = r.Value ?? Undefined.Instance;
            }

            return _engine.CompletionPool.Rent(Completion.Normal, v, null);
        }

        public Completion ExecuteStatementList(List<StatementListItem> statementList)
        {
            var c = Completion.Empty;
            Completion sl = c;
            Statement s = null;

            try
            {
                var statementListCount = statementList.Count;
                for (var i = 0; i < statementListCount; i++)
                {
                    s = (Statement) statementList[i];
                    c = ExecuteStatement(s);
                    if (c.Type != Completion.Normal)
                    {
                        var executeStatementList = _engine.CompletionPool.Rent(
                            c.Type,
                            c.Value ?? sl.Value,
                            c.Identifier,
                            c.Location);

                        _engine.CompletionPool.Return(sl);
                        _engine.CompletionPool.Return(c);
                        return executeStatementList;
                    }

                    if (sl != c)
                    {
                        _engine.CompletionPool.Return(sl);
                    }

                    sl = c;
                }
            }
            catch (JavaScriptException v)
            {
                var completion = _engine.CompletionPool.Rent(Completion.Throw, v.Error, null, v.Location ?? s.Location);
                return completion;
            }

            var rent = _engine.CompletionPool.Rent(c.Type, c.GetValueOrDefault(), c.Identifier);
            _engine.CompletionPool.Return(c);
            return rent;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.13
        /// </summary>
        /// <param name="throwStatement"></param>
        /// <returns></returns>
        public Completion ExecuteThrowStatement(ThrowStatement throwStatement)
        {
            var jsValue = _engine.GetValue(_engine.EvaluateExpression(throwStatement.Argument), true);
            return _engine.CompletionPool.Rent(Completion.Throw, jsValue, null, throwStatement.Location);
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
                var catchClause = tryStatement.Handler;
                if (catchClause != null)
                {
                    var c = _engine.GetValue(b);
                    var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                    var catchEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                    catchEnv.Record.CreateMutableBinding(((Identifier) catchClause.Param).Name, c);
                    _engine.ExecutionContext.LexicalEnvironment = catchEnv;
                    b = ExecuteStatement(catchClause.Body);
                    _engine.ExecutionContext.LexicalEnvironment = oldEnv;
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
            var declarationsCount = statement.Declarations.Count;
            for (var i = 0; i < declarationsCount; i++)
            {
                var declaration = statement.Declarations[i];
                if (declaration.Init != null)
                {
                    if (!(_engine.EvaluateExpression(declaration.Id) is Reference lhs))
                    {
                        throw new ArgumentException();
                    }

                    if (lhs.IsStrict()
                        && lhs.GetBase() is EnvironmentRecord
                        && (lhs.GetReferencedName() == "eval" || lhs.GetReferencedName() == "arguments"))
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }

                    var value = _engine.GetValue(_engine.EvaluateExpression(declaration.Init), true);
                    _engine.PutValue(lhs, value);
                    _engine.ReferencePool.Return(lhs);
                }
            }

            return Completion.EmptyUndefined;
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

            return Completion.Empty;
        }
    }
}
