using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using Jint.Parser.Ast;
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
            try
            {
                return _engine.ExecuteStatement(statement);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(statement.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(statement.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), statement.Location);
            }
        }

        public Completion ExecuteEmptyStatement(EmptyStatement emptyStatement)
        {
            try
            {
                return new Completion(Completion.Normal, null, null);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(emptyStatement.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(emptyStatement.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), emptyStatement.Location);
            }
        }

        public Completion ExecuteExpressionStatement(ExpressionStatement expressionStatement)
        {
            try
            {
                var exprRef = _engine.EvaluateExpression(expressionStatement.Expression);
                return new Completion(Completion.Normal, _engine.GetValue(exprRef), null);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(expressionStatement.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(expressionStatement.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), expressionStatement.Location);
            }
        }

        public Completion ExecuteIfStatement(IfStatement ifStatement)
        {
            try
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
            catch (JavaScriptException ex)
            {
                ex.InitLocation(ifStatement.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(ifStatement.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), ifStatement.Location);
            }
        }

        public Completion ExecuteLabelledStatement(LabelledStatement labelledStatement)
        {
            try
            {
                labelledStatement.Body.LabelSet = labelledStatement.Label.Name;
                var result = ExecuteStatement(labelledStatement.Body);
                if (result.Type == Completion.Break && result.Identifier == labelledStatement.Label.Name)
                {
                    return new Completion(Completion.Normal, result.Value, null);
                }

                return result;
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(labelledStatement.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(labelledStatement.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), labelledStatement.Location);
            }
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

            try
            {
                do
                {
                    var stmt = ExecuteStatement(doWhileStatement.Body);

                    try
                    {
                        if (stmt.Value.HasValue)
                        {
                            v = stmt.Value.Value;
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
                    }
                    catch (JavaScriptException ex)
                    {
                        ex.InitLocation(stmt.Location);
                        throw;
                    }
                    catch (StatementsCountOverflowException ex)
                    {
                        ex.InitLocation(stmt.Location);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new JavaScriptException(new JsValue(ex.Message), stmt.Location);
                    }
                } while (iterating);

                return new Completion(Completion.Normal, v, null);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(doWhileStatement.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(doWhileStatement.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), doWhileStatement.Location);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.2
        /// </summary>
        /// <param name="whileStatement"></param>
        /// <returns></returns>
        public Completion ExecuteWhileStatement(WhileStatement whileStatement)
        {
            try
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

                    if (stmt.Value.HasValue)
                    {
                        v = stmt.Value.Value;
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
            catch (JavaScriptException ex)
            {
                ex.InitLocation(whileStatement.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(whileStatement.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), whileStatement.Location);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.3
        /// </summary>
        /// <param name="forStatement"></param>
        /// <returns></returns>
        public Completion ExecuteForStatement(ForStatement forStatement)
        {
            try
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

                    try
                    {
                        var stmt = ExecuteStatement(forStatement.Body);
                        if (stmt.Value.HasValue)
                        {
                            v = stmt.Value.Value;
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
                    catch (JavaScriptException ex)
                    {
                        ex.InitLocation(forStatement.Body);
                        throw;
                    }
                    catch (StatementsCountOverflowException ex)
                    {
                        ex.InitLocation(forStatement.Body);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new JavaScriptException(new JsValue(ex.Message), forStatement.Body.Location);
                    }
                }
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(forStatement.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(forStatement.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), forStatement.Location);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.4
        /// </summary>
        /// <param name="forInStatement"></param>
        /// <returns></returns>
        public Completion ExecuteForInStatement(ForInStatement forInStatement)
        {
            try
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
                    var keys = cursor.Properties.Keys.ToArray();
                    foreach (var p in keys)
                    {
                        try
                        {
                            if (processedKeys.Contains(p))
                            {
                                continue;
                            }

                            processedKeys.Add(p);

                            // collection might be modified by inner statement 
                            if (!cursor.Properties.ContainsKey(p))
                            {
                                continue;
                            }

                            var value = cursor.Properties[p];
                            if (!value.Enumerable.HasValue || !value.Enumerable.Value.AsBoolean())
                            {
                                continue;
                            }

                            _engine.PutValue(varRef, p);

                            var stmt = ExecuteStatement(forInStatement.Body);
                            if (stmt.Value.HasValue)
                            {
                                v = stmt.Value.Value;
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
                        catch (JavaScriptException ex)
                        {
                            ex.InitLocation(forInStatement.Body);
                            throw;
                        }
                        catch (StatementsCountOverflowException ex)
                        {
                            ex.InitLocation(forInStatement.Body);
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw new JavaScriptException(new JsValue(ex.Message), forInStatement.Body.Location);
                        }
                    }

                    cursor = cursor.Prototype;
                }

                return new Completion(Completion.Normal, v, null);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(forInStatement);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(forInStatement);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), forInStatement.Location);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.7
        /// </summary>
        /// <param name="continueStatement"></param>
        /// <returns></returns>
        public Completion ExecuteContinueStatement(ContinueStatement continueStatement)
        {
            try
            {
                return new Completion(Completion.Continue, null, continueStatement.Label != null ? continueStatement.Label.Name : null);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(continueStatement);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(continueStatement);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), continueStatement.Location);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.8
        /// </summary>
        /// <param name="breakStatement"></param>
        /// <returns></returns>
        public Completion ExecuteBreakStatement(BreakStatement breakStatement)
        {
            try
            {
                return new Completion(Completion.Break, null, breakStatement.Label != null ? breakStatement.Label.Name : null);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(breakStatement);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(breakStatement);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), breakStatement.Location);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.9
        /// </summary>
        /// <param name="statement"></param>
        /// <returns></returns>
        public Completion ExecuteReturnStatement(ReturnStatement statement)
        {
            try
            {
                if (statement.Argument == null)
                {
                    return new Completion(Completion.Return, Undefined.Instance, null);
                }

                var exprRef = _engine.EvaluateExpression(statement.Argument);
                return new Completion(Completion.Return, _engine.GetValue(exprRef), null);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(statement);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(statement);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), statement.Location);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.10
        /// </summary>
        /// <param name="withStatement"></param>
        /// <returns></returns>
        public Completion ExecuteWithStatement(WithStatement withStatement)
        {
            try
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
                }
                finally
                {
                    _engine.ExecutionContext.LexicalEnvironment = oldEnv;
                }

                return c;
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(withStatement);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(withStatement);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), withStatement.Location);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.11
        /// </summary>
        /// <param name="switchStatement"></param>
        /// <returns></returns>
        public Completion ExecuteSwitchStatement(SwitchStatement switchStatement)
        {
            try
            {
                var exprRef = _engine.EvaluateExpression(switchStatement.Discriminant);
                var r = ExecuteSwitchBlock(switchStatement.Cases, _engine.GetValue(exprRef));
                if (r.Type == Completion.Break && r.Identifier == switchStatement.LabelSet)
                {
                    return new Completion(Completion.Normal, r.Value, null,switchStatement.Location);
                }
                return r;
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(switchStatement);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(switchStatement);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), switchStatement.Location);
            }
        }

        public Completion ExecuteSwitchBlock(IEnumerable<SwitchCase> switchBlock, JsValue input)
        {
            JsValue v = Undefined.Instance;
            SwitchCase defaultCase = null;
            bool hit = false;
            foreach (var clause in switchBlock)
            {
                try
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

                        v = r.Value.HasValue ? r.Value.Value : Undefined.Instance;
                    }
                }
                catch (JavaScriptException ex)
                {
                    ex.InitLocation(clause.Location);
                    throw;
                }
                catch (StatementsCountOverflowException ex)
                {
                    ex.InitLocation(clause.Location);
                    throw;
                }
                catch (Exception ex)
                {
                    throw new JavaScriptException(new JsValue(ex.Message), clause.Location);
                }

            }

            // do we need to execute the default case ?
            if (hit == false && defaultCase != null)
            {
                try
                {
                    var r = ExecuteStatementList(defaultCase.Consequent);
                    if (r.Type != Completion.Normal)
                    {
                        return r;
                    }

                    v = r.Value.HasValue ? r.Value.Value : Undefined.Instance;
                }
                catch (JavaScriptException ex)
                {
                    ex.InitLocation(defaultCase.Location);
                    throw;
                }
                catch (StatementsCountOverflowException ex)
                {
                    ex.InitLocation(defaultCase.Location);
                    throw;
                }
                catch (Exception ex)
                {
                    throw new JavaScriptException(new JsValue(ex.Message), defaultCase.Location);
                }
            }

            return new Completion(Completion.Normal, v, null);
        }

        public Completion ExecuteStatementList(IEnumerable<Statement> statementList)
        {
            var c = new Completion(Completion.Normal, null, null);
            Completion sl = c;

            try
            {
                foreach (var statement in statementList)
                {
                    try
                    {
                        c = ExecuteStatement(statement);
                        if (c.Type != Completion.Normal)
                        {
                            return new Completion(c.Type, c.Value.HasValue ? c.Value : sl.Value, c.Identifier, statement.Location);
                        }

                        sl = c;
                    }
                    catch (JavaScriptException ex)
                    {
                        ex.InitLocation(statement.Location);
                        throw;
                    }
                    catch (StatementsCountOverflowException ex)
                    {
                        ex.InitLocation(statement.Location);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new JavaScriptException(new JsValue(ex.Message), statement.Location);
                    }
                }
            }
            catch(JavaScriptException v)
            {
                return new Completion(Completion.Throw, v.Error, null, v.Location);
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
            try
            {
                var exprRef = _engine.EvaluateExpression(throwStatement.Argument);
                return new Completion(Completion.Throw, _engine.GetValue(exprRef), null);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(throwStatement.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(throwStatement.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), throwStatement.Location);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.14
        /// </summary>
        /// <param name="tryStatement"></param>
        /// <returns></returns>
        public Completion ExecuteTryStatement(TryStatement tryStatement)
        {
            try
            {
                var b = ExecuteStatement(tryStatement.Block);
                if (b.Type == Completion.Throw)
                {
                    // execute catch
                    if (tryStatement.Handlers.Any())
                    {
                        foreach (var catchClause in tryStatement.Handlers)
                        {
                            try
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
                            catch (JavaScriptException ex)
                            {
                                ex.InitLocation(catchClause.Location);
                                throw;
                            }
                            catch (StatementsCountOverflowException ex)
                            {
                                ex.InitLocation(catchClause.Location);
                                throw;
                            }
                            catch (Exception ex)
                            {
                                throw new JavaScriptException(new JsValue(ex.Message), catchClause.Location);
                            }
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
            catch (JavaScriptException ex)
            {
                ex.InitLocation(tryStatement.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(tryStatement.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), tryStatement.Location);
            }
        }

        public Completion ExecuteProgram(Program program)
        {
            try
            {
                return ExecuteStatementList(program.Body);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(program.Location);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(program.Location);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), program.Location);
            }
        }

        public Completion ExecuteVariableDeclaration(VariableDeclaration statement)
        {
            foreach (var declaration in statement.Declarations)
            {
                try
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
                catch (JavaScriptException ex)
                {
                    ex.InitLocation(declaration.Location);
                    throw;
                }
                catch (StatementsCountOverflowException ex)
                {
                    ex.InitLocation(declaration.Location);
                    throw;
                }
                catch (Exception ex)
                {
                    throw new JavaScriptException(new JsValue(ex.Message), declaration.Location);
                }
            }

            return new Completion(Completion.Normal, Undefined.Instance, null);
        }

        public Completion ExecuteBlockStatement(BlockStatement blockStatement)
        {
            try
            {
                return ExecuteStatementList(blockStatement.Body);
            }
            catch (JavaScriptException ex)
            {
                ex.InitLocation(blockStatement);
                throw;
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.InitLocation(blockStatement);
                throw;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(new JsValue(ex.Message), blockStatement.Location);
            }
        }

        public Completion ExecuteDebuggerStatement(DebuggerStatement debuggerStatement)
        {
            //This is very dangeres for release builds (especially if hosted inside IIS)... so just do it inside debug builds (Debugger.IsAttached should do the trick... but who knows)
#if DEBUG
            if (_engine.Options.IsDebuggerStatementAllowed())
            {
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debugger.Launch();
                }

                System.Diagnostics.Debugger.Break();
            }

            return new Completion(Completion.Normal, null, null);
#endif
        }
    }
}
