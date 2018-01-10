using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    /// <summary>
    ///
    /// </summary>
    public sealed class ScriptFunctionInstance : FunctionInstance, IConstructor
    {
        private readonly IFunction _functionDeclaration;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="functionDeclaration"></param>
        /// <param name="scope"></param>
        /// <param name="strict"></param>
        public ScriptFunctionInstance(Engine engine, IFunction functionDeclaration, LexicalEnvironment scope, bool strict)
            : base(engine, GetParameterNames(functionDeclaration), scope, strict)
        {
            _functionDeclaration = functionDeclaration;

            Engine = engine;
            Extensible = true;
            Prototype = engine.Function.PrototypeObject;

            DefineOwnProperty("length", new AllForbiddenPropertyDescriptor(JsNumber.Create(FormalParameters.Length)), false);

            var proto = engine.Object.Construct(Arguments.Empty);
            proto.SetOwnProperty("constructor", new NonEnumerablePropertyDescriptor(this));
            SetOwnProperty("prototype", new WritablePropertyDescriptor(proto));
            if (_functionDeclaration.Id != null)
            {
                DefineOwnProperty("name", new NullConfigurationPropertyDescriptor(_functionDeclaration.Id.Name), false);
            }

            if (strict)
            {
                var thrower = engine.Function.ThrowTypeError;
                DefineOwnProperty("caller", new PropertyDescriptor(thrower, thrower, false, false), false);
                DefineOwnProperty("arguments", new PropertyDescriptor(thrower, thrower, false, false), false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string[] GetParameterNames(IFunction functionDeclaration)
        {
            var list = functionDeclaration.Params;
            var count = list.Count;

            if (count == 0)
            {
                return System.Array.Empty<string>();
            }

            var names = new string[count];
            for (var i = 0; i < count; ++i)
            {
                names[i] = ((Identifier) list[i]).Name;
            }

            return names;
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
                else if (ReferenceEquals(thisArg, Undefined) || ReferenceEquals(thisArg, Null))
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
                        _functionDeclaration.HoistingScope.FunctionDeclarations,
                        _functionDeclaration.HoistingScope.VariableDeclarations,
                        this,
                        arguments);

                    var result = Engine.ExecuteStatement(_functionDeclaration.Body);

                    if (result.Type == Completion.Throw)
                    {
                        JavaScriptException ex = new JavaScriptException(result.GetValueOrDefault())
                            .SetCallstack(Engine, result.Location);
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

                return Undefined;
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
    }
}