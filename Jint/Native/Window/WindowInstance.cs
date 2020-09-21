using Jint.Native.Object;
using Jint.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jint.Native.Window
{
    public class WindowInstance : ObjectInstance
    {
        public WindowInstance(Engine engine)
            : base(engine, objectClass: ObjectClass.Window)
        {
        }
        public static WindowInstance CreateWindowObject(Engine engine)
        {
            var window = new WindowInstance(engine)
            {
                _prototype = engine.Object.PrototypeObject
            };
            return window;
        }
        public override JsValue Get(JsValue property, JsValue receiver)
        {
            var result = base.Get(property, receiver);
            if (result.IsUndefined())
            {
                return Engine.Global.Get(property, receiver);
            }
            return result;
        }
        public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
        {
            return base.GetOwnPropertyKeys(types);
        }
        public override bool HasOwnProperty(JsValue property)
        {
            return base.HasOwnProperty(property);
        }
        public override bool HasProperty(JsValue property)
        {
            return base.HasProperty(property);
        }
    }
}
