using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Esprima;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.RegExp
{
    public sealed class RegExpConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("RegExp");

        public RegExpConstructor(Engine engine)
            : base(engine, _functionName, FunctionThisMode.Global)
        {
        }

        public static RegExpConstructor CreateRegExpConstructor(Engine engine)
        {
            var obj = new RegExpConstructor(engine)
            {
                _prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the RegExp constructor is the Function prototype object
            obj.PrototypeObject = RegExpPrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(2, PropertyFlag.AllForbidden);

            // The initial value of RegExp.prototype is the RegExp prototype object
            obj._prototypeDescriptor= new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        protected override void Initialize()
        {
            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(_engine, "get [Symbol.species]", (thisObj, _) => thisObj, 0, PropertyFlag.Configurable), set: Undefined, PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments, thisObject);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            return Construct(arguments, this);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-regexp-pattern-flags
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var pattern = arguments.At(0);
            var flags = arguments.At(1);

            var patternIsRegExp = pattern.IsRegExp();
            if (newTarget.IsUndefined())
            {
                newTarget = this;
                if (patternIsRegExp && flags.IsUndefined())
                {
                    var patternConstructor = pattern.Get(CommonProperties.Constructor);
                    if (ReferenceEquals(newTarget, patternConstructor))
                    {
                        return (ObjectInstance) pattern;
                    }
                }
            }

            JsValue p;
            JsValue f;
            if (pattern is RegExpInstance regExpInstance)
            {
                p = regExpInstance.Source;
                f = flags.IsUndefined() ? regExpInstance.Flags : flags;
            }
            else if (patternIsRegExp)
            {
                p = pattern.Get(RegExpPrototype.PropertySource);
                f = flags.IsUndefined() ? pattern.Get(RegExpPrototype.PropertyFlags) : flags;
            }
            else
            {
                p = pattern;
                f = flags;
            }

            var r = RegExpAlloc();
            return RegExpInitialize(r, p, f);
        }

        private ObjectInstance RegExpInitialize(RegExpInstance r, JsValue pattern, JsValue flags)
        {
            var p = pattern.IsUndefined() ? "" : TypeConverter.ToString(pattern);
            if (string.IsNullOrEmpty(p))
            {
                p = "(?:)";
            }

            var f = flags.IsUndefined() ? "" : TypeConverter.ToString(flags);

            try
            {
                var scanner = new Scanner("/" + p + "/" + flags , new ParserOptions { AdaptRegexp = true });
               
                // seems valid
                r.Value = scanner.TestRegExp(p, f);

                var timeout = _engine.Options._RegexTimeoutInterval;
                if (timeout.Ticks > 0)
                {
                    r.Value = new Regex(r.Value.ToString(), r.Value.Options, timeout);
                }
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowSyntaxError(_engine, ex.Message);
            }

            r.Flags = f;
            r.Source = p;
            
            RegExpInitialize(r);

            return r;
        }

        private RegExpInstance RegExpAlloc()
        {
            var r = new RegExpInstance(Engine)
            {
                _prototype = PrototypeObject
            };
            return r;
        }

        public RegExpInstance Construct(string regExp, Engine engine)
        {
            var r = new RegExpInstance(Engine);
            r._prototype = PrototypeObject;

            var scanner = new Scanner(regExp, new ParserOptions { AdaptRegexp = true });
            var body = (string)scanner.ScanRegExpBody().Value;
            var flags = (string)scanner.ScanRegExpFlags().Value;
            r.Value = scanner.TestRegExp(body, flags);

            var timeout = engine.Options._RegexTimeoutInterval;
            if (timeout.Ticks > 0)
            {
                r.Value = new Regex(r.Value.ToString(), r.Value.Options);
            }

            r.Flags = flags;
            r.Source = string.IsNullOrEmpty(body) ? "(?:)" : body;

            RegExpInitialize(r);
            
            return r;
        }

        public RegExpInstance Construct(Regex regExp, string flags, Engine engine)
        {
            var r = new RegExpInstance(Engine);
            r._prototype = PrototypeObject;

            r.Flags = flags;
            r.Source = regExp.ToString();

            var timeout = _engine.Options._RegexTimeoutInterval;
            if (timeout.Ticks > 0)
            {
                r.Value = new Regex(regExp.ToString(), regExp.Options, timeout);
            }
            else
            {
                r.Value = regExp;
            }

            RegExpInitialize(r);

            return r;
        }
        
        private static void RegExpInitialize(RegExpInstance r)
        {
            r.SetOwnProperty(RegExpInstance.PropertyLastIndex, new PropertyDescriptor(0, PropertyFlag.OnlyWritable));
        }

        public RegExpPrototype PrototypeObject { get; private set; }

        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}
