using System;
using System.Text.RegularExpressions;
using Esprima;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.RegExp
{
    public sealed class RegExpConstructor : FunctionInstance, IConstructor
    {
        public RegExpConstructor(Engine engine)
            : base(engine, null, null, false)
        {
        }

        public static RegExpConstructor CreateRegExpConstructor(Engine engine)
        {
            var obj = new RegExpConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the RegExp constructor is the Function prototype object
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = RegExpPrototype.CreatePrototypeObject(engine, obj);

            obj.FastAddProperty("length", 2, false, false, false);

            // The initial value of RegExp.prototype is the RegExp prototype object
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var pattern = arguments.At(0);
            var flags = arguments.At(1);

            if (pattern != Undefined.Instance && flags == Undefined.Instance && TypeConverter.ToObject(Engine, pattern).Class == "Regex")
            {
                return pattern;
            }

            return Construct(arguments);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-7.8.5
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.10.4
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            string p;
            string f;

            var pattern = arguments.At(0);
            var flags = arguments.At(1);

            var r = pattern.TryCast<RegExpInstance>();
            if (flags == Undefined.Instance && r != null)
            {
                return r;
            }
            else if (flags != Undefined.Instance && r != null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }
            else
            {
                if (pattern == Undefined.Instance)
                {
                    p = "";

                }
                else
                {
                    p = TypeConverter.ToString(pattern);
                }

                f = flags != Undefined.Instance ? TypeConverter.ToString(flags) : "";
            }

            r = new RegExpInstance(Engine);
            r.Prototype = PrototypeObject;
            r.Extensible = true;

            var options = new Scanner("").ParseRegexOptions(f);

            try
            {
                r.Value = new Regex(p, options);
            }
            catch (Exception e)
            {
                throw new JavaScriptException(Engine.SyntaxError, e.Message);
            }

            string s;
            s = p;

            if (System.String.IsNullOrEmpty(s))
            {
                s = "(?:)";
            }

            r.Flags = f;
            r.Source = s;

            r.FastAddProperty("global", r.Global, false, false, false);
            r.FastAddProperty("ignoreCase", r.IgnoreCase, false, false, false);
            r.FastAddProperty("multiline", r.Multiline, false, false, false);
            r.FastAddProperty("source", r.Source, false, false, false);
            r.FastAddProperty("lastIndex", 0, true, false, false);

            return r;
        }

        public RegExpInstance Construct(string regExp)
        {
            var r = new RegExpInstance(Engine);
            r.Prototype = PrototypeObject;
            r.Extensible = true;

            var scanner = new Scanner(regExp, new ParserOptions { AdaptRegexp = true });
            var body = (string)scanner.ScanRegExpBody().Value;
            var flags = (string)scanner.ScanRegExpFlags().Value;
            r.Value = scanner.TestRegExp(body, flags);

            r.Flags = flags;
            r.Source = System.String.IsNullOrEmpty(body) ? "(?:)" : body;

            r.FastAddProperty("global", r.Global, false, false, false);
            r.FastAddProperty("ignoreCase", r.IgnoreCase, false, false, false);
            r.FastAddProperty("multiline", r.Multiline, false, false, false);
            r.FastAddProperty("source", r.Source, false, false, false);
            r.FastAddProperty("lastIndex", 0, true, false, false);

            return r;
        }

        public RegExpInstance Construct(Regex regExp)
        {
            var r = new RegExpInstance(Engine);
            r.Prototype = PrototypeObject;
            r.Extensible = true;

            string flags = "";

            if (regExp.Options.HasFlag(RegexOptions.Multiline))
            {
                flags += "m";
            }

            if (regExp.Options.HasFlag(RegexOptions.IgnoreCase))
            {
                flags += "i";
            }

            r.Flags = flags;
            r.Source = regExp.ToString();
            r.Value = regExp;

            r.FastAddProperty("global", r.Global, false, false, false);
            r.FastAddProperty("ignoreCase", r.IgnoreCase, false, false, false);
            r.FastAddProperty("multiline", r.Multiline, false, false, false);
            r.FastAddProperty("source", r.Source, false, false, false);
            r.FastAddProperty("lastIndex", 0, true, false, false);

            return r;
        }

        public RegExpPrototype PrototypeObject { get; private set; }
    }
}
