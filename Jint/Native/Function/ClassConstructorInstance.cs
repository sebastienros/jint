#nullable enable

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    internal sealed class ClassConstructorInstance : FunctionInstance, IConstructor
    {
        private readonly JintFunctionDefinition _constructorFunction;

        public ClassConstructorInstance(
            Engine engine,
            JintFunctionDefinition constructorFunction,
            LexicalEnvironment scope,
            string? name = null) : base(engine, name != null ? new JsString(name) : null, FunctionThisMode.Strict)
        {
            _constructorFunction = constructorFunction;
            _environment = scope;
        }

        public override JsValue Call(JsValue thisValue, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_engine, $"Class constructor {_constructorFunction.Name} cannot be invoked without 'new'");
            return Undefined;
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var obj = OrdinaryCreateFromConstructor(TypeConverter.ToObject(_engine, newTarget), _prototype);

            var localEnv = LexicalEnvironment.NewFunctionEnvironment(_engine, this, Undefined);
            _engine.EnterExecutionContext(localEnv, localEnv);
            var envRec = (FunctionEnvironmentRecord) localEnv._record;
            envRec.BindThisValue(obj);

            using (new StrictModeScope(true, true))
            {
                try
                {
                    _constructorFunction.Execute();
                }
                finally
                {
                    _engine.LeaveExecutionContext();
                }
            }
            return obj;
        }

        public override string ToString()
        {
            // TODO no way to extract SourceText from Esprima at the moment, just returning native code
            var nameValue = _nameDescriptor != null ? UnwrapJsValue(_nameDescriptor) : JsString.Empty;
            var name = "";
            if (!nameValue.IsUndefined())
            {
                name = TypeConverter.ToString(nameValue);
            }

            return "function " + name + "() {{[native code]}}";
        }
    }
}