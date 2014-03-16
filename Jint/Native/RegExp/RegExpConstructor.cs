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
 
            if (regExp[0] != '/')
            {
                throw new JavaScriptException(Engine.SyntaxError, "Regexp should start with slash");
            }
            var lastSlash = regExp.LastIndexOf('/');
            // Unescape escaped forward slashes (\/)
            var pattern = regExp.Substring(1, lastSlash - 1).Replace("\\/", "/");
            var flags = regExp.Substring(lastSlash + 1);

            var options = ParseOptions(r, flags);
            try
            {
                r.Value = new Regex(pattern, options);
            }
            catch (Exception e)
            {
                throw new JavaScriptException(Engine.SyntaxError, e.Message);
            }

            r.Flags = flags;
            r.Source = System.String.IsNullOrEmpty(pattern) ? "(?:)" : pattern;

            r.FastAddProperty("global", r.Global, false, false, false);
            r.FastAddProperty("ignoreCase", r.IgnoreCase, false, false, false);
            r.FastAddProperty("multiline", r.Multiline, false, false, false);
            r.FastAddProperty("source", r.Source, false, false, false);
            r.FastAddProperty("lastIndex", 0, true, false, false);

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
