using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native.Array;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Json
{
    public class JsonSerializer
    {
        private readonly Engine _engine;

        public JsonSerializer(Engine engine)
        {
            _engine = engine;
        }

        Stack<object> _stack;
        string _indent, _gap;
        List<string> _propertyList;
        object _replacerFunction = Undefined.Instance;

        public object Serialize(object value, object replacer, object space)
        {
            _stack = new Stack<object>();

            if (TypeConverter.GetType(replacer) == TypeCode.Object)
            {
                if (replacer is ICallable)
                {
                    _replacerFunction = replacer;
                }
                else
                {
                    var replacerObj = (ObjectInstance) replacer;
                    if (replacerObj.Class == "Array")
                    {
                        _propertyList = new List<string>();
                    }

                    foreach (var property in replacerObj.Properties.Values)
                    {
                        object v = _engine.GetValue(property);
                        string item = null;
                        var type = TypeConverter.GetType(v);
                        if (type == TypeCode.String)
                        {
                            item = (string)v;
                        }
                        else if (type == TypeCode.Double)
                        {
                            item = TypeConverter.ToString(v);
                        }
                        else if(type == TypeCode.Object)
                        {
                            var propertyObj = (ObjectInstance) v;
                            if (propertyObj.Class == "String" || propertyObj.Class == "Number")
                            {
                                item = TypeConverter.ToString(propertyObj);
                            }
                        }

                        if (item != null && !_propertyList.Contains(item))
                        {
                            _propertyList.Add(item);
                        }

                    }
                }
            }

            var spaceObj = space as ObjectInstance;
            if (spaceObj != null)
            {
                if (spaceObj.Class == "Number")
                {
                    space = TypeConverter.ToNumber(spaceObj);
                }
                else if (spaceObj.Class == "String")
                {
                    space = TypeConverter.ToString(spaceObj);
                }
            }

            // defining the gap
            if (TypeConverter.GetType(space) == TypeCode.Double)
            {
                _gap = new System.String(' ', (int) System.Math.Min(10, (double) space));
            } 
            else if (TypeConverter.GetType(space) == TypeCode.String)
            {
                var stringSpace = (string) space;
                _gap = stringSpace.Length <= 10 ? stringSpace : stringSpace.Substring(0, 10);
            }
            else
            {
                _gap = "";
            }

            var wrapper = _engine.Object.Construct(Arguments.Empty);
            wrapper.DefineOwnProperty("",
                                      new DataDescriptor(value)
                                          {
                                              Configurable = true,
                                              Enumerable = true,
                                              Writable = true
                                          }, false);

            return Str("", wrapper);
        }

        private string Str(string key, ObjectInstance holder)
        {
            var value = holder.Get(key);
            var valueObj = value as ObjectInstance;
            if (valueObj != null)
            {
                var toJson = valueObj.Get("toJSON") as ICallable;
                if (toJson != null)
                {
                    value = toJson.Call(value, Arguments.From(key));
                }
            }
            
            if (_replacerFunction != Undefined.Instance)
            {
                var replacerFunctionCallable = (ICallable)_replacerFunction;
                value = replacerFunctionCallable.Call(holder, Arguments.From(key));
            }

            valueObj = value as ObjectInstance;
            if (valueObj != null)
            {
                switch (valueObj.Class)
                {
                    case "Number":
                        value = TypeConverter.ToNumber(value);
                        break;
                    case "String":
                        value = TypeConverter.ToString(value);
                        break;
                    case "Boolean":
                        value = ((IPrimitiveType) value).PrimitiveValue;
                        break;
                }
            }

            if (value == Null.Instance)
            {
                return "null";
            }

            if (true.Equals(value))
            {
                return "true";
            }

            if (false.Equals(value))
            {
                return "false";
            }

            if (TypeConverter.GetType(value) == TypeCode.String)
            {
                return Quote((string) value);
            }

            if (TypeConverter.GetType(value) == TypeCode.Double)
            {
                if (GlobalObject.IsFinite(this, Arguments.From(value)))
                {
                    return TypeConverter.ToString(value);
                }
                
                return "null";
            }

            valueObj = value as ObjectInstance;
            var valueCallable = valueObj as ICallable;
            if (valueObj != null && valueCallable == null)
            {
                if (valueObj.Class == "Array")
                {
                    return SerializeArray((ArrayInstance)value);
                }
                
                return SerializeObject(valueObj);
            }

            return "null";
        }

        private string Quote(string value)
        {
            var product = "\"";

            foreach (char c in value)
            {
                switch (c)
                {
                    case '\"':
                        product += "\\\"";
                        break;
                    case '\\':
                        product += "\\\\";
                        break;
                    case '\b':
                        product += "\\b";
                        break;
                    case '\f':
                        product += "\\f";
                        break;
                    case '\n':
                        product += "\\n";
                        break;
                    case '\r':
                        product += "\\r";
                        break;
                    case '\t':
                        product += "\\t";
                        break;
                    default:
                        if (c < 0x20)
                        {
                            product += "\\u";
                            product += ((int) c).ToString("x4");
                        }
                        else
                            product += c;
                        break;
                }
            }

            product += "\"";
            return product;
        }

        private string SerializeArray(ArrayInstance value)
        {
            EnsureNonCyclicity(value);
            _stack.Push(value);
            var stepback = _indent;
            _indent = _indent + _gap;
            var partial = new List<string>();
            var len = TypeConverter.ToUint32(value.Get("length"));
            for (int i = 0; i < len; i++)
            {
                var strP = Str(TypeConverter.ToString(i), value);
                partial.Add(strP);
            }
            if (partial.Count == 0)
            {
                return "[]";
            }

            string final;
            if (_gap == "")
            {
                var separator = ",";
                var properties = System.String.Join(separator, partial.ToArray());
                final = "[" + properties + "]";
            }
            else
            {
                var separator = ",\n" + _indent;
                var properties = System.String.Join(separator, partial.ToArray());
                final = "[\n" + _indent + properties + "\n" + stepback + "]";
            }
            
            _stack.Pop();
            _indent = stepback;
            return final;
        }

        private void EnsureNonCyclicity(object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (_stack.Contains(value))
            {
                throw new JavaScriptException(_engine.TypeError, "Cyclic reference detected.");
            }
        }

        private string SerializeObject(ObjectInstance value)
        {
            string final;

            EnsureNonCyclicity(value);
            _stack.Push(value);
            var stepback = _indent;
            _indent += _gap;
            var k = _propertyList;
            if (k == null)
            {
                k = value.Properties.Where(x => x.Value.Enumerable).Select(x => x.Key).ToList();
            }
            var partial = new List<string>();
            foreach (var p in k)
            {
                var strP = Str(p, value);
                if (strP != "null")
                {
                    var member = Quote(p) + ":";
                    if (_gap != "")
                    {
                        member += " ";
                    }
                    member += strP;
                    partial.Add(member);
                }
            }
            if (partial.Count == 0)
            {
                final = "{}";
            }
            else
            {
                if (_gap == "")
                {
                    var separator = ",";
                    var properties = System.String.Join(separator, partial.ToArray());
                    final = "{" + properties + "}";
                }
                else
                {
                    var separator = ",\n" + _indent;
                    var properties = System.String.Join(separator, partial.ToArray());
                    final = "{\n" + _indent + properties + "\n" + stepback + "}";
                }                
            }
            _stack.Pop();
            _indent = stepback;
            return final;
        }
    }
}
