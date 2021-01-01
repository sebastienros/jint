#nullable enable

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    internal sealed class ClassConstructorInstance : FunctionInstance, IConstructor
    {
        internal enum ConstructorKind
        {
            Base,
            Derived
        }

        public ConstructorKind _constructorKind;

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
            if (thisValue.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine, $"Class constructor {_functionDefinition.Name} cannot be invoked without 'new'");
            }
            
            return Construct(arguments, this);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var kind = _constructorKind;

            JsValue thisArgument = Null;
            
            if (kind == ConstructorKind.Base)
            {
                thisArgument = OrdinaryCreateFromConstructor(TypeConverter.ToObject(_engine, newTarget), _prototype);
            }
            
            // Let calleeContext be PrepareForOrdinaryCall(F, newTarget).
            var env = LexicalEnvironment.NewFunctionEnvironment(_engine, this, Undefined);
            var calleeContext = _engine.EnterExecutionContext(env, env);

            if (kind == ConstructorKind.Base)
            {
                OrdinaryCallBindThis(calleeContext, thisArgument);
            }

            var constructorEnv = (FunctionEnvironmentRecord) env._record;
            
            using (new StrictModeScope(true, force: true))
            {
                try
                {
                    var argumentsInstance = _engine.FunctionDeclarationInstantiation(
                        functionInstance: this,
                        arguments,
                        env);
                    
                    var result = _functionDefinition.Execute();

                    var value = result.GetValueOrDefault().Clone();
                    argumentsInstance?.FunctionWasCalled();

                    if (result.Type == CompletionType.Return)
                    {
                        if (value is ObjectInstance oi)
                        {
                            return oi;
                        }

                        if (kind == ConstructorKind.Base)
                        {
                            return (ObjectInstance) thisArgument!;
                        }

                        if (value.IsUndefined())
                        {
                            ExceptionHelper.ThrowTypeError(_engine);
                        }
                    }
                    else if (result.Type == CompletionType.Throw)
                    {
                        ExceptionHelper.ThrowJavaScriptException(_engine, value, result);
                    }
                }
                finally
                {
                    _engine.LeaveExecutionContext();
                }
            }

            return (ObjectInstance) constructorEnv.GetThisBinding();
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