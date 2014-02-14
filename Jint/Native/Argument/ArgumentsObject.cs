using System;
using System.Collections.Generic;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;

namespace Jint.Native.Argument
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.6
    /// </summary>
    public class ArgumentsInstance : ObjectInstance
    {
        public ArgumentsInstance(Engine engine) : base(engine)
        {
            // todo: complete implementation
        }

        public bool Strict { get; set; }

        public static ArgumentsInstance CreateArgumentsObject(Engine engine, FunctionInstance func, string[] names, JsValue[] args, EnvironmentRecord env, bool strict)
        {
            var len = args.Length;
            var obj = new ArgumentsInstance(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.Extensible = true;
            obj.FastAddProperty("length", len, true, false, true);
            obj.Strict = strict;
            var map = engine.Object.Construct(Arguments.Empty);
            var mappedNamed = new List<string>();
            var indx = 0;
            while (indx <= len - 1)
            {
                var indxStr = TypeConverter.ToString(indx);
                var val = args[indx];
                obj.FastAddProperty(indxStr, val, true, true, true);
                if (indx < names.Length)
                {
                    var name = names[indx];
                    if (!strict && !mappedNamed.Contains(name))
                    {
                        mappedNamed.Add(name);
                        Func<JsValue, JsValue> g = n => env.GetBindingValue(name, false);
                        var p = new Action<JsValue, JsValue>((n, o) => env.SetMutableBinding(name, o, true));

                        map.DefineOwnProperty(indxStr, new ClrAccessDescriptor(engine, g, p) { Configurable = true }, false);
                    }
                }
                indx++;
            }

            // step 12
            if (mappedNamed.Count > 0)
            {
                obj.ParameterMap = map;
            }

            // step 13
            if (!strict)
            {
                obj.FastAddProperty("callee",func, true, false, true);
            }
            // step 14
            else
            {
                var thrower = engine.Function.ThrowTypeError;
                obj.DefineOwnProperty("caller", new PropertyDescriptor(get: thrower, set: thrower, enumerable:false, configurable:false), false);
                obj.DefineOwnProperty("callee", new PropertyDescriptor(get: thrower, set: thrower, enumerable: false, configurable: false), false);
            }

            return obj;
        }

        public ObjectInstance ParameterMap { get; set; }

        public override string Class
        {
            get
            {
                return "Arguments";
            }
        }


        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            if (!Strict && ParameterMap != null)
            {
                var desc = base.GetOwnProperty(propertyName);
                if (desc == PropertyDescriptor.Undefined)
                {
                    return desc;
                }

                var isMapped = ParameterMap.GetOwnProperty(propertyName);
                if (isMapped != PropertyDescriptor.Undefined)
                {
                    desc.Value = ParameterMap.Get(propertyName);
                }

                return desc;
            }

            return base.GetOwnProperty(propertyName);
        }

        public override bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            if (!Strict && ParameterMap != null)
            {
                var map = ParameterMap;
                var isMapped = map.GetOwnProperty(propertyName);
                var allowed = base.DefineOwnProperty(propertyName, desc, false);
                if (!allowed)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }
                }
                if (isMapped != PropertyDescriptor.Undefined)
                {
                    if (desc.IsAccessorDescriptor())
                    {
                        map.Delete(propertyName, false);
                    }
                    else
                    {
                        if (desc.Value.HasValue && desc.Value.Value != Undefined.Instance)
                        {
                            map.Put(propertyName, desc.Value.Value, throwOnError);
                        }

                        if (desc.Writable.HasValue && desc.Writable.Value == false)
                        {
                            map.Delete(propertyName, false);
                        }
                    }
                }

                return true;
            }

            return base.DefineOwnProperty(propertyName, desc, throwOnError);
        }

        public override bool Delete(string propertyName, bool throwOnError)
        {
            if (!Strict && ParameterMap != null)
            {
                var map = ParameterMap;
                var isMapped = map.GetOwnProperty(propertyName);
                var result = base.Delete(propertyName, throwOnError);
                if (result && isMapped != PropertyDescriptor.Undefined)
                {
                    map.Delete(propertyName, false);
                }

                return result;
            }

            return base.Delete(propertyName, throwOnError);
        }
    }
}
 