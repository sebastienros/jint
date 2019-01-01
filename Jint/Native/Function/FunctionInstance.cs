using System.Collections.Generic;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public abstract class FunctionInstance : ObjectInstance, ICallable
    {
        private const string PropertyNamePrototype = "prototype";
        private const int PropertyNamePrototypeLength = 9;
        protected PropertyDescriptor _prototype;

        private const string PropertyNameLength = "length";
        private const int PropertyNameLengthLength = 6;
        protected PropertyDescriptor _length;

        private const string PropertyNameName = "name";
        private const int PropertyNameNameLength = 4;
        private PropertyDescriptor _name;

        protected readonly LexicalEnvironment _scope;
        protected internal readonly string[] _formalParameters;
        private readonly bool _strict;

        protected FunctionInstance(
            Engine engine,
            string name,
            string[] parameters,
            LexicalEnvironment scope,
            bool strict)
            : this(engine, name, parameters, scope, strict, objectClass: "Function")
        {
        }

        protected FunctionInstance(
            Engine engine,
            string name,
            string[] parameters,
            LexicalEnvironment scope,
            bool strict,
            string objectClass)
            : base(engine, objectClass)
        {
            _name = new PropertyDescriptor(name, PropertyFlag.Configurable);
            _formalParameters = parameters;
            _scope = scope;
            _strict = strict;
        }

        /// <summary>
        /// Executed when a function object is used as a function
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public abstract JsValue Call(JsValue thisObject, JsValue[] arguments);

        public LexicalEnvironment Scope => _scope;

        public string[] FormalParameters => _formalParameters;

        public bool Strict => _strict;

        public virtual bool HasInstance(JsValue v)
        {
            var vObj = v.TryCast<ObjectInstance>();
            if (ReferenceEquals(vObj, null))
            {
                return false;
            }

            var po = Get("prototype");
            if (!po.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_engine, $"Function has non-object prototype '{TypeConverter.ToString(po)}' in instanceof check");
            }

            var o = po.AsObject();

            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            while (true)
            {
                vObj = vObj.Prototype;

                if (ReferenceEquals(vObj, null))
                {
                    return false;
                }

                if (vObj == o)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.5.4
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public override JsValue Get(string propertyName)
        {
            var v = base.Get(propertyName);

            if (propertyName.Length == 6
                && propertyName == "caller"
                && ((v.As<FunctionInstance>()?._strict).GetValueOrDefault()))
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return v;
        }

        public override IEnumerable<KeyValuePair<string, PropertyDescriptor>> GetOwnProperties()
        {
            if (_prototype != null)
            {
                yield return new KeyValuePair<string, PropertyDescriptor>(PropertyNamePrototype, _prototype);
            }
            if (_length != null)
            {
                yield return new KeyValuePair<string, PropertyDescriptor>(PropertyNameLength, _length);
            }
            if (_name != null)
            {
                yield return new KeyValuePair<string, PropertyDescriptor>(PropertyNameName, _name);
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            if (propertyName.Length == PropertyNamePrototypeLength && propertyName == PropertyNamePrototype)
            {
                return _prototype ?? PropertyDescriptor.Undefined;
            }
            if (propertyName.Length == PropertyNameLengthLength && propertyName == PropertyNameLength)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }
            if (propertyName.Length == PropertyNameNameLength && propertyName == PropertyNameName)
            {
                return _name ?? PropertyDescriptor.Undefined;
            }

            return base.GetOwnProperty(propertyName);
        }

        protected internal override void SetOwnProperty(string propertyName, PropertyDescriptor desc)
        {
            if (propertyName.Length == PropertyNamePrototypeLength && propertyName == PropertyNamePrototype)
            {
                _prototype = desc;
            }
            else if (propertyName.Length == PropertyNameLengthLength && propertyName == PropertyNameLength)
            {
                _length = desc;
            }
            else if (propertyName.Length == PropertyNameNameLength && propertyName == PropertyNameName)
            {
                _name = desc;
            }
            else
            {
                base.SetOwnProperty(propertyName, desc);
            }
        }

        public override bool HasOwnProperty(string propertyName)
        {
            if (propertyName.Length == PropertyNamePrototypeLength && propertyName == PropertyNamePrototype)
            {
                return _prototype != null;
            }
            if (propertyName.Length == PropertyNameLengthLength && propertyName == PropertyNameLength)
            {
                return _length != null;
            }
            if (propertyName.Length == PropertyNameNameLength && propertyName == PropertyNameName)
            {
                return _name != null;
            }

            return base.HasOwnProperty(propertyName);
        }

        public override void RemoveOwnProperty(string propertyName)
        {
            if (propertyName.Length == PropertyNamePrototypeLength && propertyName == PropertyNamePrototype)
            {
                _prototype = null;
            }
            if (propertyName.Length == PropertyNameLengthLength && propertyName == PropertyNameLength)
            {
                _length = null;
            }
            if (propertyName.Length == PropertyNameNameLength && propertyName == PropertyNameName)
            {
                _name = null;
            }

            base.RemoveOwnProperty(propertyName);
        }
    }
}