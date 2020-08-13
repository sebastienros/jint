using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    public abstract class FunctionInstance : ObjectInstance, ICallable
    {
        internal enum FunctionThisMode
        {
            Lexical,
            Strict,
            Global
        }

        protected internal PropertyDescriptor _prototypeDescriptor;

        protected internal PropertyDescriptor _length;
        internal PropertyDescriptor _nameDescriptor;

        protected internal LexicalEnvironment _environment;
        internal readonly JintFunctionDefinition _functionDefinition;
        internal readonly FunctionThisMode _thisMode;

        internal FunctionInstance(
            Engine engine,
            JintFunctionDefinition function,
            LexicalEnvironment scope,
            FunctionThisMode thisMode)
            : this(engine, !string.IsNullOrWhiteSpace(function.Name) ? new JsString(function.Name) : null, thisMode)
        {
            _functionDefinition = function;
            _environment = scope;
        }

        internal FunctionInstance(
            Engine engine,
            JsString name,
            FunctionThisMode thisMode = FunctionThisMode.Global,
            ObjectClass objectClass = ObjectClass.Function)
            : base(engine, objectClass)
        {
            if (!(name is null))
            {
                _nameDescriptor = new PropertyDescriptor(name, PropertyFlag.Configurable);
            }
            _thisMode = thisMode;
        }

        protected FunctionInstance(
            Engine engine,
            JsString name)
            : this(engine, name, FunctionThisMode.Global, ObjectClass.Function)
        {
        }

        /// <summary>
        /// Executed when a function object is used as a function
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public abstract JsValue Call(JsValue thisObject, JsValue[] arguments);
        public abstract Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments);

        public bool Strict => _thisMode == FunctionThisMode.Strict;

        public virtual bool HasInstance(JsValue v)
        {
            if (!(v is ObjectInstance o))
            {
                return false;
            }

            var p = Get(CommonProperties.Prototype);
            if (!(p is ObjectInstance prototype))
            {
                ExceptionHelper.ThrowTypeError(_engine, $"Function has non-object prototype '{TypeConverter.ToString(p)}' in instanceof check");
            }

            while (true)
            {
                o = o.Prototype;

                if (o is null)
                {
                    return false;
                }

                if (SameValue(p, o))
                {
                    return true;
                }
            }
        }

        public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
        {
            if (_prototypeDescriptor != null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Prototype, _prototypeDescriptor);
            }
            if (_length != null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Length, _length);
            }
            if (_nameDescriptor != null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Name, GetOwnProperty(CommonProperties.Name));
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override List<JsValue> GetOwnPropertyKeys(Types types)
        {
            var keys = new List<JsValue>();
            if (_prototypeDescriptor != null)
            {
                keys.Add(CommonProperties.Prototype);
            }
            if (_length != null)
            {
                keys.Add(CommonProperties.Length);
            }
            if (_nameDescriptor != null)
            {
                keys.Add(CommonProperties.Name);
            }

            keys.AddRange(base.GetOwnPropertyKeys(types));

            return keys;
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Prototype)
            {
                return _prototypeDescriptor ?? PropertyDescriptor.Undefined;
            }
            if (property == CommonProperties.Length)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }
            if (property == CommonProperties.Name)
            {
                return _nameDescriptor ?? PropertyDescriptor.Undefined;
            }

            return base.GetOwnProperty(property);
        }

        protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            if (property == CommonProperties.Prototype)
            {
                _prototypeDescriptor = desc;
            }
            else if (property == CommonProperties.Length)
            {
                _length = desc;
            }
            else if (property == CommonProperties.Name)
            {
                _nameDescriptor = desc;
            }
            else
            {
                base.SetOwnProperty(property, desc);
            }
        }

        public override bool HasOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Prototype)
            {
                return _prototypeDescriptor != null;
            }
            if (property == CommonProperties.Length)
            {
                return _length != null;
            }
            if (property == CommonProperties.Name)
            {
                return _nameDescriptor != null;
            }

            return base.HasOwnProperty(property);
        }

        public override void RemoveOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Prototype)
            {
                _prototypeDescriptor = null;
            }
            if (property == CommonProperties.Length)
            {
                _length = null;
            }
            if (property == CommonProperties.Name)
            {
                _nameDescriptor = null;
            }

            base.RemoveOwnProperty(property);
        }

        internal void SetFunctionName(JsValue name, string prefix = null, bool force = false)
        {
            if (!force && _nameDescriptor != null && !UnwrapJsValue(_nameDescriptor).IsUndefined())
            {
                return;
            }

            if (name is JsSymbol symbol)
            {
                name = symbol._value.IsUndefined()
                    ? JsString.Empty
                    : new JsString("[" + symbol._value + "]");
            }
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                name = prefix + " " + name;
            }

            _nameDescriptor = new PropertyDescriptor(name, PropertyFlag.Configurable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ObjectInstance OrdinaryCreateFromConstructor(JsValue constructor, ObjectInstance intrinsicDefaultProto)
        {
            var proto = GetPrototypeFromConstructor(constructor, intrinsicDefaultProto);

            var obj = new ObjectInstance(_engine)
            {
                _prototype = proto
            };
            return obj;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ObjectInstance GetPrototypeFromConstructor(JsValue constructor, ObjectInstance intrinsicDefaultProto)
        {
            var proto = constructor.Get(CommonProperties.Prototype, constructor) as ObjectInstance;
            // If Type(proto) is not Object, then
            //    Let realm be ? GetFunctionRealm(constructor).
            //    Set proto to realm's intrinsic object named intrinsicDefaultProto.
            return proto ?? intrinsicDefaultProto;
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
