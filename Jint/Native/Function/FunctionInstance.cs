using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        protected PropertyDescriptor _length;

        private JsValue _name;
        private PropertyDescriptor _nameDescriptor;

        protected internal LexicalEnvironment _environment;
        internal readonly JintFunctionDefinition _functionDefinition;
        internal readonly FunctionThisMode _thisMode;

        internal FunctionInstance(
            Engine engine,
            JintFunctionDefinition function,
            LexicalEnvironment scope,
            FunctionThisMode thisMode)
            : this(engine, !string.IsNullOrWhiteSpace(function.Name) ? new JsString(function.Name) : null,  thisMode)
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
            _name = name;
            _thisMode = thisMode;
        }

        /// <summary>
        /// Executed when a function object is used as a function
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public abstract JsValue Call(JsValue thisObject, JsValue[] arguments);

        public bool Strict => _thisMode == FunctionThisMode.Strict;
        
        public virtual bool HasInstance(JsValue v)
        {
            if (!(v is ObjectInstance o))
            {
                return false;
            }

            var p = Get(CommonProperties.Prototype, this);
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

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.5.4
        /// </summary>
        public override JsValue Get(JsValue property, JsValue receiver)
        {
            var v = base.Get(property, receiver);

            if (property == CommonProperties.Caller
                && v.As<FunctionInstance>()?._thisMode == FunctionThisMode.Strict)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return v;
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
            if (!(_name is null))
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
            if (!(_name is null))
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
                return !(_name is null)
                    ? _nameDescriptor ??= new PropertyDescriptor(_name, PropertyFlag.Configurable)
                    :  PropertyDescriptor.Undefined;
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
                _name = desc._value;
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
                return !(_name is null);
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
                _name = null;
                _nameDescriptor = null;
            }

            base.RemoveOwnProperty(property);
        }

        internal void SetFunctionName(JsValue name, bool throwIfExists = false)
        {
            if (_name is null)
            {
                JsString value;
                if (name is JsSymbol symbol)
                {
                    value = new JsString(symbol._value.IsUndefined()
                        ? ""
                        : "[" + symbol._value + "]");
                }
                else
                {
                    value = name as JsString ?? new JsString(name.ToString());
                }
                _name = value;
            }
            else if (throwIfExists)
            {
                ExceptionHelper.ThrowError(_engine, "cannot set name");
            }
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
    }
}