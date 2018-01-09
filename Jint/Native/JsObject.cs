using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using Jint.Native.Array;
using Jint.Native.Boolean;
using Jint.Native.Date;
using Jint.Native.Function;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native
{
    public class JsObject : JsValue, IEquatable<JsObject>
    {
        private readonly object _value;

        public JsObject(object value)
        {
            _value = value;
        }

        public JsObject(ObjectInstance value)
        {
            _value = value;
        }

        public override Types Type => Types.Object;

        [Pure]
        public override bool IsArray()
        {
            return _value is ArrayInstance;
        }

        [Pure]
        public override bool IsDate()
        {
            return _value is DateInstance;
        }

        [Pure]
        public override bool IsRegExp()
        {
            return _value is RegExpInstance;
        }

        [Pure]
        public override ObjectInstance AsObject()
        {
            return _value as ObjectInstance;
        }

        [Pure]
        public override TInstance AsInstance<TInstance>()
        {
            return _value as TInstance;
        }

        [Pure]
        public override ArrayInstance AsArray()
        {
            if (!IsArray())
            {
                throw new ArgumentException("The value is not an array");
            }

            return _value as ArrayInstance;
        }

        [Pure]
        public override DateInstance AsDate()
        {
            if (!IsDate())
            {
                throw new ArgumentException("The value is not a date");
            }

            return _value as DateInstance;
        }

        [Pure]
        public override RegExpInstance AsRegExp()
        {
            if (!IsRegExp())
            {
                throw new ArgumentException("The value is not a regex");
            }

            return _value as RegExpInstance;
        }

        public override bool Is<T>()
        {
            return _value is T;
        }

        public override T As<T>()
        {
            return _value as T;
        }

        public override object ToObject()
        {
            if (_value is IObjectWrapper wrapper)
            {
                return wrapper.Target;
            }

            switch ((_value as ObjectInstance).Class)
            {
                case "Array":
                    if (_value is ArrayInstance arrayInstance)
                    {
                        var len = TypeConverter.ToInt32(arrayInstance.Get("length"));
                        var result = new object[len];
                        for (var k = 0; k < len; k++)
                        {
                            var pk = TypeConverter.ToString(k);
                            var kpresent = arrayInstance.HasProperty(pk);
                            if (kpresent)
                            {
                                var kvalue = arrayInstance.Get(pk);
                                result[k] = kvalue.ToObject();
                            }
                            else
                            {
                                result[k] = null;
                            }
                        }

                        return result;
                    }

                    break;

                case "String":
                    if (_value is StringInstance stringInstance)
                    {
                        return stringInstance.PrimitiveValue.AsString();
                    }

                    break;

                case "Date":
                    if (_value is DateInstance dateInstance)
                    {
                        return dateInstance.ToDateTime();
                    }

                    break;

                case "Boolean":
                    if (_value is BooleanInstance booleanInstance)
                    {
                        return booleanInstance.PrimitiveValue.AsBoolean();
                    }

                    break;

                case "Function":
                    if (_value is FunctionInstance function)
                    {
                        return (Func<JsValue, JsValue[], JsValue>) function.Call;
                    }

                    break;

                case "Number":
                    if (_value is NumberInstance numberInstance)
                    {
                        return numberInstance.NumberData.AsNumber();
                    }

                    break;

                case "RegExp":
                    if (_value is RegExpInstance regeExpInstance)
                    {
                        return regeExpInstance.Value;
                    }

                    break;

                case "Arguments":
                case "Object":
#if __IOS__
                                IDictionary<string, object> o = new Dictionary<string, object>();
#else
                    IDictionary<string, object> o = new ExpandoObject();
#endif

                    var objectInstance = (ObjectInstance) _value;
                    foreach (var p in objectInstance.GetOwnProperties())
                    {
                        if (!p.Value.Enumerable.HasValue || p.Value.Enumerable.Value == false)
                        {
                            continue;
                        }

                        o.Add(p.Key, objectInstance.Get(p.Key).ToObject());
                    }

                    return o;
            }


            return _value;
        }


        public override string ToString()
        {
            return _value.ToString();
        }

        public override bool Equals(JsValue obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (!(obj is JsObject s))
            {
                return false;
            }

            return Equals(s);
        }

        public bool Equals(JsObject other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(_value, other._value);
        }
    }
}