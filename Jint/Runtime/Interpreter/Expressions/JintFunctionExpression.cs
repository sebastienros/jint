using Esprima.Ast;
using Jint.Native.Function;
using Jint.Native.Generator;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintFunctionExpression : JintExpression
    {
        private readonly JintFunctionDefinition _function;

        public JintFunctionExpression(Engine engine, IFunction function)
            : base(engine, ArrowParameterPlaceHolder.Empty)
        {
            _function = new JintFunctionDefinition(engine, function);
        }

        protected override object EvaluateInternal()
        {
            var funcEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
            var strict = _function.Strict || _engine._isStrict;
            var functionThisMode = strict
                ? FunctionThisMode.Strict
                : FunctionThisMode.Global;

            var name = _function.Name ?? "";
            FunctionInstance closure = new ScriptFunctionInstance(
                _engine,
                _function,
                funcEnv,
                functionThisMode);

            if (_function.Function.Generator)
            {
                // https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation
                closure = new GeneratorFunctionInstance(_engine, closure);
            }
            else
            {
                funcEnv.CreateMutableBindingAndInitialize(name, canBeDeleted: false, closure);
                // https://tc39.es/ecma262/#sec-function-definitions-runtime-semantics-evaluation
                closure = new ScriptFunctionInstance(
                    _engine,
                    _function,
                    funcEnv,
                    functionThisMode);

                closure.MakeConstructor();
            }

            funcEnv.CreateImmutableBindingAndInitialize(name, strict: false, closure);
            return closure;
        }
    }
}