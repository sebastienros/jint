using System;
using System.Text.RegularExpressions;
using Esprima;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.RegExp
{
    public sealed class RegExpConstructor : FunctionInstance, IConstructor
    {
        public RegExpConstructor(Engine engine)
            : base(engine, "RegExp", null, null, false)
        {
        }

        public static RegExpConstructor CreateRegExpConstructor(Engine engine)
        {
            var obj = new RegExpConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the RegExp constructor is the Function prototype object
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = RegExpPrototype.CreatePrototypeObject(engine, obj);

            obj.SetOwnProperty("length", new PropertyDescriptor(2, PropertyFlag.AllForbidden));

            // The initial value of RegExp.prototype is the RegExp prototype object
            obj.SetOwnProperty("prototype", new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden));

            return obj;
        }

        public void Configure()
        {
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var pattern = arguments.At(0);
            var flags = arguments.At(1);

            if (!pattern.IsUndefined()
                && flags.IsUndefined()
                && TypeConverter.ToObject(Engine, pattern).Class == "Regex")
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
            if (flags.IsUndefined() && !ReferenceEquals(r, null))
            {
                return r;
            }

            if (!flags.IsUndefined() && !ReferenceEquals(r, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            if (pattern.IsUndefined())
            {
                p = "";
            }
            else
            {
                p = TypeConverter.ToString(pattern);
            }

            f = !flags.IsUndefined() ? TypeConverter.ToString(flags) : "";

            r = new RegExpInstance(Engine);
            r.Prototype = PrototypeObject;
            r.Extensible = true;

            try
            {
                var options = new Scanner("").ParseRegexOptions(f);
                r.Value = new Regex(p, options);
            }
            catch (Exception e)
            {
                ExceptionHelper.ThrowSyntaxError(_engine, e.Message);
            }

            var s = p;

            if (string.IsNullOrEmpty(s))
            {
                s = "(?:)";
            }

            r.Flags = f;
            r.Source = s;
            AssignFlags(r, f);

            SetRegexProperties(r);

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
            AssignFlags(r, flags);
            r.Source = System.String.IsNullOrEmpty(body) ? "(?:)" : body;

            SetRegexProperties(r);

            return r;
        }

        public RegExpInstance Construct(Regex regExp, string flags)
        {
            var r = new RegExpInstance(Engine);
            r.Prototype = PrototypeObject;
            r.Extensible = true;

            r.Flags = flags;
            AssignFlags(r, flags);

            r.Source = regExp.ToString();
            r.Value = regExp;

            SetRegexProperties(r);

            return r;
        }

        private static void SetRegexProperties(RegExpInstance r)
        {
            r.SetOwnProperty("global", new PropertyDescriptor(r.Global, PropertyFlag.AllForbidden));
            r.SetOwnProperty("ignoreCase", new PropertyDescriptor(r.IgnoreCase, PropertyFlag.AllForbidden));
            r.SetOwnProperty("multiline", new PropertyDescriptor(r.Multiline, PropertyFlag.AllForbidden));
            r.SetOwnProperty("source", new PropertyDescriptor(r.Source, PropertyFlag.AllForbidden));
            r.SetOwnProperty("lastIndex", new PropertyDescriptor(0, PropertyFlag.OnlyWritable));
        }

        private static void AssignFlags(RegExpInstance r, string flags)
        {
            for(var i=0; i < flags.Length; i++)
            {
                switch (flags[i])
                {
                    case 'i':
                        r.IgnoreCase = true;
                        break;
                    case 'm':
                        r.Multiline = true;
                        break;
                    case 'g':
                        r.Global = true;
                        break;
                }
            }
        }

        public RegExpPrototype PrototypeObject { get; private set; }
    }
}
