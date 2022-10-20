using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintFunctionExpression : JintExpression
    {
        private readonly JintFunctionDefinition _function;

        public JintFunctionExpression(FunctionExpression function) : base(function)
        {
            _function = new JintFunctionDefinition(function);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            return GetValue(context);
        }

        public override JsValue GetValue(EvaluationContext context)
        {
            ScriptFunctionInstance closure;
            if (!_function.Function.Generator)
            {
                closure = _function.Function.Async
                    ? InstantiateAsyncFunctionExpression(context, _function.Name)
                    : InstantiateOrdinaryFunctionExpression(context, _function.Name);
            }
            else
            {
                closure = InstantiateGeneratorFunctionExpression(context, _function.Name);
            }

            return closure;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateordinaryfunctionexpression
        /// </summary>
        private ScriptFunctionInstance InstantiateOrdinaryFunctionExpression(EvaluationContext context, string? name = "")
        {
            var engine = context.Engine;
            var runningExecutionContext = engine.ExecutionContext;
            var scope = runningExecutionContext.LexicalEnvironment;

            DeclarativeEnvironmentRecord? funcEnv = null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                funcEnv = JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment);
                funcEnv.CreateImmutableBinding(name!, strict: false);
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

            funcEnv?.InitializeBinding(name!, closure);

            return closure;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateasyncfunctionexpression
        /// </summary>
        private ScriptFunctionInstance InstantiateAsyncFunctionExpression(EvaluationContext context, string? name = "")
        {
            var engine = context.Engine;
            var runningExecutionContext = engine.ExecutionContext;
            var scope = runningExecutionContext.LexicalEnvironment;

            DeclarativeEnvironmentRecord? funcEnv = null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                funcEnv = JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment);
                funcEnv.CreateImmutableBinding(name!, strict: false);
            }

            var privateScope = runningExecutionContext.PrivateEnvironment;

            var thisMode = _function.Strict || engine._isStrict
                ? FunctionThisMode.Strict
                : FunctionThisMode.Global;

            var intrinsics = engine.Realm.Intrinsics;
            var closure = intrinsics.Function.OrdinaryFunctionCreate(
                intrinsics.AsyncFunction.PrototypeObject,
                _function,
                thisMode,
                funcEnv ?? scope,
                privateScope
            );

            closure.SetFunctionName(name ?? "");

            funcEnv?.InitializeBinding(name!, closure);

            return closure;
        }


        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiategeneratorfunctionexpression
        /// </summary>
        private ScriptFunctionInstance InstantiateGeneratorFunctionExpression(EvaluationContext context, string? name)
        {
            // TODO generators
            return InstantiateOrdinaryFunctionExpression(context, name);
        }
    }
}
