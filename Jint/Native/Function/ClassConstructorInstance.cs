#nullable enable

using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    internal sealed class ClassConstructorInstance : ScriptFunctionInstance, IConstructor
    {
        public ClassConstructorInstance(
            Engine engine,
            JintFunctionDefinition constructorFunction,
            LexicalEnvironment scope,
            string? name = null) : base(engine, constructorFunction,  scope, FunctionThisMode.Strict)
        {
            if (name is not null)
            {
                _nameDescriptor = new PropertyDescriptor(name, PropertyFlag.Configurable);
            }
            _environment = scope;
        }

        internal override bool IsConstructor => true;

        public override JsValue Call(JsValue thisValue, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_engine, $"Class constructor {_functionDefinition.Name} cannot be invoked without 'new'");
            return Undefined;
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