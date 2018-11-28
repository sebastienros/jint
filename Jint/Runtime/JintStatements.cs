using System.Collections.Generic;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime
{
    internal abstract class JintStatement<T> : JintStatement where T : Statement
    {
        protected readonly Engine _engine;
        protected readonly T _statement;

        protected JintStatement(Engine engine, T statement)
        {
            _engine = engine;
            _statement = statement;
        }

        public override Location Location => _statement.Location;
    }
                                                                
    internal abstract class JintStatement
    {
        public abstract Location Location { get; }

        public abstract Completion Execute();

        protected internal static JintStatement Build(Engine engine, Statement statement)
        {
            switch (statement.Type)
            {
                case Nodes.BlockStatement:
                    var statementListItems = ((BlockStatement) statement).Body;
                    return new JintBlockStatement(engine, new JintStatementList(engine, statementListItems));

                case Nodes.ReturnStatement:
                    return new JintReturnStatement(engine, (ReturnStatement) statement);

                case Nodes.VariableDeclaration:
                    return new JintVariableDeclaration(engine, (VariableDeclaration) statement);

                case Nodes.BreakStatement:
                    return new JintBreakStatement(engine, (BreakStatement) statement);

                case Nodes.ContinueStatement:
                    return new JintContinueStatement(engine, (ContinueStatement) statement);

                case Nodes.DoWhileStatement:
                    return new JintDoWhileStatement(engine, (DoWhileStatement) statement);

                case Nodes.EmptyStatement:
                    return new JintEmptyStatement(engine, (EmptyStatement) statement);

                case Nodes.ExpressionStatement:
                    return new JintExpressionStatement(engine, (ExpressionStatement) statement);

                case Nodes.ForStatement:
                    return new JintForStatement(engine, (ForStatement) statement);

                case Nodes.ForInStatement:
                    return new JintForInStatement(engine, (ForInStatement) statement);

                case Nodes.IfStatement:
                    return new JintIfStatement(engine, (IfStatement) statement);

                case Nodes.LabeledStatement:
                    return new JintLabeledStatement(engine, (LabeledStatement) statement);

                case Nodes.SwitchStatement:
                    return new JintSwitchStatement(engine, (SwitchStatement) statement);

                case Nodes.FunctionDeclaration:
                    return new JintFunctionDeclarationStatement(engine, (FunctionDeclaration) statement);

                case Nodes.ThrowStatement:
                    return new JintThrowStatement(engine, (ThrowStatement) statement);

                case Nodes.TryStatement:
                    return new JintTryStatement(engine, (TryStatement) statement);

                case Nodes.WhileStatement:
                    return new JintWhileStatement(engine, (WhileStatement) statement);

                case Nodes.WithStatement:
                    return new JintWithStatement(engine, (WithStatement) statement);

                case Nodes.DebuggerStatement:
                    return new JintDebuggerStatement(engine, (DebuggerStatement) statement);

                case Nodes.Program:
                    return new JintProgram(engine, (Program) statement);

                default:
                    return ExceptionHelper.ThrowArgumentOutOfRangeException<JintStatement>();
            }
        }

        private sealed class JintProgram : JintStatement<Program>
        {
            private readonly JintStatementList _list;

            public JintProgram(Engine engine, Program statement) : base(engine, statement)
            {
                _list = new JintStatementList(_engine, _statement.Body);
            }

            public override Completion Execute()
            {
                return _list.Execute();
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.14
        /// </summary>
        private sealed class JintTryStatement : JintStatement<TryStatement>
        {
            private readonly JintStatement _block;
            private readonly JintStatement _catch;
            private readonly string _catchParamName;
            private readonly JintStatement _finalizer;

            public JintTryStatement(Engine engine, TryStatement statement) : base(engine, statement)
            {
                _block = Build(engine, statement.Block);
                if (_statement.Handler != null)
                {
                    _catch = Build(engine, _statement.Handler.Body);
                    _catchParamName = ((Identifier) _statement.Handler.Param).Name;
                }

                if (statement.Finalizer != null)
                {
                    _finalizer = Build(engine, _statement.Finalizer);
                }
            }

            public override Completion Execute()
            {
                var b = _block.Execute();
                if (b.Type == CompletionType.Throw)
                {
                    // execute catch
                    if (_catch != null)
                    {
                        var c = b.Value;
                        var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                        var catchEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                        catchEnv._record.CreateMutableBinding(_catchParamName, c);

                        _engine.UpdateLexicalEnvironment(catchEnv);
                        b = _catch.Execute();
                        _engine.UpdateLexicalEnvironment(oldEnv);
                    }
                }

                if (_finalizer != null)
                {
                    var f = _finalizer.Execute();
                    if (f.Type == CompletionType.Normal)
                    {
                        return b;
                    }

                    return f;
                }

                return b;
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.13
        /// </summary>
        private sealed class JintThrowStatement : JintStatement<ThrowStatement>
        {
            public JintThrowStatement(Engine engine, ThrowStatement statement) : base(engine, statement)
            {
            }

            public override Completion Execute()
            {
                var jsValue = _engine.GetValue(_engine.EvaluateExpression(_statement.Argument), true);
                return new Completion(CompletionType.Throw, jsValue, null, _statement.Location);
            }
        }

        private sealed class JintFunctionDeclarationStatement : JintStatement<FunctionDeclaration>
        {
            public JintFunctionDeclarationStatement(Engine engine, FunctionDeclaration statement) : base(engine, statement)
            {
            }

            public override Completion Execute()
            {
                return new Completion(CompletionType.Normal, null, null);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.11
        /// </summary>
        private sealed class JintSwitchStatement : JintStatement<SwitchStatement>
        {
            private readonly JintSwitchBlock _switchBlock;

            public JintSwitchStatement(Engine engine, SwitchStatement statement) : base(engine, statement)
            {
                _switchBlock = new JintSwitchBlock(engine, _statement.Cases);
            }

            public override Completion Execute()
            {
                var jsValue = _engine.GetValue(_engine.EvaluateExpression(_statement.Discriminant), true);
                var r = _switchBlock.Execute(jsValue);
                if (r.Type == CompletionType.Break && r.Identifier == _statement.LabelSet?.Name)
                {
                    return new Completion(CompletionType.Normal, r.Value, null);
                }

                return r;
            }
        }

        private sealed class JintSwitchBlock
        {
            private sealed class JintSwitchCase
            {
                internal readonly JintStatementList _consequent;
                internal readonly Expression _test;

                public JintSwitchCase(Engine engine, SwitchCase switchCase)
                {
                    if (switchCase.Consequent != null)
                    {
                        _consequent = new JintStatementList(engine, switchCase.Consequent);
                    }

                    _test = switchCase.Test;
                }
            }

            private readonly Engine _engine;
            private readonly List<SwitchCase> _switchBlock;
            private readonly JintSwitchCase[] _jintSwitchBlock;

            public JintSwitchBlock(Engine engine, List<SwitchCase> switchBlock)
            {
                _engine = engine;
                _switchBlock = switchBlock;
                _jintSwitchBlock = new JintSwitchCase[switchBlock.Count];
            }

            public Completion Execute(JsValue input)
            {
                JsValue v = Undefined.Instance;
                JintSwitchCase defaultCase = null;
                bool hit = false;

                for (var i = 0; i < _jintSwitchBlock.Length; i++)
                {
                    var clause = _jintSwitchBlock[i] ?? (_jintSwitchBlock[i] = new JintSwitchCase(_engine, _switchBlock[i]));
                    if (clause._test == null)
                    {
                        defaultCase = clause;
                    }
                    else
                    {
                        var clauseSelector = _engine.GetValue(_engine.EvaluateExpression(clause._test), true);
                        if (ExpressionInterpreter.StrictlyEqual(clauseSelector, input))
                        {
                            hit = true;
                        }
                    }

                    if (hit && clause._consequent != null)
                    {
                        var r = clause._consequent.Execute();
                        if (r.Type != CompletionType.Normal)
                        {
                            return r;
                        }

                        v = r.Value ?? Undefined.Instance;
                    }
                }

                // do we need to execute the default case ?
                if (hit == false && defaultCase != null)
                {
                    var r = defaultCase._consequent.Execute();
                    if (r.Type != CompletionType.Normal)
                    {
                        return r;
                    }

                    v = r.Value ?? Undefined.Instance;
                }

                return new Completion(CompletionType.Normal, v, null);
            }
        }

        private sealed class JintLabeledStatement : JintStatement<LabeledStatement>
        {
            private readonly JintStatement _body;
            private readonly string _labelName;

            public JintLabeledStatement(Engine engine, LabeledStatement statement) : base(engine, statement)
            {
                _body = Build(engine, statement.Body);
                _labelName = statement.Label.Name;
            }

            public override Completion Execute()
            {
                // TODO: Esprima added Statement.Label, maybe not necessary as this line is finding the
                // containing label and could keep a table per program with all the labels
                // labeledStatement.Body.LabelSet = labeledStatement.Label;
                var result = _body.Execute();
                if (result.Type == CompletionType.Break && result.Identifier == _labelName)
                {
                    var value = result.Value;
                    return new Completion(CompletionType.Normal, value, null);
                }

                return result;
            }
        }

        private sealed class JintDebuggerStatement : JintStatement<DebuggerStatement>
        {
            public JintDebuggerStatement(Engine engine, DebuggerStatement statement) : base(engine, statement)
            {
            }

            public override Completion Execute()
            {
                if (_engine.Options._IsDebuggerStatementAllowed)
                {
                    if (!System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Diagnostics.Debugger.Launch();
                    }

                    System.Diagnostics.Debugger.Break();
                }

                return new Completion(CompletionType.Normal, null, null);
            }
        }

        private sealed class JintIfStatement : JintStatement<IfStatement>
        {
            private readonly JintStatement _statementConsequent;
            private readonly JintStatement _alternate;

            public JintIfStatement(Engine engine, IfStatement statement) : base(engine, statement)
            {
                _statementConsequent = Build(engine, _statement.Consequent);
                _alternate = _statement.Alternate != null ? Build(engine, _statement.Alternate) : null;
            }

            public override Completion Execute()
            {
                Completion result;
                if (TypeConverter.ToBoolean(_engine.GetValue(_engine.EvaluateExpression(_statement.Test), true)))
                {
                    result = _statementConsequent.Execute();
                }
                else if (_alternate != null)
                {
                    result = _alternate.Execute();
                }
                else
                {
                    return new Completion(CompletionType.Normal, null, null);
                }

                return result;
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.4
        /// </summary>
        private sealed class JintForInStatement : JintStatement<ForInStatement>
        {
            private readonly Identifier _identifier;
            private readonly JintStatement _body;

            public JintForInStatement(Engine engine, ForInStatement statement) : base(engine, statement)
            {
                _identifier = _statement.Left.Type == Nodes.VariableDeclaration
                    ? (Identifier) ((VariableDeclaration) _statement.Left).Declarations[0].Id
                    : (Identifier) _statement.Left;

                _body = Build(engine, _statement.Body);
            }

            public override Completion Execute()
            {
                var varRef = _engine.EvaluateExpression(_identifier) as Reference;
                var experValue = _engine.GetValue(_engine.EvaluateExpression(_statement.Right), true);
                if (experValue.IsUndefined() || experValue.IsNull())
                {
                    return new Completion(CompletionType.Normal, null, null);
                }

                var obj = TypeConverter.ToObject(_engine, experValue);
                JsValue v = Null.Instance;

                // keys are constructed using the prototype chain
                var cursor = obj;
                var processedKeys = new HashSet<string>();

                while (!ReferenceEquals(cursor, null))
                {
                    var keys = _engine.Object.GetOwnPropertyNames(Undefined.Instance, Arguments.From(cursor)).AsArray();

                    var length = keys.GetLength();
                    for (var i = 0; i < length; i++)
                    {
                        var p = keys.GetOwnProperty(TypeConverter.ToString(i)).Value.AsStringWithoutTypeCheck();

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

                        var stmt = _body.Execute();
                        if (!ReferenceEquals(stmt.Value, null))
                        {
                            v = stmt.Value;
                        }

                        if (stmt.Type == CompletionType.Break)
                        {
                            return new Completion(CompletionType.Normal, v, null);
                        }

                        if (stmt.Type != CompletionType.Continue)
                        {
                            if (stmt.Type != CompletionType.Normal)
                            {
                                return stmt;
                            }
                        }
                    }

                    cursor = cursor.Prototype;
                }

                return new Completion(CompletionType.Normal, v, null);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.3
        /// </summary>
        private sealed class JintForStatement : JintStatement<ForStatement>
        {
            private readonly JintStatement _body;
            private readonly JintStatement _initStatement;
            private readonly object _initExpression;

            public JintForStatement(Engine engine, ForStatement statement) : base(engine, statement)
            {
                _body = Build(engine, _statement.Body);

                if (_statement.Init != null)
                {
                    if (_statement.Init.Type == Nodes.VariableDeclaration)
                    {
                        _initStatement = Build(engine, (Statement) _statement.Init);
                    }
                    else
                    {
                        // TODO
                        _initExpression = _statement.Init;
                    }
                }
            }

            public override Completion Execute()
            {
                _initStatement?.Execute();
                if (_initExpression != null)
                {
                    // TODO
                    _engine.GetValue(_engine.EvaluateExpression(_statement.Init), true);
                }

                JsValue v = Undefined.Instance;
                while (true)
                {
                    if (_statement.Test != null)
                    {
                        var testExprRef = _engine.EvaluateExpression(_statement.Test);
                        if (!TypeConverter.ToBoolean(_engine.GetValue(testExprRef, true)))
                        {
                            return new Completion(CompletionType.Normal, v, null);
                        }
                    }

                    var stmt = _body.Execute();
                    if (!ReferenceEquals(stmt.Value, null))
                    {
                        v = stmt.Value;
                    }

                    var stmtType = stmt.Type;
                    if (stmtType == CompletionType.Break && (stmt.Identifier == null || stmt.Identifier == _statement?.LabelSet?.Name))
                    {
                        return new Completion(CompletionType.Normal, v, null);
                    }

                    if (stmtType != CompletionType.Continue || ((stmt.Identifier != null) && stmt.Identifier != _statement?.LabelSet?.Name))
                    {
                        if (stmtType != CompletionType.Normal)
                        {
                            return stmt;
                        }
                    }

                    if (_statement.Update != null)
                    {
                        _engine.GetValue(_engine.EvaluateExpression(_statement.Update), true);
                    }
                }
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.7
        /// </summary>
        private sealed class JintContinueStatement : JintStatement<ContinueStatement>
        {
            private readonly string _labelName;

            public JintContinueStatement(Engine engine, ContinueStatement statement) : base(engine, statement)
            {
                _labelName = _statement.Label?.Name;
            }

            public override Completion Execute()
            {
                return new Completion(
                    CompletionType.Continue,
                    null,
                    _labelName);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.10
        /// </summary>
        private sealed class JintWithStatement : JintStatement<WithStatement>
        {
            private readonly JintStatement _body;

            public JintWithStatement(Engine engine, WithStatement statement) : base(engine, statement)
            {
                _body = Build(engine, statement.Body);
            }

            public override Completion Execute()
            {
                var jsValue = _engine.GetValue(_engine.EvaluateExpression(_statement.Object), true);
                var obj = TypeConverter.ToObject(_engine, jsValue);
                var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                var newEnv = LexicalEnvironment.NewObjectEnvironment(_engine, obj, oldEnv, true);
                _engine.UpdateLexicalEnvironment(newEnv);

                Completion c;
                try
                {
                    c = _body.Execute();
                }
                catch (JavaScriptException e)
                {
                    c = new Completion(CompletionType.Throw, e.Error, null, _statement.Location);
                }
                finally
                {
                    _engine.UpdateLexicalEnvironment(oldEnv);
                }

                return c;
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.8
        /// </summary>
        private sealed class JintBreakStatement : JintStatement<BreakStatement>
        {
            private readonly string _label;

            public JintBreakStatement(Engine engine, BreakStatement statement) : base(engine, statement)
            {
                _label = statement.Label?.Name;
            }

            public override Completion Execute()
            {
                return new Completion(
                    CompletionType.Break,
                    null,
                    _label);
            }
        }

        private sealed class JintExpressionStatement : JintStatement<ExpressionStatement>
        {
            public JintExpressionStatement(Engine engine, ExpressionStatement statement) : base(engine, statement)
            {
            }

            public override Completion Execute()
            {
                return new Completion(
                    CompletionType.Normal,
                    _engine.GetValue(_engine.EvaluateExpression(_statement.Expression), true),
                    null);
            }
        }

        private sealed class JintEmptyStatement : JintStatement<EmptyStatement>
        {
            public JintEmptyStatement(Engine engine, EmptyStatement statement) : base(engine, statement)
            {
            }

            public override Completion Execute()
            {
                return new Completion(CompletionType.Normal, null, null);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.2
        /// </summary>
        private sealed class JintWhileStatement : JintStatement<WhileStatement>
        {
            private readonly string _labelSetName;
            private readonly JintStatement _body;

            public JintWhileStatement(Engine engine, WhileStatement statement) : base(engine, statement)
            {
                _labelSetName = _statement?.LabelSet?.Name;
                _body = Build(engine, statement.Body);
            }

            public override Completion Execute()
            {
                var v = Undefined.Instance;
                while (true)
                {
                    var jsValue = _engine.GetValue(_engine.EvaluateExpression(_statement.Test), true);
                    if (!TypeConverter.ToBoolean(jsValue))
                    {
                        return new Completion(CompletionType.Normal, v, null);
                    }

                    var completion = _body.Execute();

                    if (!ReferenceEquals(completion.Value, null))
                    {
                        v = completion.Value;
                    }

                    if (completion.Type != CompletionType.Continue || completion.Identifier != _labelSetName)
                    {
                        if (completion.Type == CompletionType.Break && (completion.Identifier == null || completion.Identifier == _labelSetName))
                        {
                            return new Completion(CompletionType.Normal, v, null);
                        }

                        if (completion.Type != CompletionType.Normal)
                        {
                            return completion;
                        }
                    }
                }
            }
        }

        private sealed class JintVariableDeclaration : JintStatement<VariableDeclaration>
        {
            public JintVariableDeclaration(Engine engine, VariableDeclaration variableDeclaration) : base(engine, variableDeclaration)
            {
            }

            public override Completion Execute()
            {
                var declarationsCount = _statement.Declarations.Count;
                for (var i = 0; i < declarationsCount; i++)
                {
                    var declaration = _statement.Declarations[i];
                    if (declaration.Init != null)
                    {
                        var lhs = (Reference) _engine.EvaluateExpression(declaration.Id);
                        lhs.AssertValid(_engine);

                        var value = _engine.GetValue(_engine.EvaluateExpression(declaration.Init), true);
                        _engine.PutValue(lhs, value);
                        _engine._referencePool.Return(lhs);
                    }
                }

                return new Completion(CompletionType.Normal, Undefined.Instance, null);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.9
        /// </summary>
        private sealed class JintReturnStatement : JintStatement<ReturnStatement>
        {
            public JintReturnStatement(Engine engine, ReturnStatement statement) : base(engine, statement)
            {
            }

            public override Completion Execute()
            {
                var jsValue = _statement.Argument == null
                    ? Undefined.Instance
                    : _engine.GetValue(_engine.EvaluateExpression((_statement).Argument), true);

                return new Completion(CompletionType.Return, jsValue, null);
            }
        }

        private sealed class JintBlockStatement : JintStatement
        {
            private readonly JintStatementList _statementList;

            public JintBlockStatement(Engine engine, JintStatementList statementList)
            {
                _statementList = statementList;
            }


            public override Completion Execute()
            {
                return _statementList.Execute();
            }

            public override Location Location => throw new System.NotImplementedException();
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.1
        /// </summary>
        private sealed class JintDoWhileStatement : JintStatement<DoWhileStatement>
        {
            private readonly JintStatement _body;
            private readonly string _labelSetName;
            private readonly Expression _test;

            public JintDoWhileStatement(Engine engine, DoWhileStatement statement) : base(engine, statement)
            {
                _body = Build(_engine, statement.Body);
                _test = statement.Test;
                _labelSetName = statement.LabelSet?.Name;
            }

            public override Completion Execute()
            {
                JsValue v = Undefined.Instance;
                bool iterating;

                do
                {
                    var completion = _body.Execute();
                    if (!ReferenceEquals(completion.Value, null))
                    {
                        v = completion.Value;
                    }

                    if (completion.Type != CompletionType.Continue || completion.Identifier != _labelSetName)
                    {
                        if (completion.Type == CompletionType.Break && (completion.Identifier == null || completion.Identifier == _labelSetName))
                        {
                            return new Completion(CompletionType.Normal, v, null);
                        }

                        if (completion.Type != CompletionType.Normal)
                        {
                            return completion;
                        }
                    }

                    var exprRef = _engine.EvaluateExpression(_test);
                    iterating = TypeConverter.ToBoolean(_engine.GetValue(exprRef, true));
                } while (iterating);

                return new Completion(CompletionType.Normal, v, null);
            }
        }
    }
}