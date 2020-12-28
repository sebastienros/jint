using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter
{
    internal class JintStatementList
    {
        private class Pair
        {
            internal JintStatement Statement;
            internal Completion? Value;
        }

        private readonly Engine _engine;
        private readonly Statement _statement;
        private readonly NodeList<Statement> _statements;

        private Pair[] _jintStatements;
        private bool _initialized;

        public JintStatementList(Engine engine, Statement statement, NodeList<Statement> statements)
        {
            _engine = engine;
            _statement = statement;
            _statements = statements;
        }

        private void Initialize()
        {
            var jintStatements = new Pair[_statements.Count];
            for (var i = 0; i < jintStatements.Length; i++)
            {
                var esprimaStatement = _statements[i];
                jintStatements[i] = new Pair
                {
                    Statement = JintStatement.Build(_engine, esprimaStatement),
                    Value = JintStatement.FastResolve(esprimaStatement)
                };
            }
            _jintStatements = jintStatements;
        }

        public Completion Execute()
        {
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            if (_statement != null)
            {
                _engine._lastSyntaxNode = _statement;
                _engine.RunBeforeExecuteStatementChecks(_statement);
            }

            JintStatement s = null;
            var c = new Completion(CompletionType.Normal, null, null, _engine._lastSyntaxNode?.Location ?? default);
            Completion sl = c;
            
            // The value of a StatementList is the value of the last value-producing item in the StatementList
            JsValue lastValue = null;
            try
            {
                foreach (var pair in _jintStatements)
                {
                    s = pair.Statement;
                    c = pair.Value ?? s.Execute();
                    if (c.Type != CompletionType.Normal)
                    {
                        return new Completion(
                            c.Type,
                            c.Value ?? sl.Value,
                            c.Identifier,
                            c.Location);
                    }
                    sl = c;
                    lastValue = c.Value ?? lastValue;
                }
            }
            catch (JavaScriptException v)
            {
                var location = v.Location == default ? s.Location : v.Location;
                var completion = new Completion(CompletionType.Throw, v.Error, null, location);
                return completion;
            }
            catch (TypeErrorException e)
            {
                var error = _engine.TypeError.Construct(new JsValue[]
                {
                    e.Message
                });
                return new Completion(CompletionType.Throw, error, null, s.Location);
            }
            catch (RangeErrorException e)
            {
                var error = _engine.RangeError.Construct(new JsValue[]
                {
                    e.Message
                });
                c = new Completion(CompletionType.Throw, error, null, s.Location);
            }
            return new Completion(c.Type, lastValue ?? JsValue.Undefined, c.Identifier, c.Location);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-blockdeclarationinstantiation
        /// </summary>
        internal static void BlockDeclarationInstantiation(
            LexicalEnvironment env,
            List<VariableDeclaration> lexicalDeclarations)
        {
            var envRec = env._record;
            var boundNames = new List<string>();
            for (var i = 0; i < lexicalDeclarations.Count; i++)
            {
                var variableDeclaration = lexicalDeclarations[i];
                boundNames.Clear();
                variableDeclaration.GetBoundNames(boundNames);
                for (var j = 0; j < boundNames.Count; j++)
                {
                    var dn = boundNames[j];
                    if (variableDeclaration.Kind == VariableDeclarationKind.Const)
                    {
                        envRec.CreateImmutableBinding(dn, strict: true);
                    }
                    else
                    {
                        envRec.CreateMutableBinding(dn, canBeDeleted: false);
                    }
                }

                /*  If d is a FunctionDeclaration, a GeneratorDeclaration, an AsyncFunctionDeclaration, or an AsyncGeneratorDeclaration, then
                 * Let fn be the sole element of the BoundNames of d.
                 * Let fo be the result of performing InstantiateFunctionObject for d with argument env.
                 * Perform envRec.InitializeBinding(fn, fo).
                 */
            }
        }
    }
}
