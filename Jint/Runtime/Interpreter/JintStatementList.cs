using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter
{
    internal sealed class JintStatementList
    {
        private sealed class Pair
        {
            internal JintStatement Statement = null!;
            internal Completion? Value;
        }

        private readonly Statement? _statement;
        private readonly NodeList<Statement> _statements;

        private Pair[]? _jintStatements;
        private bool _initialized;
        private uint _index;
        private readonly bool _generator;

        public JintStatementList(IFunction function)
            : this((BlockStatement) function.Body)
        {
            _generator = function.Generator;
        }

        public JintStatementList(BlockStatement blockStatement)
            : this(blockStatement, blockStatement.Body)
        {
        }

        public JintStatementList(Program program)
            : this(null, program.Body)
        {
        }

        public JintStatementList(Statement? statement, in NodeList<Statement> statements)
        {
            _statement = statement;
            _statements = statements;
        }

        private void Initialize(EvaluationContext context)
        {
            var jintStatements = new Pair[_statements.Count];
            for (var i = 0; i < jintStatements.Length; i++)
            {
                var esprimaStatement = _statements[i];
                var statement = JintStatement.Build(esprimaStatement);
                // When in debug mode, don't do FastResolve: Stepping requires each statement to be actually executed.
                var value = context.DebugMode ? null : JintStatement.FastResolve(esprimaStatement);
                jintStatements[i] = new Pair
                {
                    Statement = statement,
                    Value = value
                };
            }

            _jintStatements = jintStatements;
        }

        public Completion Execute(EvaluationContext context)
        {
            if (!_initialized)
            {
                Initialize(context);
                _initialized = true;
            }

            var engine = context.Engine;
            if (_statement != null)
            {
                context.LastSyntaxNode = _statement;
                engine.RunBeforeExecuteStatementChecks(_statement);
            }

            JintStatement? s = null;
            var c = new Completion(CompletionType.Normal, null!, null, context.LastSyntaxNode?.Location ?? default);
            Completion sl = c;

            // The value of a StatementList is the value of the last value-producing item in the StatementList
            JsValue? lastValue = null;
            try
            {
                foreach (var pair in _jintStatements!)
                {
                    s = pair.Statement;
                    c = pair.Value ?? s.Execute(context);

                    if (c.Type != CompletionType.Normal)
                    {
                        return new Completion(
                            c.Type,
                            c.Value ?? sl.Value,
                            c.Target,
                            c.Location);
                    }
                    sl = c;
                    lastValue = c.Value ?? lastValue;
                }
            }
            catch (JavaScriptException v)
            {
                var location = v.Location == default ? s!.Location : v.Location;
                var completion = new Completion(CompletionType.Throw, v.Error, null, location);
                return completion;
            }
            catch (TypeErrorException e)
            {
                var error = engine.Realm.Intrinsics.TypeError.Construct(new JsValue[]
                {
                    e.Message
                });
                return new Completion(CompletionType.Throw, error, null, s!.Location);
            }
            catch (RangeErrorException e)
            {
                var error = engine.Realm.Intrinsics.RangeError.Construct(new JsValue[]
                {
                    e.Message
                });
                c = new Completion(CompletionType.Throw, error, null, s!.Location);
            }
            return new Completion(c.Type, lastValue ?? JsValue.Undefined, c.Target, c.Location);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-blockdeclarationinstantiation
        /// </summary>
        internal static void BlockDeclarationInstantiation(
            Engine engine,
            EnvironmentRecord env,
            List<Declaration> declarations)
        {
            var privateEnv = env._engine.ExecutionContext.PrivateEnvironment;
            var boundNames = new List<string>();
            for (var i = 0; i < declarations.Count; i++)
            {
                var d = declarations[i];
                boundNames.Clear();
                d.GetBoundNames(boundNames);
                for (var j = 0; j < boundNames.Count; j++)
                {
                    var dn = boundNames[j];
                    if (d is VariableDeclaration { Kind: VariableDeclarationKind.Const })
                    {
                        env.CreateImmutableBinding(dn, strict: true);
                    }
                    else
                    {
                        env.CreateMutableBinding(dn, canBeDeleted: false);
                    }
                }

                if (d is FunctionDeclaration functionDeclaration)
                {
                    var definition = new JintFunctionDefinition(engine, functionDeclaration);
                    var fn = definition.Name!;
                    var fo = env._engine.Realm.Intrinsics.Function.InstantiateFunctionObject(definition, env, privateEnv);
                    env.InitializeBinding(fn, fo);
                }
            }
        }
    }
}
