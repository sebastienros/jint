using System;
using System.Text.RegularExpressions;
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

            if (pattern != Undefined.Instance && flags == Undefined.Instance && TypeConverter.ToObject(Engine, pattern).AsObject().Class == "Regex")
            {
                return pattern;
            }

            return Construct(arguments);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.7.2.1
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
            if (pattern.AsString() != null && flags == Undefined.Instance && r != null)
            {
                p = r.Pattern;
                f = r.Flags;
            }
            else if (pattern.AsString() != null && flags != Undefined.Instance && r != null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }
            else
            {
                p = pattern != Undefined.Instance ? TypeConverter.ToString(pattern).AsString() : "";
                f = flags != Undefined.Instance ? TypeConverter.ToString(flags).AsString() : "";
            }

            r = new RegExpInstance(Engine);
            r.Prototype = PrototypeObject;
            r.Extensible = true;

            var options = ParseOptions(r, f);

            try
            {
                r.Value = new Regex(p, options);
            }
            catch (Exception e)
            {
                throw new JavaScriptException(Engine.SyntaxError, e.Message);
            }

            string s;
            if (string.IsNullOrEmpty(p))
            {
                s = "(?:)";
            }
            else
            {
                s = p;
             
                if (s.StartsWith("/"))
                {
                    s = "\\" + s;
                }

                if (s.EndsWith("/"))
                {
                    s = s.TrimEnd('/') + "\\/";
                }
            }

            r.FastAddProperty("global", r.Global, false, false, false);
            r.FastAddProperty("ignoreCase", r.IgnoreCase, false, false, false);
            r.FastAddProperty("multiline", r.Multiline, false, false, false);
            r.FastAddProperty("lastIndex", 0, true, false, false);
            r.FastAddProperty("source", s, false, false, false);

            r.Flags = f;
            r.Source = s;

            return r;
        }

        public RegExpInstance Construct(string regExp)
        {
            var r = new RegExpInstance(Engine);
            r.Prototype = PrototypeObject;
            r.Extensible = true;

            var segments = regExp.Split('/');

            var pattern = segments[1];
            var flags = segments[2];

            var options = ParseOptions(r, flags);
            try
            {
                r.Value = new Regex(pattern, options);
            }
            catch (Exception e)
            {
                throw new JavaScriptException(Engine.SyntaxError, e.Message);
            }

            r.FastAddProperty("global", r.Global, false, false, false);
            r.FastAddProperty("ignoreCase", r.IgnoreCase, false, false, false);
            r.FastAddProperty("multiline", r.Multiline, false, false, false);
            r.FastAddProperty("lastIndex", 0, true, false, false);
            r.FastAddProperty("source", pattern, false, false, false);

            r.Flags = flags;
            r.Source = pattern;

            return r;
        }

        private RegexOptions ParseOptions(RegExpInstance r, string flags)
        {
            for (int k = 0; k < flags.Length; k++)
            {
                var c = flags[k];
                if (c == 'g')
                {
                    if (r.Global)
                    {
                        throw new JavaScriptException(Engine.SyntaxError);
                    }

                    r.Global = true;
                }
                else if (c == 'i')
                {
                    if (r.IgnoreCase)
                    {
                        throw new JavaScriptException(Engine.SyntaxError);
                    }

                    r.IgnoreCase = true;
                }
                else if (c == 'm')
                {
                    if (r.Multiline)
                    {
                        throw new JavaScriptException(Engine.SyntaxError);
                    }

                    r.Multiline = true;
                }
                else
                {
                    throw new JavaScriptException(Engine.SyntaxError);
                }
            }

            var options = RegexOptions.ECMAScript;

            if (r.Multiline)
            {
                options = options | RegexOptions.Multiline;
            }

            if (r.IgnoreCase)
            {
                options = options | RegexOptions.IgnoreCase;
            }

            return options;
        }

        public RegExpPrototype PrototypeObject { get; private set; }
    }
}
