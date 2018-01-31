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
        private IPropertyDescriptor _prototype;
        private const string PropertyNameLength = "length";
        private IPropertyDescriptor _length;

        private readonly Engine _engine;

        protected FunctionInstance(Engine engine, string[] parameters, LexicalEnvironment scope, bool strict) : base(engine)
        {
            _engine = engine;
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
            if (vObj == null)
            {
                return false;
            }

            var po = Get("prototype");
            if (!po.IsObject())
            {
                throw new JavaScriptException(_engine.TypeError, string.Format("Function has non-object prototype '{0}' in instanceof check", TypeConverter.ToString(po)));
            }

            var o = po.AsObject();

            if (o == null)
            {
                throw new JavaScriptException(_engine.TypeError);
            }

            while (true)
            {
                vObj = vObj.Prototype;

                if (vObj == null)
                {
                    return false;
                }

                if (vObj == o)
                {
                    return true;
                }
            }
        }

        public override string Class => "Function";

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.5.4
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public override JsValue Get(string propertyName)
        {
            var v = base.Get(propertyName);

            var f = v.As<FunctionInstance>();
            if (propertyName == "caller" && f != null && f.Strict)
            {
                throw new JavaScriptException(_engine.TypeError);
            }

            return v;
        }

        public override IEnumerable<KeyValuePair<string, IPropertyDescriptor>> GetOwnProperties()
        {
            if (_prototype != null)
            {
                yield return new KeyValuePair<string, IPropertyDescriptor>(PropertyNamePrototype, _prototype);
            }
            if (_length != null)
            {
                yield return new KeyValuePair<string, IPropertyDescriptor>(PropertyNameLength, _length);
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override IPropertyDescriptor GetOwnProperty(string propertyName)
        {
            if (propertyName == PropertyNamePrototype)
            {
                return _prototype ?? PropertyDescriptor.Undefined;
            }
            if (propertyName == PropertyNameLength)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }

            return base.GetOwnProperty(propertyName);
        }

        protected internal override void SetOwnProperty(string propertyName, IPropertyDescriptor desc)
        {
            if (propertyName == PropertyNamePrototype)
            {
                _prototype = desc;
            }
            else if (propertyName == PropertyNameLength)
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
            if (propertyName == PropertyNamePrototype)
            {
                return _prototype != null;
            }
            if (propertyName == PropertyNameLength)
            {
                return _length != null;
            }

            return base.HasOwnProperty(propertyName);
        }

        public override void RemoveOwnProperty(string propertyName)
        {
            if (propertyName == PropertyNamePrototype)
            {
                _prototype = null;
            }
            if (propertyName == PropertyNameLength)
            {
                _prototype = null;
            }

            base.RemoveOwnProperty(propertyName);
        }
    }
}