using System.Text.RegularExpressions;
using Jint.Native.Array;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
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
            FastAddProperty("toString", new ClrFunctionInstance(Engine, ToRegExpString), true, false, true);
            FastAddProperty("exec", new ClrFunctionInstance(Engine, Exec, 1), true, false, true);
            FastAddProperty("test", new ClrFunctionInstance(Engine, Test, 1), true, false, true);

            FastAddProperty("global", false, false, false, false);
            FastAddProperty("ignoreCase", false, false, false, false);
            FastAddProperty("multiline", false, false, false, false);
            FastAddProperty("source", "(?:)", false, false, false);
            FastAddProperty("lastIndex", 0, true, false, false);
        }

        private JsValue ToRegExpString(JsValue thisObj, JsValue[] arguments)
        {
            var regExp = thisObj.TryCast<RegExpInstance>();

            return "/" + regExp.Source + "/"
                + (regExp.Flags.Contains("g") ? "g" : "")
                + (regExp.Flags.Contains("i") ? "i" : "")
                + (regExp.Flags.Contains("m") ? "m" : "")
                ;
        }

        private JsValue Test(JsValue thisObj, JsValue[] arguments)
        {
            var r = TypeConverter.ToObject(Engine, thisObj);
            if (r.Class != "RegExp")
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var match = Exec(r, arguments);
            return match != Null;
        }

        internal JsValue Exec(JsValue thisObj, JsValue[] arguments)
        {
            var R = TypeConverter.ToObject(Engine, thisObj) as RegExpInstance;
            if (R == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var s = TypeConverter.ToString(arguments.At(0));
            var length = s.Length;
            var lastIndex = TypeConverter.ToNumber(R.Get("lastIndex"));
            var i = TypeConverter.ToInteger(lastIndex);
            var global = R.Global;

            if (!global)
            {
                i = 0;
            }

            if (R.Source == "(?:)")  // Reg Exp is really ""
            {
                // "aaa".match() => [ '', index: 0, input: 'aaa' ]
                var aa = InitReturnValueArray((ArrayInstance) Engine.Array.Construct(Arguments.Empty), s, 1, 0);
                aa.DefineOwnProperty("0", new PropertyDescriptor("", true, true, true), true);
                return aa;
            }

            Match r = null;
            if (i < 0 || i > length)
            {
                R.Put("lastIndex", (double) 0, true);
                return Null;
            }

            r = R.Match(s, i);

            if (!r.Success)
            {
                R.Put("lastIndex", (double) 0, true);
                return Null;
            }

            var e = r.Index + r.Length;

            if (global)
            {
                R.Put("lastIndex", (double) e, true);
            }
            var n = r.Groups.Count;
            var matchIndex = r.Index;

            var a = InitReturnValueArray((ArrayInstance) Engine.Array.Construct(Arguments.Empty), s, n, matchIndex);

            for (uint k = 0; k < n; k++)
            {
                var group = r.Groups[(int) k];
                var value = group.Success ? group.Value : Undefined;
                a.SetIndexValue(k, value, throwOnError: true);
            }

            return a;
        }

        private static ArrayInstance InitReturnValueArray(ArrayInstance array, string inputValue, int lengthValue, int indexValue)
        {
            array.SetOwnProperty("index", new ConfigurableEnumerableWritablePropertyDescriptor(indexValue));
            array.SetOwnProperty("input", new ConfigurableEnumerableWritablePropertyDescriptor(inputValue));
            array.SetOwnProperty("length", new WritablePropertyDescriptor(lengthValue));
            return array;
        }
    }
}
