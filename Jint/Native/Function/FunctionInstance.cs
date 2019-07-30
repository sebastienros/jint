using System.Collections.Generic;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public abstract class FunctionInstance : ObjectInstance, ICallable
    {
        protected internal PropertyDescriptor _prototype;

        protected PropertyDescriptor _length;

        private JsValue _name;
        private PropertyDescriptor _nameDescriptor;

        protected readonly LexicalEnvironment _scope;
        protected internal readonly string[] _formalParameters;
        protected readonly bool _strict;

        protected FunctionInstance(
            Engine engine,
            string name,
            string[] parameters,
            LexicalEnvironment scope,
            bool strict,
            string objectClass = "Function")
            : this(engine, !string.IsNullOrWhiteSpace(name) ? new JsString(name) : null, parameters, scope, strict, objectClass)
        {
        }

        internal FunctionInstance(
            Engine engine,
            JsString name,
            string[] parameters,
            LexicalEnvironment scope,
            bool strict,
            string objectClass = "Function")
            : this(engine, name, strict, objectClass)
        {
            _formalParameters = parameters;
            _scope = scope;
        }

        internal FunctionInstance(
            Engine engine,
            JsString name,
            bool strict,
            string objectClass = "Function")
            : base(engine, objectClass)
        {
            _name = name;
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

            var po = Get(KnownKeys.Prototype);
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
        public override JsValue Get(in Key propertyName)
        {
            var v = base.Get(propertyName);

            if (propertyName == KnownKeys.Caller
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
                yield return new KeyValuePair<string, PropertyDescriptor>(KnownKeys.Prototype, _prototype);
            }
            if (_length != null)
            {
                yield return new KeyValuePair<string, PropertyDescriptor>(KnownKeys.Length, _length);
            }
            if (!(_name is null))
            {
                yield return new KeyValuePair<string, PropertyDescriptor>(KnownKeys.Name, GetOwnProperty(KnownKeys.Name));
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override PropertyDescriptor GetOwnProperty(in Key propertyName)
        {
            if (propertyName == KnownKeys.Prototype)
            {
                return _prototype ?? PropertyDescriptor.Undefined;
            }
            if (propertyName == KnownKeys.Length)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }
            if (propertyName == KnownKeys.Name)
            {
                return !(_name is null)
                    ? _nameDescriptor ?? (_nameDescriptor = new PropertyDescriptor(_name, PropertyFlag.Configurable))
                    :  PropertyDescriptor.Undefined;
            }

            return base.GetOwnProperty(propertyName);
        }

        protected internal override void SetOwnProperty(in Key propertyName, PropertyDescriptor desc)
        {
            if (propertyName == KnownKeys.Prototype)
            {
                _prototype = desc;
            }
            else if (propertyName == KnownKeys.Length)
            {
                _length = desc;
            }
            else if (propertyName == KnownKeys.Name)
            {
                _name = desc._value;
                _nameDescriptor = desc;
            }
            else
            {
                base.SetOwnProperty(propertyName, desc);
            }
        }

        public override bool HasOwnProperty(in Key propertyName)
        {
            if (propertyName == KnownKeys.Prototype)
            {
                return _prototype != null;
            }
            if (propertyName == KnownKeys.Length)
            {
                return _length != null;
            }
            if (propertyName == KnownKeys.Name)
            {
                return !(_name is null);
            }

            return base.HasOwnProperty(propertyName);
        }

        public override void RemoveOwnProperty(in Key propertyName)
        {
            if (propertyName == KnownKeys.Prototype)
            {
                _prototype = null;
            }
            if (propertyName == KnownKeys.Length)
            {
                _length = null;
            }
            if (propertyName == KnownKeys.Name)
            {
                _name = null;
                _nameDescriptor = null;
            }

            base.RemoveOwnProperty(propertyName);
        }

        internal void SetFunctionName(in Key key, bool throwIfExists = false)
        {
            if (_name is null)
            {
                _name = key.Name;
            }
            else if (throwIfExists)
            {
                ExceptionHelper.ThrowError(_engine, "cannot set name");
            }
        }
    }
}