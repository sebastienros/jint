using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jint.Native.Object
{
    public class NTSObjectInstance : ObjectInstance
    {
        private JObject root;

        public NTSObjectInstance(Engine engine, JObject token)
            : base(engine)
        {
            this.root = token;
        }

        public override Runtime.Descriptors.PropertyDescriptor GetOwnProperty(string propertyName)
        {

            var descriptor = base.GetOwnProperty(propertyName);
            if (descriptor == Runtime.Descriptors.PropertyDescriptor.Undefined)
            {
                var prop = root.Properties().FirstOrDefault(p => p.Name == propertyName);
                if (prop != null)
                    Properties[propertyName] = descriptor = new Runtime.Descriptors.NTSPropertyDescriptor(Engine, this, prop);
            }
            return descriptor;
        }

        public JsValue Convert(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Array:
                    return Engine.Array.Construct(token.Select(Convert).ToArray());
                case JTokenType.Boolean:
                    return new JsValue(token.Value<bool>());
                case JTokenType.Bytes:
                    throw new NotSupportedException();
                case JTokenType.Comment:
                    throw new NotSupportedException();
                case JTokenType.Constructor:
                    throw new NotSupportedException();
                case JTokenType.Date:
                    return Engine.Date.Construct(token.Value<DateTime>());
                case JTokenType.Float:
                    return new JsValue(token.Value<float>());
                case JTokenType.Guid:
                    throw new NotSupportedException();
                case JTokenType.Integer:
                    return new JsValue(token.Value<int>());
                case JTokenType.None:
                    throw new NotSupportedException();
                case JTokenType.Null:
                    return JsValue.Null;
                case JTokenType.Object:
                    return new NTSObjectInstance(Engine, (JObject)token);
                case JTokenType.Property:
                    throw new NotSupportedException();
                case JTokenType.Raw:
                    throw new NotSupportedException();
                case JTokenType.String:
                    return new JsValue(token.Value<string>());
                case JTokenType.TimeSpan:
                    throw new NotSupportedException();
                case JTokenType.Undefined:
                    return JsValue.Undefined;
                case JTokenType.Uri:
                    throw new NotSupportedException();
                default:
                    throw new NotSupportedException();
            }
        }

        internal JToken ConvertBack(JsValue value)
        {
            switch (value.Type)
            {
                case Jint.Runtime.Types.None:
                    throw new NotSupportedException();
                case Jint.Runtime.Types.Undefined:
                    return JValue.CreateUndefined();
                case Jint.Runtime.Types.Null:
                    return JValue.CreateNull();
                case Jint.Runtime.Types.Boolean:
                    return new JValue(value.AsBoolean());
                case Jint.Runtime.Types.String:
                    return JValue.CreateString(value.AsString());
                case Jint.Runtime.Types.Number:
                    return new JValue(value.AsNumber());
                case Jint.Runtime.Types.Object:
                    var ntsObjectInstance = value.AsObject() as NTSObjectInstance;
                    if (ntsObjectInstance != null)
                        return ntsObjectInstance.root;
                    return new JObject(value.AsObject().Properties.Where(kvp => !kvp.Value.Enumerable.HasValue || kvp.Value.Enumerable.Value.AsBoolean()).Select(kvp => new JProperty(kvp.Key, ConvertBack(kvp.Value.Value ?? JsValue.Undefined))));
                default:
                    throw new NotSupportedException();
            }
        }

        internal JToken ConvertBack(JTokenType type, JsValue value)
        {
            switch (type)
            {
                case JTokenType.Array:
                    if (value.IsArray())
                    {
                        var array = value.AsArray();
                        return new JArray(array.Properties.Where(k => ArrayInstance.IsArrayIndex(new JsValue(k.Key))).Select(kvp => ConvertBack(kvp.Value.Value ?? JsValue.Null)));
                    }
                    break;
                case JTokenType.Boolean:
                    if (value.IsBoolean())
                        return new JValue(value.AsBoolean());
                    break;
                case JTokenType.Date:
                    if (value.IsDate())
                        return new JValue(value.AsDate());
                    break;
                case JTokenType.Float:
                    if (value.IsNumber())
                        return new JValue((float)value.AsNumber());
                    break;
                case JTokenType.Integer:
                    if (value.IsNumber())
                        return new JValue((int)value.AsNumber());
                    break;
                case JTokenType.String:
                    if (value.IsString())
                        return JValue.CreateString(value.AsString());
                    break;
            }
            return ConvertBack(value);
        }
    }
}
