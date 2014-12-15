using System.Linq;
using Jint.Native.Object;
using Jint.Parser;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class ScriptFunctionInstance : FunctionInstance, IConstructor
    {
        private readonly IFunctionDeclaration _functionDeclaration;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="functionDeclaration"></param>
        /// <param name="scope"></param>
        /// <param name="strict"></param>
        public ScriptFunctionInstance(Engine engine, IFunctionDeclaration functionDeclaration, LexicalEnvironment scope, bool strict)
            : base(engine, functionDeclaration.Parameters.Select(x => x.Name).ToArray(), scope, strict)
        {
            _functionDeclaration = functionDeclaration;

            Engine = engine;
            Extensible = true;
            Prototype = engine.Function.PrototypeObject;

            DefineOwnProperty("length", new PropertyDescriptor(new JsValue(FormalParameters.Length), false, false, false ), false);

            var proto = engine.Object.Construct(Arguments.Empty);
            proto.DefineOwnProperty("constructor", new PropertyDescriptor(this, true, false, true), false);
            DefineOwnProperty("prototype", new PropertyDescriptor(proto, true, false, false ), false);
            if (_functionDeclaration.Id != null)
            {
                DefineOwnProperty("name", new PropertyDescriptor(_functionDeclaration.Id.Name, null, null, null), false);
            }

            if (strict)
            {
                var thrower = engine.Function.ThrowTypeError;
                DefineOwnProperty("caller", new PropertyDescriptor(thrower, thrower, false, false), false);
                DefineOwnProperty("arguments", new PropertyDescriptor(thrower, thrower, false, false), false);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.1
        /// </summary>
        /// <param name="thisArg"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public override JsValue Call(JsValue thisArg, JsValue[] arguments)
        {
            using (new StrictModeScope(Strict, true))
            {
                // setup new execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.3
                JsValue thisBinding;
                if (StrictModeScope.IsStrictModeCode)
                {
                    thisBinding = thisArg;
                }
                else if (thisArg == Undefined.Instance || thisArg == Null.Instance)
                {
                    thisBinding = Engine.Global;
                }
                else if (!thisArg.IsObject())
                {
                    thisBinding = TypeConverter.ToObject(Engine, thisArg);
                }
                else
                {
                    thisBinding = thisArg;
                }

                var localEnv = LexicalEnvironment.NewDeclarativeEnvironment(Engine, Scope);

                Engine.EnterExecutionContext(localEnv, localEnv, thisBinding);

                try
                {
                    Engine.DeclarationBindingInstantiation(
                        DeclarationBindingType.FunctionCode,
                        _functionDeclaration.FunctionDeclarations, 
                        _functionDeclaration.VariableDeclarations, 
                        this,
                        arguments);

                    var result = Engine.ExecuteStatement(_functionDeclaration.Body);

                    if (result.Type == Completion.Throw)
                    {
                        JavaScriptException ex = new JavaScriptException(result.GetValueOrDefault());
                        ex.Location = result.Location;
                        throw ex;
                    }

                    if (result.Type == Completion.Return)
                    {
                        return result.GetValueOrDefault();
                    }
                }
                finally
                {
                    Engine.LeaveExecutionContext();
                }

                return Undefined.Instance;
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.2
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            var proto = Get("prototype").TryCast<ObjectInstance>();
            var obj = new ObjectInstance(Engine);
            obj.Extensible = true;
            obj.Prototype = proto ?? Engine.Object.PrototypeObject;

            var result = Call(obj, arguments).TryCast<ObjectInstance>();
            if (result != null)
            {
                return result;
            }
            
            return obj;
        }

        public ObjectInstance PrototypeObject { get; private set; }
    }
}
