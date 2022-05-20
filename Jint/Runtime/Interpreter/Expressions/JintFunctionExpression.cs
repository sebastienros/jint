using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintFunctionExpression : JintExpression
    {
        private readonly JintFunctionDefinition _function;

        public JintFunctionExpression(Engine engine, FunctionExpression function)
            : base(ArrowParameterPlaceHolder.Empty)
        {
            _function = new JintFunctionDefinition(engine, function);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            return GetValue(context);
        }

        public override Completion GetValue(EvaluationContext context)
        {
            var closure = !_function.Function.Generator
                ? InstantiateOrdinaryFunctionExpression(context, _function.Name)
                : InstantiateGeneratorFunctionExpression(context, _function.Name);

            return Completion.Normal(closure, _expression.Location);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateordinaryfunctionexpression
        /// </summary>
        private ScriptFunctionInstance InstantiateOrdinaryFunctionExpression(EvaluationContext context, string name = "")
        {
            var engine = context.Engine;
            var runningExecutionContext = engine.ExecutionContext;
            var scope = runningExecutionContext.LexicalEnvironment;

            DeclarativeEnvironmentRecord funcEnv = null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                funcEnv = JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment);
                funcEnv.CreateImmutableBinding(name, strict: false);
            }

            var privateScope = runningExecutionContext.PrivateEnvironment;

            var thisMode = _function.Strict || engine._isStrict
                ? FunctionThisMode.Strict
                : FunctionThisMode.Global;

            var intrinsics = engine.Realm.Intrinsics;
            var closure = intrinsics.Function.OrdinaryFunctionCreate(
                intrinsics.Function.PrototypeObject,
                _function,
                thisMode,
                funcEnv ?? scope,
                privateScope
            );

            if (name is not null)
            {
                closure.SetFunctionName(name);
            }
            closure.MakeConstructor();

            funcEnv?.InitializeBinding(name, closure);

            return closure;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiategeneratorfunctionexpression
        /// </summary>
        private ScriptFunctionInstance InstantiateGeneratorFunctionExpression(EvaluationContext context, string name = "")
        {
            // TODO generators
            return InstantiateOrdinaryFunctionExpression(context, name);
        }
    }
}
