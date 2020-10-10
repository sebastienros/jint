using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-forbodyevaluation
    /// </summary>
    internal sealed class JintForStatement : JintStatement<ForStatement>
    {
        private JintVariableDeclaration _initStatement;
        private JintExpression _initExpression;

        private JintExpression _test;
        private JintExpression _increment;

        private JintStatement _body;
        private List<string> _boundNames;

        private bool _shouldCreatePerIterationEnvironment;

        public JintForStatement(ForStatement statement) : base(statement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;
            _body = Build(_statement.Body);

            if (_statement.Init != null)
            {
                if (_statement.Init.Type == Nodes.VariableDeclaration)
                {
                    var d = (VariableDeclaration) _statement.Init;
                    if (d.Kind != VariableDeclarationKind.Var)
                    {
                        _boundNames = new List<string>();
                        d.GetBoundNames(_boundNames);
                    }
                    _initStatement = new JintVariableDeclaration(d);
                    _shouldCreatePerIterationEnvironment = d.Kind == VariableDeclarationKind.Let;
                }
                else
                {
                    _initExpression = JintExpression.Build(engine, (Expression) _statement.Init);
                }
            }

            if (_statement.Test != null)
            {
                _test = JintExpression.Build(engine, _statement.Test);
            }

            if (_statement.Update != null)
            {
                _increment = JintExpression.Build(engine, _statement.Update);
            }
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            EnvironmentRecord oldEnv = null;
            EnvironmentRecord loopEnv = null;
            var engine = context.Engine;
            if (_boundNames != null)
            {
                oldEnv = engine.ExecutionContext.LexicalEnvironment;
                loopEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                var loopEnvRec = loopEnv;
                var kind = _initStatement._statement.Kind;
                for (var i = 0; i < _boundNames.Count; i++)
                {
                    var name = _boundNames[i];
                    if (kind == VariableDeclarationKind.Const)
                    {
                        loopEnvRec.CreateImmutableBinding(name, true);
                    }
                    else
                    {
                        loopEnvRec.CreateMutableBinding(name, false);
                    }
                }

                engine.UpdateLexicalEnvironment(loopEnv);
            }

            try
            {
                if (_initExpression != null)
                {
                    _initExpression?.GetValue(context);
                }
                else
                {
                    _initStatement?.Execute(context);
                }

                return ForBodyEvaluation(context);
            }
            finally
            {
                if (oldEnv is not null)
                {
                    engine.UpdateLexicalEnvironment(oldEnv);
                }
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-forbodyevaluation
        /// </summary>
        private Completion ForBodyEvaluation(EvaluationContext context)
        {
            var v = Undefined.Instance;

            if (_shouldCreatePerIterationEnvironment)
            {
                CreatePerIterationEnvironment(context);
            }

            while (true)
            {
                if (_test != null)
                {
                    if (!TypeConverter.ToBoolean(_test.GetValue(context).Value))
                    {
                        return NormalCompletion(v);
                    }
                }

                var result = _body.Execute(context);
                if (!ReferenceEquals(result.Value, null))
                {
                    v = result.Value;
                }

                if (result.Type == CompletionType.Break && (result.Target == null || result.Target == _statement?.LabelSet?.Name))
                {
                    return NormalCompletion(result.Value);
                }

                if (result.Type != CompletionType.Continue || (result.Target != null && result.Target != _statement?.LabelSet?.Name))
                {
                    if (result.Type != CompletionType.Normal)
                    {
                        return result;
                    }
                }

                if (_shouldCreatePerIterationEnvironment)
                {
                    CreatePerIterationEnvironment(context);
                }

                _increment?.GetValue(context);
            }
        }

        private void CreatePerIterationEnvironment(EvaluationContext context)
        {
            if (_boundNames == null || _boundNames.Count == 0)
            {
                return;
            }

            var engine = context.Engine;
            var lastIterationEnv = engine.ExecutionContext.LexicalEnvironment;
            var lastIterationEnvRec = lastIterationEnv;
            var outer = lastIterationEnv._outerEnv;
            var thisIterationEnv = JintEnvironment.NewDeclarativeEnvironment(engine, outer);

            for (var j = 0; j < _boundNames.Count; j++)
            {
                var bn = _boundNames[j];
                var lastValue = lastIterationEnvRec.GetBindingValue(bn, true);
                thisIterationEnv.CreateMutableBindingAndInitialize(bn, false, lastValue);
            }

            engine.UpdateLexicalEnvironment(thisIterationEnv);
        }
    }
}