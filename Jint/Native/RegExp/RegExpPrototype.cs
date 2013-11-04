using System.Text.RegularExpressions;
using Jint.Native.Array;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.RegExp
{
    public sealed class RegExpPrototype : RegExpInstance
    {
        private RegExpPrototype(Engine engine)
            : base(engine)
        {
        }

        public static RegExpPrototype CreatePrototypeObject(Engine engine, RegExpConstructor regExpConstructor)
        {
            var obj = new RegExpPrototype(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.Extensible = true;

            obj.FastAddProperty("constructor", regExpConstructor, true, false, true);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance<RegExpInstance, object>(Engine, ToRegExpString), true, false, true);
            FastAddProperty("exec", new ClrFunctionInstance<object, object>(Engine, Exec, 1), true, false, true);
            FastAddProperty("test", new ClrFunctionInstance<object, bool>(Engine, Test, 1), true, false, true);
        }

        private object ToRegExpString(RegExpInstance thisObj, object[] arguments)
        {
            return "/" + thisObj.Source + "/" 
                + (thisObj.Flags.Contains("g") ? "g" : "")
                + (thisObj.Flags.Contains("i") ? "i" : "")
                + (thisObj.Flags.Contains("m") ? "m" : "")
                ;
        }

        private bool Test(object thisObj, object[] arguments)
        {
            var r = TypeConverter.ToObject(Engine, thisObj);
            if (r.Class != "RegExp")
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var match = Exec(r, arguments);
            return match != Null.Instance;
        }

        internal object Exec(object thisObj, object[] arguments)
        {
            var R = TypeConverter.ToObject(Engine, thisObj) as RegExpInstance;
            if (R == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var s = TypeConverter.ToString(arguments.Length > 0 ? arguments[0] : Undefined.Instance);
            var length = s.Length;
            var lastIndex = TypeConverter.ToNumber(R.Get("lastIndex"));
            var i = TypeConverter.ToInteger(lastIndex);
            var global = R.Global;
            
            if (!global)
            {
                i = 0;
            }

            Match r = null;
            if (i < 0 || i >= length)
            {
                R.Put("lastIndex", (double) 0, true);
                return Null.Instance;
            }

            r = R.Match(s, i);

            if (!r.Success)
            {
                R.Put("lastIndex", (double) 0, true);
                return Null.Instance;
            }

            var e = r.Index + r.Length;
            
            if (global)
            {
                R.Put("lastIndex", (double) e, true);
            }
            var n = r.Groups.Count;
            var a = Engine.Array.Construct(Arguments.Empty);
            var matchIndex = r.Index;
            a.DefineOwnProperty("index", new DataDescriptor(matchIndex) { Writable = true, Enumerable = true, Configurable = true }, true);
            a.DefineOwnProperty("input", new DataDescriptor(s) { Writable = true, Enumerable = true, Configurable = true }, true);
            a.DefineOwnProperty("length", new DataDescriptor(n) { Writable = true, Enumerable = true, Configurable = true }, true);
            for (var k = 0; k < n; k++)
            {
                var group = r.Groups[k];
                var value = group.Success ? group.Value : Undefined.Instance;
                a.DefineOwnProperty(k.ToString(), new DataDescriptor(value) { Writable = true, Enumerable = true, Configurable = true }, true);
            
            }

            return a;
        }
    }
}
