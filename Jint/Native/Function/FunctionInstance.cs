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
        private PropertyDescriptor _prototype;

        private const string PropertyNameLength = "length";
        private const int PropertyNameLengthLength = 6;
        private PropertyDescriptor _length;
        
        protected FunctionInstance(Engine engine, string[] parameters, LexicalEnvironment scope, bool strict)
            : this(engine, parameters, scope, strict, objectClass: "Function")
        {
        }

        protected FunctionInstance(
            Engine engine, 
            string[] parameters, 
            LexicalEnvironment scope, 
            bool strict, 
            in string objectClass)
            : base(engine, objectClass)
        {
            FormalParameters = parameters;
            Scope = scope;
            Strict = strict;
        }

        /// <summary>
        /// Executed when a function object is used as a function
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public abstract JsValue Call(JsValue thisObject, JsValue[] arguments);

        public LexicalEnvironment Scope { get; }

        public string[] FormalParameters { get; }
        public bool Strict { get; }

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
                throw new JavaScriptException(_engine.TypeError, $"Function has non-object prototype '{TypeConverter.ToString(po)}' in instanceof check");
            }

            var o = po.AsObject();

            if (ReferenceEquals(o, null))
            {
                throw new JavaScriptException(_engine.TypeError);
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
                && ((v.As<FunctionInstance>()?.Strict).GetValueOrDefault()))
            {
                throw new JavaScriptException(_engine.TypeError);
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
                _prototype = null;
            }

            base.RemoveOwnProperty(propertyName);
        }
    }
}